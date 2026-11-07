using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using VectorAtc.Core.Abstractions;
using VectorAtc.Core.Controller;
using VectorAtc.Core.Flight;
using VectorAtc.Core.Geo;
using VectorAtc.Core.Ground;
using VectorAtc.Core.Traffic;
using VectorAtc.Core.Speech;
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
    private readonly CabinAnnouncer? _cabin;
    private readonly ISpeechOutput? _speech;
    private bool _voiceEnabled = true;
    private readonly ISpeechInput? _listener;
    private readonly IGroundServicesProvider? _ground;
    private readonly ITrafficSource? _traffic;
    private IReadOnlyList<TrafficRow> _trafficRows = Array.Empty<TrafficRow>();
    private MainView _view = MainView.Radio;
    private static readonly double[] MapRanges = { 10, 20, 40, 80, 160 };
    private int _mapRangeIndex = 2;
    private double _mapWidth, _mapHeight;
    private IReadOnlyList<MapMarker> _mapMarkers = Array.Empty<MapMarker>();
    private IReadOnlyList<MapRing> _mapRings = Array.Empty<MapRing>();
    private bool _isListening;
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
        Func<bool>? advanceDemoFrame = null, Func<ISimDataSource>? realSimFactory = null, CabinAnnouncer? cabin = null,
        ISpeechOutput? speech = null, ISpeechInput? listener = null,
        IGroundServicesProvider? ground = null, ITrafficSource? traffic = null)
    {
        _loc = loc ?? throw new ArgumentNullException(nameof(loc));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _sim = sim ?? throw new ArgumentNullException(nameof(sim));
        _advanceDemoFrame = advanceDemoFrame;
        _demoSim = _sim;
        _realSimFactory = realSimFactory;
        _cabin = cabin;
        _speech = speech;
        _listener = listener;
        _ground = ground;
        _traffic = traffic;
        RefreshTraffic();
        if (_ground is not null)
        {
            foreach (var status in _ground.Statuses)
                GroundServices.Add(new GroundServiceItem(status.Service, RequestGround));
            RefreshGround();
            _ground.Changed += (_, change) => OnGroundChanged(change);
        }
        if (_listener is not null)
        {
            _listener.Recognized += (_, text) => OnHeard(text);
            _listener.NotUnderstood += (_, _) => LogVoiceNote("Voice.NotUnderstood");
            _listener.Failed += (_, _) => LogVoiceNote("Voice.Failed");
        }

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
        ToggleVoiceCommand = new RelayCommand(_ => ToggleVoice(), _ => _speech is not null);

        _engine.Culture = _loc.Culture;
        RebuildQuickActions();

        _log.EntryAdded += (_, entry) =>
        {
            Lines.Add(ToLine(entry));
            Speak(entry);
        };
        _loc.LanguageChanged += (_, _) => OnLanguageChanged();
        HookSim(_sim);
    }

    public ObservableCollection<LogLine> Lines { get; } = new();

    public ObservableCollection<GroundServiceItem> GroundServices { get; } = new();

    public bool IsGroundVisible => _ground is not null;

    public bool IsTabsVisible => _traffic is not null;

    private enum MainView
    {
        Radio,
        Traffic,
        Map
    }

    public bool IsLogView => _view == MainView.Radio;
    public bool IsTrafficView => _view == MainView.Traffic;
    public bool IsMapView => _view == MainView.Map;

    private void SelectView(MainView view)
    {
        if (_view == view) return;
        _view = view;
        OnPropertyChanged(nameof(IsLogView));
        OnPropertyChanged(nameof(IsTrafficView));
        OnPropertyChanged(nameof(IsMapView));
    }

    public ICommand ShowRadioCommand => new RelayCommand(_ => SelectView(MainView.Radio));

    public ICommand ShowTrafficCommand => new RelayCommand(_ => SelectView(MainView.Traffic), _ => _traffic is not null);

    public ICommand ShowMapCommand => new RelayCommand(_ => SelectView(MainView.Map), _ => _traffic is not null);

    public ICommand ZoomInCommand => new RelayCommand(_ => ChangeZoom(-1));

    public ICommand ZoomOutCommand => new RelayCommand(_ => ChangeZoom(1));

    public IReadOnlyList<MapMarker> MapMarkers
    {
        get => _mapMarkers;
        private set => SetProperty(ref _mapMarkers, value);
    }

    public IReadOnlyList<MapRing> MapRings
    {
        get => _mapRings;
        private set => SetProperty(ref _mapRings, value);
    }

    public string LabelTabMap => _loc["Ui.TabMap"];

    public string MapRangeText => string.Format(CultureInfo.InvariantCulture, "{0}: {1:F0} nm", _loc["Ui.MapRange"], MapRanges[_mapRangeIndex]);

    /// <summary>Окно сообщает размер области карты в пикселях.</summary>
    public void SetMapViewport(double width, double height)
    {
        _mapWidth = width;
        _mapHeight = height;
        RefreshMap();
    }

    private void ChangeZoom(int step)
    {
        var next = Math.Clamp(_mapRangeIndex + step, 0, MapRanges.Length - 1);
        if (next == _mapRangeIndex) return;
        _mapRangeIndex = next;
        OnPropertyChanged(nameof(MapRangeText));
        RefreshMap();
    }

    private void RefreshMap()
    {
        if (_mapWidth <= 0 || _mapHeight <= 0)
        {
            MapMarkers = Array.Empty<MapMarker>();
            MapRings = Array.Empty<MapRing>();
            return;
        }

        var range = MapRanges[_mapRangeIndex];
        var center = _lastAircraft?.Position ?? _engine.Airport.HoldPoint;
        var map = new MapProjection(center, range, _mapWidth, _mapHeight);
        var cx = _mapWidth / 2.0;
        var cy = _mapHeight / 2.0;

        var rings = new List<MapRing>();
        foreach (var fraction in new[] { 0.5, 1.0 })
        {
            var radius = range * fraction * map.PixelsPerNm;
            rings.Add(new MapRing(cx - radius, cy - radius, radius * 2,
                string.Format(CultureInfo.InvariantCulture, "{0:F0} nm", range * fraction), cx, cy - radius));
        }

        var markers = new List<MapMarker>();
        if (_traffic is not null)
        {
            foreach (var t in _traffic.Targets)
            {
                var p = map.Project(t.Position);
                if (!map.IsVisible(p, 20)) continue;
                markers.Add(new MapMarker(p.X, p.Y, t.HeadingDeg, t.Callsign,
                    $"{t.AirlineIcao} · {t.AircraftIcao} · {t.OriginIcao}→{t.DestinationIcao}",
                    t.OnGround ? MapMarkerKind.Ground : MapMarkerKind.Airborne));
            }
        }

        if (_lastAircraft is { } own)
            markers.Add(new MapMarker(cx, cy, own.HeadingMagneticDeg, _loc["Ui.MapYou"], Callsign, MapMarkerKind.Own));

        MapRings = rings;
        MapMarkers = markers;
    }

    public IReadOnlyList<TrafficRow> TrafficRows
    {
        get => _trafficRows;
        private set => SetProperty(ref _trafficRows, value);
    }

    public string LabelTabRadio => _loc["Ui.TabRadio"];
    public string LabelTabTraffic => _loc["Ui.TabTraffic"];
    public string LabelTrafficTitle => _loc["Ui.TrafficTitle"];
    public string LabelTrafficNote => _loc["Ui.TrafficNote"];
    public string LabelColCallsign => _loc["Ui.ColCallsign"];
    public string LabelColAirline => _loc["Ui.ColAirline"];
    public string LabelColType => _loc["Ui.ColType"];
    public string LabelColRoute => _loc["Ui.ColRoute"];
    public string LabelColAltitude => _loc["Ui.ColAltitude"];
    public string LabelColSpeed => _loc["Ui.ColSpeed"];
    public string LabelColDistance => _loc["Ui.ColDistance"];

    /// <summary>Один шаг времени для всего, что движется само: услуги и трафик (вызывается таймером окна).</summary>
    public void Tick(TimeSpan elapsed)
    {
        AdvanceGround(elapsed);
        _traffic?.Advance(elapsed);
        RefreshTraffic();
        RefreshMap();
    }

    private void RefreshTraffic()
    {
        if (_traffic is null) return;
        var own = _lastAircraft?.Position;
        var rows = _traffic.Targets
            .Select(t => (Target: t, DistanceNm: own is { } o ? VectorAtc.Core.Geo.Geo.DistanceMeters(o, t.Position) / 1852.0 : (double?)null))
            .OrderBy(x => x.DistanceNm ?? double.MaxValue)
            .ThenBy(x => x.Target.Callsign, StringComparer.Ordinal)
            .Select(x => new TrafficRow(
                x.Target.Callsign, x.Target.AirlineIcao, x.Target.AircraftIcao,
                x.Target.OriginIcao + " → " + x.Target.DestinationIcao,
                FormatAltitude(x.Target),
                string.Format(CultureInfo.InvariantCulture, "{0:F0} kt", x.Target.GroundSpeedKnots),
                x.DistanceNm is null ? "—" : string.Format(CultureInfo.InvariantCulture, "{0:F0} nm", x.DistanceNm)))
            .ToList();
        TrafficRows = rows;
    }

    private static string FormatAltitude(TrafficTarget t) =>
        t.OnGround ? "GND"
        : t.AltitudeFeet >= 18000 ? string.Format(CultureInfo.InvariantCulture, "FL{0:000}", t.AltitudeFeet / 100)
        : string.Format(CultureInfo.InvariantCulture, "{0:F0} ft", t.AltitudeFeet);

    public string LabelGround => _loc["Ui.GroundCaption"];

    public string LabelOrder => _loc["Ui.GroundOrder"];

    /// <summary>Сообщает, сколько времени прошло, чтобы услуги двигались (вызывается таймером окна).</summary>
    public void AdvanceGround(TimeSpan elapsed)
    {
        if (_ground is null) return;
        _ground.Advance(elapsed);
        RefreshGround();
    }

    private void RequestGround(GroundService service)
    {
        if (_ground is null) return;
        var a = _lastAircraft;
        if (a is not null && (!a.OnGround || a.GroundSpeedKnots >= 1))
        {
            _log.Add(LogSource.Violation, _loc["Ground.Unavailable"], "Ground.Unavailable");
            return;
        }

        if (!_ground.Request(service))
            _log.Add(LogSource.Violation, _loc["Ground.Busy"], "Ground.Busy");
        RefreshGround();
    }

    private void OnGroundChanged(GroundServiceChange change)
    {
        var key = (change.State == GroundServiceState.InProgress ? "Ground.Started." : "Ground.Done.") + change.Service;
        _log.Add(LogSource.Ground, _loc[key], key);
        RefreshGround();
    }

    private void RefreshGround()
    {
        if (_ground is null) return;
        var statuses = _ground.Statuses;
        for (var i = 0; i < statuses.Count && i < GroundServices.Count; i++)
        {
            var s = statuses[i];
            var state = s.State switch
            {
                GroundServiceState.InProgress => string.Format(CultureInfo.InvariantCulture, "{0:F0}%", s.Progress * 100),
                GroundServiceState.Completed => _loc["Ground.State.Completed"],
                _ => _loc["Ground.State.Idle"],
            };
            GroundServices[i].Update(s, _loc["Ground.Service." + s.Service], state);
        }
    }

    public ICommand SendCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand NextFrameCommand { get; }
    public ICommand ToggleVoiceCommand { get; }
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

    /// <summary>true, пока есть соединение с источником данных (индикатор в окне).</summary>
    public bool IsConnected => _sim.ConnectionState == SimConnectionState.Connected;

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
        _speech?.Stop();
        EndTalk();
        RefreshGround();
        RefreshTraffic();
        RefreshMap();
        RebuildQuickActions();
        Lines.Clear();
        foreach (var entry in _log.Entries)
            Lines.Add(ToLine(entry));
        OnPropertyChanged(string.Empty); // обновить все подписи
    }

    /// <summary>Подпись кнопки голоса: включён, выключен или нет голоса для языка интерфейса.</summary>
    public string VoiceButtonText =>
        _speech is null || !_speech.CanSpeak(_loc.Culture) ? _loc["Ui.VoiceMissing"]
        : _loc[_voiceEnabled ? "Ui.VoiceOn" : "Ui.VoiceOff"];

    public bool IsVoiceVisible => _speech is not null;

    public bool IsTalkVisible => _listener is not null;

    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (_isListening == value) return;
            _isListening = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TalkButtonText));
        }
    }

    /// <summary>Подпись кнопки «говорить»: готова, слушает или нет распознавателя для языка.</summary>
    public string TalkButtonText =>
        _listener is null || !_listener.CanListen(_loc.Culture) ? _loc["Ui.TalkMissing"]
        : _loc[IsListening ? "Ui.TalkListening" : "Ui.TalkIdle"];

    /// <summary>Кнопку нажали: начать слушать микрофон. Свою речь диспетчера на это время прерываем.</summary>
    public void BeginTalk()
    {
        if (_listener is null || IsListening || !_listener.CanListen(_loc.Culture))
            return;
        _speech?.Stop();
        IsListening = true;
        _listener.StartListening(_loc.Culture);
    }

    /// <summary>Кнопку отпустили: закончить слушать, результат придёт отдельно.</summary>
    public void EndTalk()
    {
        if (_listener is null || !IsListening)
            return;
        IsListening = false;
        _listener.StopListening();
    }

    private void OnHeard(string text)
    {
        IsListening = false;
        SendText(text);
    }

    private void LogVoiceNote(string key)
    {
        IsListening = false;
        _log.Add(LogSource.Violation, _loc[key], key);
    }

    private void ToggleVoice()
    {
        _voiceEnabled = !_voiceEnabled;
        if (!_voiceEnabled)
            _speech?.Stop();
        OnPropertyChanged(nameof(VoiceButtonText));
    }

    /// <summary>Озвучивает реплики диспетчера и салона; слова пилота и замечания не озвучиваются.</summary>
    private void Speak(LogEntry entry)
    {
        if (_speech is null || !_voiceEnabled || entry.Source is LogSource.Pilot or LogSource.Violation or LogSource.Ground)
            return;
        var role = entry.Source == LogSource.Cabin ? SpeechRole.Cabin : SpeechRole.Controller;
        _speech.Speak(entry.Text, _loc.Culture, role);
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
        OnPropertyChanged(nameof(IsConnected));
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
        RefreshMap();
        if (_cabin is not null)
        {
            foreach (var announcement in _cabin.Update(state))
                _log.Add(LogSource.Cabin, _loc["Cabin." + announcement], "Cabin." + announcement);
        }

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
            LogSource.Cabin => _loc["Log.CabinPrefix"],
            LogSource.Ground => _loc["Log.GroundPrefix"],
            _ => _loc["Log.ViolationPrefix"],
        };
        return new LogLine(entry.Time.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            speaker, entry.Code is null ? entry.Text : _loc[entry.Code], entry.Source);
    }
}
