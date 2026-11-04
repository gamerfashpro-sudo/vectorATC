using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using VectorAtc.Core.Abstractions;
using VectorAtc.Core.Controller;
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
    private readonly ISimDataSource _sim;
    private readonly Func<bool>? _advanceDemoFrame;
    private readonly RelayCommand _sendCommand;
    private readonly RelayCommand _connectCommand;
    private readonly RelayCommand _nextFrameCommand;

    private string _callsign = "N172SP";
    private string _inputText = "";
    private IReadOnlyList<QuickAction> _quickActions = Array.Empty<QuickAction>();

    public MainViewModel(
        ILocalizationService loc, ControllerEngine engine, ExchangeLog log, ISimDataSource sim,
        Func<bool>? advanceDemoFrame = null)
    {
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _sim = sim ?? throw new ArgumentNullException(nameof(sim));
        _advanceDemoFrame = advanceDemoFrame;

        _sendCommand = new RelayCommand(_ => SendInput(), _ => !string.IsNullOrWhiteSpace(InputText));
        _connectCommand = new RelayCommand(_ => ToggleConnection());
        _nextFrameCommand = new RelayCommand(
            _ => _advanceDemoFrame?.Invoke(),
            _ => _advanceDemoFrame is not null && _sim.ConnectionState == SimConnectionState.Connected);

        SendCommand = _sendCommand;
        ConnectCommand = _connectCommand;
        NextFrameCommand = _nextFrameCommand;
        ToggleLanguageCommand = new RelayCommand(_ => ToggleLanguage());
        ToggleStyleCommand = new RelayCommand(_ => ToggleStyle());

        _engine.Culture = _loc.Culture;
        RebuildQuickActions();

        _log.EntryAdded += (_, entry) => Lines.Add(ToLine(entry));
        _loc.LanguageChanged += (_, _) => OnLanguageChanged();
        _sim.ConnectionStateChanged += (_, _) => RefreshConnection();
        _sim.AircraftStateUpdated += (_, state) =>
        {
            _engine.OnAircraftState(state);
            RefreshFlight();
        };
    }

    public ObservableCollection<LogLine> Lines { get; } = new();

    public ICommand SendCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand NextFrameCommand { get; }
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
        SimConnectionState.Connected => "Sim.Connected",
        SimConnectionState.Connecting => "Sim.Connecting",
        _ => "Sim.Disconnected",
    }];

    public string ConnectButtonText =>
        _loc[_sim.ConnectionState == SimConnectionState.Disconnected ? "Ui.Connect" : "Ui.Disconnect"];

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
            speaker, entry.Text, entry.Source);
    }
}
