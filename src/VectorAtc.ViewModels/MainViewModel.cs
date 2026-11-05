using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using VectorAtc.Core.Abstractions;
using VectorAtc.Core.Controller;
using VectorAtc.Core.Models;
using VectorAtc.Core.Phraseology;
using VectorAtc.Localization;

namespace VectorAtc.ViewModels;

/// <summary>
/// Логика главного окна. Не зависит от WPF, поэтому проверяется обычными тестами.
/// Предполагает вызовы из одного (UI) потока: события источника данных симулятора должны
/// переводиться в этот поток снаружи, когда появится реальный SimConnect.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private static readonly string[] QuickKeys =
    {
        "Quick.Taxi", "Quick.Pushback", "Quick.RadioCheck", "Quick.SayAgain", "Quick.ReadyDeparture",
    };

    private readonly ILocalizationService _loc;
    private readonly ControllerEngine _engine;
    private readonly ExchangeLog _log;
    private ISimDataSource _sim;
    private readonly ISimDataSource _demoSim;
    private readonly Func<ISimDataSource>? _realSimFactory;
    private ISimDataSource? _realSim;
    private AircraftState? _lastAircraft;
    private readonly Func<bool>? _advanceDemoFrame;
    private readonly RelayCommand _sendCommand;
    private readonly RelayCommand _connectCommand;
    private readonly RelayCommand _nextFrameCommand;
    private readonly RelayCommand _toggleSourceCommand;

    private string _callsign = "N172SP";
    private string _inputText = "";
    private IReadOnlyList<QuickAction> _quickActions = Array.Empty<QuickAction>();

    public MainViewModel(
        ILocalizationService loc, ControllerEngine engine, ExchangeLog log, ISimDataSource sim,
        Func<bool>? advanceDemoFrame = null, Func<ISimDataSource>? realSimFactory = null)
    {
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _sim = sim ?? throw new ArgumentNullException(nameof(sim));
        _advanceDemoFrame = advanceDemoFrame;
        _demoSim = _sim;
        _realSimFactory = realSimFactory;

        _sendCommand = new RelayCommand(_ => SendInput(), _ => !string.IsNullOrWhiteSpace(InputText));
        _connectCommand = new RelayCommand(_ => ToggleConnection());
        _nextFrameCommand = new RelayCommand(
            _ => _advanceDemoFrame?.Invoke(),
            _ => IsDemo && _sim.ConnectionState == SimConnectionState.Connected);
        _toggleSourceCommand = new RelayCommand(
            _ => ToggleSource(),
            _ => _realSimFactory is not null && _sim.ConnectionState == SimConnectionState.Disconnected);

        SendCommand = _sendCommand;
        ConnectCommand = _connectCommand;
        NextFrameCommand = _nextFrameCommand;
        ToggleSourceCommand = _toggleSourceCommand;
        ToggleLanguageCommand = new RelayCommand(_ => ToggleLanguage());
        ToggleStyleCommand = new RelayCommand(_ => ToggleStyle());

        _engine.Culture = _loc.Culture;
        RebuildQuickActions();

        _log.EntryAdded += (_, entry) => Lines.Add(ToLine(entry));
        _loc.LanguageChanged += (_, _) => OnLanguageChanged();
        HookSim(_sim);
    }

    public ObservableCollection<LogLine> Lines { get; } = new();

    public ICommand SendCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand NextFrameCommand { get; }
    public ICommand ToggleSourceCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand ToggleStyleCommand { get; }

    public string Callsign
    {
        get => _callsign;
        set => SetProperty(ref _callsign, value ?? "");
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value ?? ""))
                _sendCommand.NotifyCanExecuteChanged();
        }
    }

    public IReadOnlyList<QuickAction> QuickActions
    {
        get => _quickActions;
        private set => SetProperty(ref _quickActions, value);
    }

    // Подписи (локализуются, обновляются при смене языка)
    public string Title => _loc["App.Title"];
    public string LabelLog => _loc["Ui.Log"];
    public string LabelStatus => _loc["Ui.Status"];
    public string LabelPhase => _loc["Ui.Phase"];
    public string LabelState => _loc["Ui.State"];
    public string LabelCallsign => _loc["Ui.Callsign"];
    public string LabelSend => _loc["Ui.Send"];
    public string LabelNextFrame => _loc["Ui.NextFrame"];
    public string InputHint => _loc["Ui.InputHint"];

    // Состояние
    public string StatusText => _loc[_sim.ConnectionState switch
    {
        SimConnectionState.Connected => IsDemo ? "Sim.DemoConnected" : "Sim.Connected",
        SimConnectionState.Connecting => "Sim.Connecting",
        _ => "Sim.Disconnected",
    }];

    public string ConnectButtonText =>
        _loc[_sim.ConnectionState == SimConnectionState.Disconnected ? "Ui.Connect" : "Ui.Disconnect"];

    /// <summary>true, пока источником данных служит встроенная имитация.</summary>
    public bool IsDemo => _advanceDemoFrame is not null && ReferenceEquals(_sim, _demoSim);

    public string SourceButtonText => _loc[IsDemo ? "Ui.SourceDemo" : "Ui.SourceSimulator"];

    public string LabelTelemetry => _loc["Ui.Telemetry"];

    /// <summary>Последние данные самолёта одной строкой: по ним видно, что данные реально приходят.</summary>
    public string TelemetryText
    {
        get
        {
            var a = _lastAircraft;
            if (a is null) return "—";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:F5}, {1:F5}  |  {2:F0} ft  |  {3:F0}°  |  {4:F0} kt  |  {5}  |  COM1 {6:F3}  COM2 {7:F3}",
                a.Position.LatitudeDeg, a.Position.LongitudeDeg, a.AltitudeFeet, a.HeadingMagneticDeg, a.GroundSpeedKnots,
                _loc[a.OnGround ? "Ui.OnGround" : "Ui.InAir"], a.Com1ActiveKhz / 1000.0, a.Com2ActiveKhz / 1000.0);
        }
    }

    public string PhaseText => _loc[$"Phase.{_engine.Phase}"];
    public string StateText => _loc[$"State.{_engine.State}"];

    /// <summary>Подпись кнопки языка показывает язык, на который произойдёт переключение.</summary>
    public string LanguageButtonText => _loc.Culture.TwoLetterISOLanguageName == "ru" ? "EN" : "RU";

    /// <summary>Подпись кнопки стиля показывает текущий стиль фразеологии.</summary>
    public string StyleButtonText => _engine.Style == PhraseStyle.Icao ? "ICAO" : "US";

    private void SendInput()
    {
        var text = InputText.Trim();
        InputText = "";
        SendText(text);
    }

    private void SendText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _engine.HandlePilotMessage(text);
        RefreshFlight();
    }

    private async void ToggleConnection()
    {
        try
        {
            if (_sim.ConnectionState == SimConnectionState.Disconnected)
                await _sim.ConnectAsync();
            else
                await _sim.DisconnectAsync();
        }
        catch (SimConnectionException ex)
        {
            _log.Add(LogSource.Violation, _loc["Sim.Failure." + ex.Kind]);
        }
        catch (Exception ex)
        {
            _log.Add(LogSource.Violation, ex.Message);
        }
    }

    private void ToggleLanguage() =>
        _loc.SetCulture(CultureInfo.GetCultureInfo(_loc.Culture.TwoLetterISOLanguageName == "ru" ? "en" : "ru"));

    private void ToggleStyle()
    {
        _engine.Style = _engine.Style == PhraseStyle.Icao ? PhraseStyle.Us : PhraseStyle.Icao;
        OnPropertyChanged(nameof(StyleButtonText));
    }

    private void OnLanguageChanged()
    {
        _engine.Culture = _loc.Culture;
        RebuildQuickActions();
        Lines.Clear();
        foreach (var entry in _log.Entries)
            Lines.Add(ToLine(entry));
        OnPropertyChanged(string.Empty); // обновить все подписи
    }

    private void RebuildQuickActions() =>
        QuickActions = QuickKeys
            .Select(key =>
            {
                var label = _loc[key];
                return new QuickAction(label, new RelayCommand(_ => SendText($"{Callsign} {label}")));
            })
            .ToList();

    private void RefreshFlight()
    {
        OnPropertyChanged(nameof(PhaseText));
        OnPropertyChanged(nameof(StateText));
    }

    private void RefreshConnection()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ConnectButtonText));
        _nextFrameCommand.NotifyCanExecuteChanged();
        _toggleSourceCommand.NotifyCanExecuteChanged();
    }

    private void HookSim(ISimDataSource sim)
    {
        sim.ConnectionStateChanged += OnSimConnectionChanged;
        sim.AircraftStateUpdated += OnSimAircraftUpdated;
    }

    private void UnhookSim(ISimDataSource sim)
    {
        sim.ConnectionStateChanged -= OnSimConnectionChanged;
        sim.AircraftStateUpdated -= OnSimAircraftUpdated;
    }

    private void OnSimConnectionChanged(object? sender, SimConnectionState state) => RefreshConnection();

    private void OnSimAircraftUpdated(object? sender, AircraftState state)
    {
        _lastAircraft = state;
        _engine.OnAircraftState(state);
        OnPropertyChanged(nameof(TelemetryText));
        RefreshFlight();
    }

    /// <summary>Переключает источник данных между демо и симулятором. Только пока нет подключения.</summary>
    private void ToggleSource()
    {
        if (_realSimFactory is null || _sim.ConnectionState != SimConnectionState.Disconnected)
            return;

        UnhookSim(_sim);
        _sim = ReferenceEquals(_sim, _demoSim) ? _realSim ??= _realSimFactory() : _demoSim;
        HookSim(_sim);
        _lastAircraft = null;
        OnPropertyChanged(string.Empty);
        _nextFrameCommand.NotifyCanExecuteChanged();
    }

    private LogLine ToLine(LogEntry entry)
    {
        var speaker = entry.Source switch
        {
            LogSource.Pilot => _loc["Log.PilotPrefix"],
            LogSource.Controller => _loc["Log.ControllerPrefix"],
            _ => _loc["Log.ViolationPrefix"],
        };
        return new LogLine(entry.Time.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            speaker, entry.Code is null ? entry.Text : _loc[entry.Code], entry.Source);
    }
}
