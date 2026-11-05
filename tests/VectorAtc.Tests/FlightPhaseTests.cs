using VectorAtc.Core.Flight;
using VectorAtc.Core.Models;
using Xunit;

namespace VectorAtc.Tests;

public class FlightPhaseTests
{
    private static AircraftState S(bool ground, double gs, double alt = 0, double vs = 0) =>
        new(new GeoPoint(55, 37), alt, 270, gs, vs, ground, 118100, 121500);

    [Fact]
    public void Ground_phases_follow_ground_speed()
    {
        var d = new FlightPhaseDetector();
        Assert.Equal(FlightPhase.Parked, d.Update(S(true, 0)));
        Assert.Equal(FlightPhase.Taxi, d.Update(S(true, 15)));
        Assert.Equal(FlightPhase.TakeoffRoll, d.Update(S(true, 80)));
        Assert.Equal(FlightPhase.Taxi, d.Update(S(true, 20)));
    }

    [Fact]
    public void Full_flight_goes_through_every_phase()
    {
        var d = new FlightPhaseDetector();
        d.Update(S(true, 0));
        d.Update(S(true, 80));
        Assert.Equal(FlightPhase.Climb, d.Update(S(false, 140, 500, 0)));
        Assert.Equal(FlightPhase.Climb, d.Update(S(false, 250, 9000, 1800)));
        Assert.Equal(FlightPhase.Cruise, d.Update(S(false, 450, 35000, 0)));
        Assert.Equal(FlightPhase.Descent, d.Update(S(false, 400, 30000, -1800)));
        Assert.Equal(FlightPhase.LandingRoll, d.Update(S(true, 120, 0, 0)));
        Assert.Equal(FlightPhase.Taxi, d.Update(S(true, 20)));
        Assert.Equal(FlightPhase.Parked, d.Update(S(true, 0)));
    }

    [Fact]
    public void Level_off_below_cruise_altitude_stays_climb()
    {
        var d = new FlightPhaseDetector();
        d.Update(S(false, 200, 5000, 1500));
        Assert.Equal(FlightPhase.Climb, d.Update(S(false, 220, 5000, 0)));
    }

    [Fact]
    public void Go_around_from_descent_becomes_climb()
    {
        var d = new FlightPhaseDetector();
        d.Update(S(false, 150, 3000, -700));
        Assert.Equal(FlightPhase.Descent, d.Phase);
        Assert.Equal(FlightPhase.Climb, d.Update(S(false, 150, 2500, 1500)));
    }

    [Fact]
    public void First_sample_in_air_is_classified_by_vertical_speed_and_altitude()
    {
        Assert.Equal(FlightPhase.Cruise, new FlightPhaseDetector().Update(S(false, 450, 36000, 0)));
        Assert.Equal(FlightPhase.Descent, new FlightPhaseDetector().Update(S(false, 300, 20000, -1500)));
        Assert.Equal(FlightPhase.Climb, new FlightPhaseDetector().Update(S(false, 200, 4000, 2000)));
    }

    private static List<CabinAnnouncement> Run(CabinAnnouncer a, params AircraftState[] frames)
    {
        var all = new List<CabinAnnouncement>();
        foreach (var f in frames) all.AddRange(a.Update(f));
        return all;
    }

    [Fact]
    public void Announcements_follow_the_flight_once_each_in_order()
    {
        var a = new CabinAnnouncer();
        var got = Run(a,
            S(true, 0), S(true, 15), S(true, 0), S(true, 15),   // руление с остановкой на предварительном старте
            S(true, 80), S(false, 140, 500, 2000),
            S(false, 450, 35000, 0), S(false, 450, 35000, 0),
            S(false, 400, 30000, -1800), S(false, 250, 12000, -1500), S(false, 200, 3500, -800), S(false, 160, 2000, -700),
            S(true, 120), S(true, 20), S(true, 0));

        Assert.Equal(new[]
        {
            CabinAnnouncement.SafetyBriefing, CabinAnnouncement.PrepareForTakeoff, CabinAnnouncement.CruiseInfo,
            CabinAnnouncement.DescentPreparation, CabinAnnouncement.LandingPreparation,
            CabinAnnouncement.Landed, CabinAnnouncement.Deplane
        }, got.ToArray());
    }

    [Fact]
    public void Aborted_takeoff_repeats_nothing_and_second_attempt_is_not_announced_again()
    {
        var a = new CabinAnnouncer();
        var got = Run(a, S(true, 15), S(true, 80), S(true, 20), S(true, 80));
        Assert.Equal(new[] { CabinAnnouncement.SafetyBriefing, CabinAnnouncement.PrepareForTakeoff }, got.ToArray());
    }

    [Fact]
    public void Landing_preparation_waits_for_the_configured_altitude()
    {
        var a = new CabinAnnouncer { LandingPreparationAltitudeFeet = 4000 };
        var high = Run(a, S(false, 300, 15000, -1500));
        Assert.DoesNotContain(CabinAnnouncement.LandingPreparation, high);
    }

    [Fact]
    public void Flight_state_resets_after_deplane_so_next_flight_is_announced()
    {
        var a = new CabinAnnouncer();
        var frames = new[]
        {
            S(true, 15), S(true, 80), S(false, 140, 500, 2000), S(false, 450, 35000, 0), S(false, 400, 20000, -1800),
            S(false, 160, 2000, -700), S(true, 120), S(true, 20), S(true, 0)
        };
        Run(a, frames);
        var second = Run(a, frames);
        Assert.Contains(CabinAnnouncement.SafetyBriefing, second);
        Assert.Contains(CabinAnnouncement.Deplane, second);
    }
}
