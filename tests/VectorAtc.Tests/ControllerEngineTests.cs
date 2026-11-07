using System.Globalization;
using VectorAtc.Core.Controller;
using VectorAtc.Core.Models;
using VectorAtc.Core.Phraseology;
using VectorAtc.Localization;
using VectorAtc.SimLink;
using Xunit;

namespace VectorAtc.Tests;

public class ControllerEngineTests
{
    // Точка ожидания совпадает с последним кадром MockScenarios.ParkedToHoldShort.
    private static readonly AirportInfo Airport = new(
        "TEST", "27", new[] { "A", "B" }, new GeoPoint(55.0010, 37.0018), 25, 118_100);

    private const string GoodReadback =
        "taxi to holding point runway two seven via alpha bravo November one seven two Sierra Papa";

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    }

    private static (ControllerEngine Engine, ExchangeLog Log) Create(
        PhraseStyle style = PhraseStyle.Icao, string culture = "en")
    {
        var log = new ExchangeLog(new FixedTime());
        var engine = new ControllerEngine(new PhraseBook(), Airport, style, CultureInfo.GetCultureInfo(culture), log);
        return (engine, log);
    }

    private static string Join(IEnumerable<string> lines) => string.Join(" | ", lines);

    [Fact]
    public async Task Full_scenario_taxi_request_clearance_readback_hold_short_tower()
    {
        var (engine, _) = Create();

        Assert.Equal("N172SP, taxi to holding point runway 27 via Alpha, Bravo",
            Join(engine.HandlePilotMessage("N172SP request taxi")));
        Assert.Equal(FlightState.TaxiClearanceIssued, engine.State);

        Assert.Equal("N172SP, correct", Join(engine.HandlePilotMessage(GoodReadback)));
        Assert.Equal(FlightState.Taxiing, engine.State);
        Assert.Equal(ControllerPhase.Ground, engine.Phase);

        var sim = new MockSimDataSource(MockScenarios.ParkedToHoldShort());
        await sim.ConnectAsync();
        var transmissions = new List<string>();
        var frames = 0;
        while (sim.Advance())
        {
            frames++;
            transmissions.AddRange(engine.OnAircraftState(sim.Current!));
        }

        Assert.Equal(5, frames);
        Assert.Equal("N172SP, contact tower 118.1", Join(transmissions));
        Assert.Equal(FlightState.TowerContacted, engine.State);
        Assert.Equal(ControllerPhase.Tower, engine.Phase);

        Assert.Equal("N172SP, line up and wait runway 27",
            Join(engine.HandlePilotMessage("N172SP ready for departure")));
        Assert.Equal(FlightState.LinedUp, engine.State);
    }

    [Fact]
    public void Aircraft_still_moving_or_far_from_hold_point_does_not_trigger_handoff()
    {
        var (engine, _) = Create();
        engine.HandlePilotMessage("N172SP request taxi");
        engine.HandlePilotMessage(GoodReadback);
        var frames = MockScenarios.ParkedToHoldShort();

        // кадр 0: стоит, но далеко от точки ожидания; кадр 3: рядом, но ещё едет
        Assert.Equal(0, engine.OnAircraftState(frames[0]).Count);
        Assert.Equal(0, engine.OnAircraftState(frames[3]).Count);
        Assert.Equal(FlightState.Taxiing, engine.State);
    }

    [Fact]
    public void Aircraft_rolling_through_the_hold_point_does_not_trigger_handoff()
    {
        var (engine, _) = Create();
        engine.HandlePilotMessage("N172SP request taxi");
        engine.HandlePilotMessage(GoodReadback);
        var atHoldPoint = MockScenarios.ParkedToHoldShort()[4];
        var rolling = atHoldPoint with { GroundSpeedKnots = 10 };

        Assert.Equal(0, engine.OnAircraftState(rolling).Count);
        Assert.Equal(FlightState.Taxiing, engine.State);
        Assert.Equal(1, engine.OnAircraftState(atHoldPoint).Count);
    }

    [Fact]
    public void Airborne_aircraft_at_hold_point_coordinates_does_not_trigger_handoff()
    {
        var (engine, _) = Create();
        engine.HandlePilotMessage("N172SP request taxi");
        engine.HandlePilotMessage(GoodReadback);
        var flying = MockScenarios.ParkedToHoldShort()[4] with { OnGround = false };

        Assert.Equal(0, engine.OnAircraftState(flying).Count);
    }

    [Fact]
    public void Handoff_is_not_issued_before_a_correct_readback()
    {
        var (engine, _) = Create();
        engine.HandlePilotMessage("N172SP request taxi");

        Assert.Equal(0, engine.OnAircraftState(MockScenarios.ParkedToHoldShort()[4]).Count);
        Assert.Equal(FlightState.TaxiClearanceIssued, engine.State);
    }

    [Fact]
    public void Wrong_runway_in_readback_is_rejected_logged_and_clearance_repeated()
    {
        var (engine, log) = Create();
        engine.HandlePilotMessage("N172SP request taxi");

        var reply = engine.HandlePilotMessage("taxi to holding point runway three six via alpha bravo N172SP");

        Assert.Equal("N172SP, negative, I say again | N172SP, taxi to holding point runway 27 via Alpha, Bravo",
            Join(reply));
        Assert.Equal(FlightState.TaxiClearanceIssued, engine.State);
        var violations = log.Entries.Where(e => e.Source == LogSource.Violation).ToList();
        Assert.Single(violations);
        Assert.Equal("Readback error: Runway", violations[0].Text);
    }

    [Fact]
    public void Missing_route_in_readback_is_an_error_but_correct_runway_is_not()
    {
        var (engine, log) = Create();
        engine.HandlePilotMessage("N172SP request taxi");
        engine.HandlePilotMessage("runway two seven N172SP");

        var violations = log.Entries.Where(e => e.Source == LogSource.Violation).Select(e => e.Text).ToList();
        Assert.Equal("Readback error: Route", string.Join(";", violations));
    }

    [Fact]
    public void Radio_check_is_answered_in_any_state()
    {
        var (engine, _) = Create();
        Assert.Equal("N172SP, readability five", Join(engine.HandlePilotMessage("N172SP radio check")));
        engine.HandlePilotMessage("N172SP request taxi");
        Assert.Equal("N172SP, readability five", Join(engine.HandlePilotMessage("N172SP radio check")));
        Assert.Equal(FlightState.TaxiClearanceIssued, engine.State);
    }

    [Fact]
    public void Request_in_wrong_state_gets_unable_and_state_is_unchanged()
    {
        var (engine, _) = Create();
        Assert.Equal("N172SP, unable", Join(engine.HandlePilotMessage("N172SP ready for departure")));
        Assert.Equal(FlightState.Parked, engine.State);
    }

    [Fact]
    public void Pushback_then_taxi()
    {
        var (engine, _) = Create();
        Assert.Equal("N172SP, pushback approved", Join(engine.HandlePilotMessage("N172SP request pushback")));
        Assert.Equal(FlightState.PushbackApproved, engine.State);
        engine.HandlePilotMessage("N172SP request taxi");
        Assert.Equal(FlightState.TaxiClearanceIssued, engine.State);
    }

    [Fact]
    public void Message_without_any_callsign_before_session_start_gets_station_calling()
    {
        var (engine, _) = Create();
        Assert.Equal("Station calling, say again", Join(engine.HandlePilotMessage("request taxi")));
        Assert.Equal(FlightState.Parked, engine.State);
    }

    [Fact]
    public void Other_callsign_is_not_served()
    {
        var (engine, _) = Create();
        engine.HandlePilotMessage("N172SP request pushback");

        Assert.Equal("Station calling, say again", Join(engine.HandlePilotMessage("UAL123 request taxi")));
        Assert.Equal(FlightState.PushbackApproved, engine.State);
    }

    [Fact]
    public void Say_again_repeats_the_last_controller_transmission()
    {
        var (engine, log) = Create();
        var first = Join(engine.HandlePilotMessage("N172SP request taxi"));

        Assert.Equal(first, Join(engine.HandlePilotMessage("N172SP say again")));
        Assert.Equal(FlightState.TaxiClearanceIssued, engine.State);
        Assert.Equal(2, log.Entries.Count(e => e.Source == LogSource.Controller && e.Text == first));
    }

    [Fact]
    public void Unrecognised_message_from_known_station_gets_say_again_request()
    {
        var (engine, _) = Create();
        engine.HandlePilotMessage("N172SP request pushback");
        Assert.Equal("N172SP, say again", Join(engine.HandlePilotMessage("N172SP hello")));
    }

    [Fact]
    public void Emergency_is_acknowledged_in_any_state()
    {
        var (engine, _) = Create();
        engine.HandlePilotMessage("N172SP request taxi");
        Assert.Equal("N172SP, roger mayday", Join(engine.HandlePilotMessage("Mayday N172SP")));
    }

    [Fact]
    public void Us_style_keeps_its_own_readback_and_mayday_wording()
    {
        var (engine, _) = Create(PhraseStyle.Us);
        engine.HandlePilotMessage("N172SP request taxi");
        Assert.Equal("N172SP, roger mayday, say intentions", Join(engine.HandlePilotMessage("Mayday N172SP")));
    }

    [Fact]
    public void Log_records_pilot_and_controller_in_order_with_provider_time()
    {
        var (engine, log) = Create();
        engine.HandlePilotMessage("N172SP request taxi");

        Assert.Equal(2, log.Entries.Count);
        Assert.Equal(LogSource.Pilot, log.Entries[0].Source);
        Assert.Equal(LogSource.Controller, log.Entries[1].Source);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero), log.Entries[0].Time);
    }

    [Fact]
    public void Us_style_taxi_clearance()
    {
        var (engine, _) = Create(PhraseStyle.Us);
        Assert.Equal("N172SP, runway 27, taxi via Alpha, Bravo", Join(engine.HandlePilotMessage("N172SP request taxi")));
    }

    [Fact]
    public void Russian_flow_with_russian_readback()
    {
        var (engine, _) = Create(culture: "ru");

        Assert.Equal("N172SP, рулите на предварительный старт полосы 27 по Alpha, Bravo",
            Join(engine.HandlePilotMessage("N172SP запрашиваю руление")));
        Assert.Equal("N172SP, повторили правильно",
            Join(engine.HandlePilotMessage("рулю на предварительный старт полосы два семь по альфа браво новембер один семь два сьерра папа")));
        Assert.Equal(FlightState.Taxiing, engine.State);
    }
}
