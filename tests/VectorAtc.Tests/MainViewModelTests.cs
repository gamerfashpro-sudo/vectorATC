using System.Globalization;
using VectorAtc.Core.Speech;
using VectorAtc.Core.Controller;
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
