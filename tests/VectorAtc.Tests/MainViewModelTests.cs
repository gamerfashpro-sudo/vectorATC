using System.Globalization;
using VectorAtc.Core.Speech;
using VectorAtc.Core.Controller;
using VectorAtc.Core.Traffic;
using VectorAtc.Core.Ground;
using VectorAtc.Core.Models;
using VectorAtc.Core.Flight;
using VectorAtc.Core.Parsing;
using VectorAtc.Core.Phraseology;
using VectorAtc.Localization;
using VectorAtc.SimLink;
using VectorAtc.ViewModels;
using Xunit;

namespace VectorAtc.Tests;

public class MainViewModelTests
{
    private const string GoodReadback =
        "taxi to holding point runway two seven via alpha bravo November one seven two Sierra Papa";

    private sealed class Fixture
    {
        public LocalizationService Loc { get; } = new();
        public ExchangeLog Log { get; } = new();
        public MockSimDataSource Sim { get; } = new(MockScenarios.ParkedToHoldShort());
        public ControllerEngine Engine { get; }
        public MainViewModel Vm { get; }

        public Fixture()
        {
            Engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, Loc.Culture, Log);
            Vm = new MainViewModel(Loc, Engine, Log, Sim, () => Sim.Advance());
        }

        public void Type(string text) => Vm.InputText = text;
    }

    [Fact]
    public void Send_shows_pilot_and_controller_lines_and_clears_input()
    {
        var f = new Fixture();
        f.Type("N172SP request taxi");
        f.Vm.SendCommand.Execute(null);

        Assert.Equal(2, f.Vm.Lines.Count);
        Assert.Equal("N172SP request taxi", f.Vm.Lines[0].Text);
        Assert.Equal("Pilot", f.Vm.Lines[0].Speaker);
        Assert.Equal("ATC", f.Vm.Lines[1].Speaker);
        Assert.Equal("N172SP, taxi to holding point runway 27 via Alpha, Bravo", f.Vm.Lines[1].Text);
        Assert.Equal("", f.Vm.InputText);
        Assert.Equal("Taxi clearance issued", f.Vm.StateText);
    }

    [Fact]
    public void Send_is_disabled_for_blank_input_and_does_nothing()
    {
        var f = new Fixture();
        Assert.False(f.Vm.SendCommand.CanExecute(null));

        f.Type("   ");
        f.Vm.SendCommand.Execute(null);

        Assert.Empty(f.Vm.Lines);
        f.Type("N172SP radio check");
        Assert.True(f.Vm.SendCommand.CanExecute(null));
    }

    [Fact]
    public void CanExecuteChanged_is_raised_when_input_changes()
    {
        var f = new Fixture();
        var raised = 0;
        f.Vm.SendCommand.CanExecuteChanged += (_, _) => raised++;

        f.Type("hello");
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Property_changes_are_announced_after_sending()
    {
        var f = new Fixture();
        var changed = new List<string?>();
        f.Vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        f.Type("N172SP request taxi");
        f.Vm.SendCommand.Execute(null);

        Assert.Contains("StateText", changed);
        Assert.Contains("PhaseText", changed);
    }

    [Fact]
    public void Quick_action_sends_callsign_plus_localized_text()
    {
        var f = new Fixture();
        f.Vm.Callsign = "UAL123";
        var taxi = f.Vm.QuickActions[0];

        Assert.Equal("Request taxi", taxi.Label);
        taxi.Command.Execute(null);

        Assert.Equal("UAL123 Request taxi", f.Vm.Lines[0].Text);
        Assert.Equal("UAL123, taxi to holding point runway 27 via Alpha, Bravo", f.Vm.Lines[1].Text);
    }

    [Fact]
    public void Every_quick_action_label_is_a_recognised_intent_in_both_languages()
    {
        var parser = new RequestParser();
        foreach (var language in new[] { "en", "ru" })
        {
            var f = new Fixture();
            if (language == "ru") f.Vm.ToggleLanguageCommand.Execute(null);

            Assert.Equal(5, f.Vm.QuickActions.Count);
            foreach (var action in f.Vm.QuickActions)
                Assert.True(parser.Parse($"UAL123 {action.Label}").Intent != IntentType.Unknown);
        }
    }

    [Fact]
    public void Language_toggle_updates_labels_quick_actions_and_controller_replies()
    {
        var f = new Fixture();
        var changed = new List<string?>();
        f.Vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal("RU", f.Vm.LanguageButtonText);
        f.Vm.ToggleLanguageCommand.Execute(null);

        Assert.Equal("EN", f.Vm.LanguageButtonText);
        Assert.Equal("Vector ATC", f.Vm.Title);
        Assert.Equal("Отправить", f.Vm.LabelSend);
        Assert.Equal("Запрашиваю руление", f.Vm.QuickActions[0].Label);
        Assert.Equal("Нет подключения к симулятору", f.Vm.StatusText);
        Assert.Contains("", changed);

        f.Type("N172SP запрашиваю руление");
        f.Vm.SendCommand.Execute(null);
        Assert.Equal("N172SP, рулите на предварительный старт полосы 27 по Alpha, Bravo", f.Vm.Lines[^1].Text);
        Assert.Equal("Диспетчер", f.Vm.Lines[^1].Speaker);
    }

    [Fact]
    public void Style_toggle_switches_between_icao_and_us_phraseology()
    {
        var f = new Fixture();
        Assert.Equal("ICAO", f.Vm.StyleButtonText);

        f.Vm.ToggleStyleCommand.Execute(null);
        Assert.Equal("US", f.Vm.StyleButtonText);

        f.Type("N172SP request taxi");
        f.Vm.SendCommand.Execute(null);
        Assert.Equal("N172SP, runway 27, taxi via Alpha, Bravo", f.Vm.Lines[^1].Text);
    }

    [Fact]
    public void Connect_button_toggles_connection_and_enables_next_frame()
    {
        var f = new Fixture();
        Assert.Equal("Not connected to simulator", f.Vm.StatusText);
        Assert.Equal("Connect", f.Vm.ConnectButtonText);
        Assert.False(f.Vm.NextFrameCommand.CanExecute(null));

        f.Vm.ConnectCommand.Execute(null);

        Assert.Equal("Connected (demo mode)", f.Vm.StatusText);
        Assert.Equal("Disconnect", f.Vm.ConnectButtonText);
        Assert.True(f.Vm.NextFrameCommand.CanExecute(null));

        f.Vm.ConnectCommand.Execute(null);
        Assert.Equal("Not connected to simulator", f.Vm.StatusText);
        Assert.False(f.Vm.NextFrameCommand.CanExecute(null));
    }

    [Fact]
    public void Demo_flow_through_the_window_logic_ends_with_tower_handoff()
    {
        var f = new Fixture();
        f.Vm.ConnectCommand.Execute(null);

        f.Type("N172SP request taxi");
        f.Vm.SendCommand.Execute(null);
        f.Type(GoodReadback);
        f.Vm.SendCommand.Execute(null);
        Assert.Equal("Taxiing", f.Vm.StateText);

        for (var i = 0; i < 5; i++) f.Vm.NextFrameCommand.Execute(null);

        Assert.Equal("Tower", f.Vm.PhaseText);
        Assert.Equal("Tower contacted", f.Vm.StateText);
        Assert.Equal("N172SP, contact tower 118.1", f.Vm.Lines[^1].Text);
    }

    [Fact]
    public void Readback_error_is_shown_as_a_note_line()
    {
        var f = new Fixture();
        f.Type("N172SP request taxi");
        f.Vm.SendCommand.Execute(null);
        f.Type("runway three six via alpha bravo N172SP");
        f.Vm.SendCommand.Execute(null);

        var note = f.Vm.Lines.Single(l => l.Kind == LogSource.Violation);
        Assert.Equal("Note", note.Speaker);
        Assert.Equal("Readback error: Runway", note.Text);
    }

    [Fact]
    public void Language_switch_retranslates_existing_note_and_speaker_labels()
    {
        var f = new Fixture();
        f.Type("N172SP request taxi");
        f.Vm.SendCommand.Execute(null);
        f.Type("runway three six via alpha bravo N172SP");
        f.Vm.SendCommand.Execute(null);
        var count = f.Vm.Lines.Count;

        f.Vm.ToggleLanguageCommand.Execute(null);

        Assert.Equal(count, f.Vm.Lines.Count);
        var note = f.Vm.Lines.Single(l => l.Kind == LogSource.Violation);
        Assert.Equal("Ошибка read-back: полоса", note.Text);
        Assert.Equal("Пилот", f.Vm.Lines[0].Speaker);
    }

    [Fact]
    public void Test_airport_hold_point_matches_final_scenario_frame()
    {
        var last = MockScenarios.ParkedToHoldShort()[^1];
        Assert.Equal(MockScenarios.TestAirport.HoldPoint, last.Position);
    }

    [Fact]
    public void Engine_style_and_culture_can_be_changed_between_messages()
    {
        var log = new ExchangeLog();
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport,
            PhraseStyle.Icao, CultureInfo.GetCultureInfo("en"), log);
        engine.HandlePilotMessage("N172SP radio check");

        engine.Style = PhraseStyle.Us;
        Assert.Equal("N172SP, loud and clear", string.Join("", engine.HandlePilotMessage("N172SP radio check")));

        engine.Culture = CultureInfo.GetCultureInfo("ru");
        Assert.Equal("N172SP, слышу вас хорошо", string.Join("", engine.HandlePilotMessage("N172SP radio check")));
    }

    [Fact]
    public void Demo_status_is_localized_and_not_shown_as_a_real_simulator_connection()
    {
        var f = new Fixture();
        f.Vm.ToggleLanguageCommand.Execute(null);
        f.Vm.ConnectCommand.Execute(null);

        Assert.Equal("Подключено (демо-режим)", f.Vm.StatusText);
    }

    [Fact]
    public void Cabin_announcement_is_logged_and_retranslated_on_language_switch()
    {
        var loc = new LocalizationService();
        var log = new ExchangeLog();
        var sim = new MockSimDataSource();
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, loc.Culture, log);
        var vm = new MainViewModel(loc, engine, log, sim, null, null, new CabinAnnouncer());
        vm.ConnectCommand.Execute(null);

        sim.Push(new AircraftState(new GeoPoint(55, 37), 0, 270, 15, 0, true, 118100, 121500));

        var line = vm.Lines.Single(l => l.Kind == LogSource.Cabin);
        Assert.Equal("Cabin", line.Speaker);
        Assert.Contains("welcome aboard", line.Text);

        vm.ToggleLanguageCommand.Execute(null);

        var ru = vm.Lines.Single(l => l.Kind == LogSource.Cabin);
        Assert.Equal("Салон", ru.Speaker);
        Assert.Contains("добро пожаловать на борт", ru.Text);
    }

    [Fact]
    public void IsConnected_follows_connection_and_notifies_the_window()
    {
        var f = new Fixture();
        var changed = new List<string?>();
        f.Vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.False(f.Vm.IsConnected);
        f.Vm.ConnectCommand.Execute(null);

        Assert.True(f.Vm.IsConnected);
        Assert.Contains("IsConnected", changed);
    }

    private sealed class FakeSpeech : ISpeechOutput
    {
        public List<(string Text, string Lang, SpeechRole Role)> Spoken { get; } = new();
        public int Stops { get; private set; }
        public HashSet<string> Languages { get; } = new() { "en", "ru" };

        public bool CanSpeak(CultureInfo culture) => Languages.Contains(culture.TwoLetterISOLanguageName);
        public void Speak(string text, CultureInfo culture, SpeechRole role) =>
            Spoken.Add((text, culture.TwoLetterISOLanguageName, role));
        public void Stop() => Stops++;
    }

    private static (MainViewModel Vm, FakeSpeech Speech, MockSimDataSource Sim) SpeechFixture()
    {
        var loc = new LocalizationService();
        var log = new ExchangeLog();
        var sim = new MockSimDataSource();
        var speech = new FakeSpeech();
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, loc.Culture, log);
        var vm = new MainViewModel(loc, engine, log, sim, null, null, new CabinAnnouncer(), speech);
        return (vm, speech, sim);
    }

    [Fact]
    public void Controller_reply_is_spoken_but_pilot_line_is_not()
    {
        var (vm, speech, _) = SpeechFixture();
        vm.InputText = "N172SP request taxi";
        vm.SendCommand.Execute(null);

        var spoken = Assert.Single(speech.Spoken);
        Assert.Equal(SpeechRole.Controller, spoken.Role);
        Assert.Equal("en", spoken.Lang);
        Assert.Equal("N172SP, taxi to holding point runway 27 via Alpha, Bravo", spoken.Text);
    }

    [Fact]
    public void Cabin_announcement_is_spoken_with_cabin_role()
    {
        var (vm, speech, sim) = SpeechFixture();
        vm.ConnectCommand.Execute(null);
        sim.Push(new AircraftState(new GeoPoint(55, 37), 0, 270, 15, 0, true, 118100, 121500));

        var spoken = Assert.Single(speech.Spoken);
        Assert.Equal(SpeechRole.Cabin, spoken.Role);
        Assert.Contains("welcome aboard", spoken.Text);
    }

    [Fact]
    public void Voice_off_silences_and_stops_current_speech()
    {
        var (vm, speech, _) = SpeechFixture();
        Assert.Equal("Voice: on", vm.VoiceButtonText);

        vm.ToggleVoiceCommand.Execute(null);

        Assert.Equal("Voice: off", vm.VoiceButtonText);
        Assert.Equal(1, speech.Stops);
        vm.InputText = "N172SP request taxi";
        vm.SendCommand.Execute(null);
        Assert.Empty(speech.Spoken);
    }

    [Fact]
    public void Language_switch_stops_speech_and_reports_missing_voice()
    {
        var (vm, speech, _) = SpeechFixture();
        speech.Languages.Remove("ru");

        vm.ToggleLanguageCommand.Execute(null);

        Assert.Equal(1, speech.Stops);
        Assert.Equal("Голос: нет для этого языка", vm.VoiceButtonText);
    }

    private sealed class FakeListener : ISpeechInput
    {
        public HashSet<string> Languages { get; } = new() { "en", "ru" };
        public List<string> Started { get; } = new();
        public int Stopped { get; private set; }

        public event EventHandler<string>? Recognized;
        public event EventHandler? NotUnderstood;
        public event EventHandler? Failed;

        public bool CanListen(CultureInfo culture) => Languages.Contains(culture.TwoLetterISOLanguageName);
        public void StartListening(CultureInfo culture) => Started.Add(culture.TwoLetterISOLanguageName);
        public void StopListening() => Stopped++;
        public void Hear(string text) => Recognized?.Invoke(this, text);
        public void Mumble() => NotUnderstood?.Invoke(this, EventArgs.Empty);
        public void Break() => Failed?.Invoke(this, EventArgs.Empty);
    }

    private static (MainViewModel Vm, FakeListener Mic, FakeSpeech Speech) MicFixture()
    {
        var loc = new LocalizationService();
        var log = new ExchangeLog();
        var speech = new FakeSpeech();
        var mic = new FakeListener();
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, loc.Culture, log);
        var vm = new MainViewModel(loc, engine, log, new MockSimDataSource(), null, null, null, speech, mic);
        return (vm, mic, speech);
    }

    [Fact]
    public void Hold_to_talk_listens_then_recognised_phrase_is_sent_like_typed_text()
    {
        var (vm, mic, speech) = MicFixture();
        Assert.Equal("Hold to talk", vm.TalkButtonText);

        vm.BeginTalk();
        Assert.True(vm.IsListening);
        Assert.Equal("Listening…", vm.TalkButtonText);
        Assert.Equal(new[] { "en" }, mic.Started.ToArray());
        Assert.Equal(1, speech.Stops);

        vm.EndTalk();
        Assert.False(vm.IsListening);
        Assert.Equal(1, mic.Stopped);

        mic.Hear("N172SP request taxi");
        Assert.Equal("N172SP request taxi", vm.Lines[0].Text);
        Assert.Equal("Pilot", vm.Lines[0].Speaker);
        Assert.Equal("N172SP, taxi to holding point runway 27 via Alpha, Bravo", vm.Lines[1].Text);
    }

    [Fact]
    public void Unrecognised_speech_adds_a_localised_note()
    {
        var (vm, mic, _) = MicFixture();
        vm.BeginTalk();
        vm.EndTalk();
        mic.Mumble();

        Assert.Equal("Could not understand. Say it again.", vm.Lines.Single(l => l.Kind == LogSource.Violation).Text);

        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("Не удалось разобрать. Повторите.", vm.Lines.Single(l => l.Kind == LogSource.Violation).Text);
    }

    [Fact]
    public void Microphone_failure_adds_a_note_and_stops_listening()
    {
        var (vm, mic, _) = MicFixture();
        vm.BeginTalk();
        mic.Break();

        Assert.False(vm.IsListening);
        Assert.Equal("Microphone or recognizer is unavailable.", vm.Lines.Single().Text);
    }

    [Fact]
    public void Talk_is_ignored_when_language_has_no_recognizer_and_double_press_starts_once()
    {
        var (vm, mic, _) = MicFixture();
        vm.BeginTalk();
        vm.BeginTalk();
        Assert.Single(mic.Started);

        mic.Languages.Remove("ru");
        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("Голосовой ввод: нет для этого языка", vm.TalkButtonText);

        vm.BeginTalk();
        Assert.Single(mic.Started);
        Assert.False(vm.IsListening);
    }

    private static (MainViewModel Vm, MockSimDataSource Sim, FakeSpeech Speech) GroundFixture()
    {
        var loc = new LocalizationService();
        var log = new ExchangeLog();
        var sim = new MockSimDataSource();
        var speech = new FakeSpeech();
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, loc.Culture, log);
        var ground = new SimulatedGroundServices(new Dictionary<GroundService, TimeSpan>
        {
            [GroundService.Refuel] = TimeSpan.FromSeconds(10),
            [GroundService.Boarding] = TimeSpan.FromSeconds(10),
            [GroundService.Catering] = TimeSpan.FromSeconds(10),
            [GroundService.Baggage] = TimeSpan.FromSeconds(10),
            [GroundService.Deboarding] = TimeSpan.FromSeconds(10),
        });
        var vm = new MainViewModel(loc, engine, log, sim, null, null, null, speech, null, ground);
        return (vm, sim, speech);
    }

    [Fact]
    public void Ground_service_runs_logs_start_and_finish_and_is_not_spoken()
    {
        var (vm, _, speech) = GroundFixture();
        var refuel = vm.GroundServices[0];
        Assert.Equal("Refuelling", refuel.Name);
        Assert.Equal("Not ordered", refuel.StateText);

        refuel.RequestCommand.Execute(null);
        Assert.Equal("Ground crew", vm.Lines[0].Speaker);
        Assert.Equal("Refuelling started.", vm.Lines[0].Text);

        vm.AdvanceGround(TimeSpan.FromSeconds(5));
        Assert.Equal("50%", refuel.StateText);
        Assert.Equal(50, refuel.ProgressPercent);
        Assert.False(refuel.RequestCommand.CanExecute(null));

        vm.AdvanceGround(TimeSpan.FromSeconds(5));
        Assert.Equal("Done", refuel.StateText);
        Assert.Equal("Refuelling complete.", vm.Lines[^1].Text);
        Assert.True(refuel.RequestCommand.CanExecute(null));
        Assert.Empty(speech.Spoken);
    }

    [Fact]
    public void Ground_texts_and_lines_are_retranslated_on_language_switch()
    {
        var (vm, _, _) = GroundFixture();
        vm.GroundServices[0].RequestCommand.Execute(null);

        vm.ToggleLanguageCommand.Execute(null);

        Assert.Equal("Заправка", vm.GroundServices[0].Name);
        Assert.Equal("Заправка началась.", vm.Lines[0].Text);
        Assert.Equal("Наземная служба", vm.Lines[0].Speaker);
    }

    [Fact]
    public void Ground_service_is_refused_while_aircraft_is_moving_or_airborne()
    {
        var (vm, sim, _) = GroundFixture();
        vm.ConnectCommand.Execute(null);
        sim.Push(new AircraftState(new GeoPoint(55, 37), 0, 270, 15, 0, true, 118100, 121500));

        vm.GroundServices[1].RequestCommand.Execute(null);

        Assert.Equal("Ground services are available only while the aircraft is parked.",
            vm.Lines.Single(l => l.Kind == LogSource.Violation).Text);
        Assert.Equal("Not ordered", vm.GroundServices[1].StateText);
    }

    [Fact]
    public void Deboarding_while_boarding_is_refused_with_a_note()
    {
        var (vm, _, _) = GroundFixture();
        vm.GroundServices[1].RequestCommand.Execute(null);
        vm.GroundServices[4].RequestCommand.Execute(null);

        Assert.Equal("This service cannot be started right now.",
            vm.Lines.Single(l => l.Kind == LogSource.Violation).Text);
        Assert.Equal("Not ordered", vm.GroundServices[4].StateText);
    }

    private static (MainViewModel Vm, MockSimDataSource Sim, SimulatedTraffic Traffic) TrafficFixture()
    {
        var loc = new LocalizationService();
        var log = new ExchangeLog();
        var sim = new MockSimDataSource();
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, loc.Culture, log);
        var traffic = new SimulatedTraffic(MockScenarios.TestAirport.HoldPoint);
        var vm = new MainViewModel(loc, engine, log, sim, null, null, null, null, null, null, traffic);
        return (vm, sim, traffic);
    }

    [Fact]
    public void Traffic_table_shows_airline_type_route_and_formatted_altitude()
    {
        var (vm, _, _) = TrafficFixture();
        Assert.Equal(8, vm.TrafficRows.Count);

        var afl = vm.TrafficRows.Single(r => r.Callsign == "AFL1432");
        Assert.Equal("AFL", afl.Airline);
        Assert.Equal("A320", afl.Aircraft);
        Assert.Equal("UUEE → ULLI", afl.Route);
        Assert.Equal("FL240", afl.Altitude);
        Assert.Equal("430 kt", afl.Speed);
        Assert.Equal("—", afl.Distance);
        Assert.Equal("15000 ft", vm.TrafficRows.Single(r => r.Callsign == "UTA611").Altitude);
        Assert.Equal("GND", vm.TrafficRows.Single(r => r.Callsign == "AFL2127").Altitude);
    }

    [Fact]
    public void Traffic_is_sorted_by_distance_from_own_aircraft_once_known()
    {
        var (vm, sim, _) = TrafficFixture();
        vm.ConnectCommand.Execute(null);
        var hold = MockScenarios.TestAirport.HoldPoint;
        sim.Push(new AircraftState(hold, 0, 270, 0, 0, true, 118100, 121500));
        vm.Tick(TimeSpan.Zero);

        Assert.Equal("AFL2127", vm.TrafficRows[0].Callsign);   // стоит рядом на земле
        Assert.EndsWith(" nm", vm.TrafficRows[0].Distance);
        var distances = vm.TrafficRows.Select(r => double.Parse(r.Distance.Replace(" nm", ""), CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(distances.OrderBy(d => d).ToList(), distances);
    }

    [Fact]
    public void Tick_moves_traffic_and_table_distance_changes()
    {
        var (vm, sim, _) = TrafficFixture();
        vm.ConnectCommand.Execute(null);
        sim.Push(new AircraftState(MockScenarios.TestAirport.HoldPoint, 0, 270, 0, 0, true, 118100, 121500));
        vm.Tick(TimeSpan.Zero);
        var before = vm.TrafficRows.Single(r => r.Callsign == "DLH846").Distance;   // летит на восток, к нам

        vm.Tick(TimeSpan.FromMinutes(5));

        Assert.NotEqual(before, vm.TrafficRows.Single(r => r.Callsign == "DLH846").Distance);
    }

    [Fact]
    public void Tabs_switch_between_radio_and_traffic_views()
    {
        var (vm, _, _) = TrafficFixture();
        Assert.True(vm.IsLogView);
        Assert.False(vm.IsTrafficView);

        vm.ShowTrafficCommand.Execute(null);
        Assert.True(vm.IsTrafficView);
        Assert.False(vm.IsLogView);

        vm.ShowRadioCommand.Execute(null);
        Assert.True(vm.IsLogView);
    }

    [Fact]
    public void Traffic_labels_are_translated()
    {
        var (vm, _, _) = TrafficFixture();
        Assert.Equal("Traffic", vm.LabelTabTraffic);
        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("Трафик", vm.LabelTabTraffic);
        Assert.Equal("Маршрут", vm.LabelColRoute);
    }

    [Fact]
    public void Map_is_empty_until_the_window_reports_its_size()
    {
        var (vm, _, _) = TrafficFixture();
        Assert.Empty(vm.MapMarkers);
        Assert.Empty(vm.MapRings);
    }

    [Fact]
    public void Map_shows_own_aircraft_in_the_centre_and_traffic_with_full_labels()
    {
        var (vm, sim, _) = TrafficFixture();
        vm.ConnectCommand.Execute(null);
        sim.Push(new AircraftState(MockScenarios.TestAirport.HoldPoint, 0, 270, 0, 0, true, 118100, 121500));
        vm.SetMapViewport(800, 600);

        var own = vm.MapMarkers.Single(m => m.Kind == MapMarkerKind.Own);
        Assert.Equal(400, own.X);
        Assert.Equal(300, own.Y);
        Assert.Equal(270, own.HeadingDeg);
        Assert.Equal("You", own.Title);

        var parked = vm.MapMarkers.Single(m => m.Title == "AFL2127");
        Assert.Equal(MapMarkerKind.Ground, parked.Kind);
        Assert.Equal("AFL · SU95 · UUEE→ULLI", parked.Subtitle);

        var uta = vm.MapMarkers.Single(m => m.Title == "UTA611");     // в 30 милях к северу, курс 190
        Assert.Equal(MapMarkerKind.Airborne, uta.Kind);
        Assert.Equal(190, uta.HeadingDeg);
        Assert.True(uta.Y < 300);                                    // севернее: выше центра
    }

    [Fact]
    public void Map_before_first_aircraft_data_is_centred_on_the_airport()
    {
        var (vm, _, _) = TrafficFixture();
        vm.SetMapViewport(800, 600);
        Assert.DoesNotContain(MapMarkerKind.Own, vm.MapMarkers.Select(m => m.Kind));
        var parked = vm.MapMarkers.Single(m => m.Title == "AFL2127");
        Assert.True(Math.Abs(parked.X - 400) < 10 && Math.Abs(parked.Y - 300) < 10);   // 0,2–0,3 мили от центра
    }

    [Fact]
    public void Zoom_changes_range_rings_and_hides_far_traffic()
    {
        var (vm, _, _) = TrafficFixture();
        vm.SetMapViewport(800, 600);
        Assert.Equal("Range: 40 nm", vm.MapRangeText);
        var wide = vm.MapMarkers.Count;
        Assert.Equal("40 nm", vm.MapRings[^1].Label);
        Assert.Equal("20 nm", vm.MapRings[0].Label);

        vm.ZoomInCommand.Execute(null);
        vm.ZoomInCommand.Execute(null);   // 10 миль

        Assert.Equal("Range: 10 nm", vm.MapRangeText);
        Assert.True(vm.MapMarkers.Count < wide);
        Assert.Equal("10 nm", vm.MapRings[^1].Label);

        for (var i = 0; i < 10; i++) vm.ZoomInCommand.Execute(null);   // ниже минимума не уходит
        Assert.Equal("Range: 10 nm", vm.MapRangeText);
        for (var i = 0; i < 10; i++) vm.ZoomOutCommand.Execute(null);
        Assert.Equal("Range: 160 nm", vm.MapRangeText);
    }

    [Fact]
    public void Map_tab_switches_view_and_labels_are_translated()
    {
        var (vm, _, _) = TrafficFixture();
        vm.ShowMapCommand.Execute(null);
        Assert.True(vm.IsMapView);
        Assert.False(vm.IsLogView);
        Assert.False(vm.IsTrafficView);

        vm.ToggleLanguageCommand.Execute(null);
        Assert.Equal("Карта", vm.LabelTabMap);
        Assert.Equal("Дальность: 40 nm", vm.MapRangeText);
    }

    [Fact]
    public void Status_without_demo_source_says_connected_to_simulator()
    {
        var loc = new LocalizationService();
        var log = new ExchangeLog();
        var sim = new MockSimDataSource();
        var engine = new ControllerEngine(new PhraseBook(), MockScenarios.TestAirport, PhraseStyle.Icao, loc.Culture, log);
        var vm = new MainViewModel(loc, engine, log, sim);

        vm.ConnectCommand.Execute(null);

        Assert.Equal("Connected to simulator", vm.StatusText);
    }
}
