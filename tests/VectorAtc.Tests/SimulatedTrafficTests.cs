using VectorAtc.Core.Geo;
using VectorAtc.Core.Models;
using VectorAtc.Core.Traffic;
using Xunit;

namespace VectorAtc.Tests;

public class SimulatedTrafficTests
{
    private static readonly GeoPoint Center = new(55.0, 37.0);

    private static TrafficTarget Get(SimulatedTraffic t, string callsign) => t.Targets.Single(x => x.Callsign == callsign);

    [Fact]
    public void Targets_have_full_flight_info_and_are_marked_simulated()
    {
        var t = new SimulatedTraffic(Center);
        Assert.Equal(8, t.Targets.Count);
        Assert.True(t.Targets.All(x => x.Source == TrafficSource.Simulated));
        Assert.True(t.Targets.All(x => x.AirlineIcao.Length == 3 && x.OriginIcao.Length == 4 && x.DestinationIcao.Length == 4));
        Assert.True(t.Targets.All(x => x.AircraftIcao.Length >= 3));
    }

    [Fact]
    public void Flying_aircraft_moves_the_right_distance_along_its_heading()
    {
        var t = new SimulatedTraffic(Center);
        var before = Get(t, "SBI312");   // курс 200, 450 узлов
        t.Advance(TimeSpan.FromMinutes(1));
        var after = Get(t, "SBI312");

        var movedNm = Geo.DistanceMeters(before.Position, after.Position) / 1852.0;
        Assert.True(Math.Abs(movedNm - 7.5) < 0.1);            // 450 уз за минуту = 7,5 миль
        Assert.True(after.Position.LatitudeDeg < before.Position.LatitudeDeg);   // курс 200: на юг
        Assert.True(after.Position.LongitudeDeg < before.Position.LongitudeDeg); // и на запад
    }

    [Fact]
    public void Aircraft_on_the_ground_stays_put()
    {
        var t = new SimulatedTraffic(Center);
        var before = Get(t, "AFL2127");
        t.Advance(TimeSpan.FromHours(1));
        Assert.Equal(before.Position, Get(t, "AFL2127").Position);
        Assert.True(before.OnGround);
    }

    [Fact]
    public void Aircraft_leaving_the_area_returns_to_its_start_point()
    {
        var t = new SimulatedTraffic(Center, radiusNm: 60);
        var start = Get(t, "UAE132").Position;
        t.Advance(TimeSpan.FromHours(1));   // 500 миль: давно вне радиуса
        var after = Get(t, "UAE132");
        Assert.Equal(start, after.Position);
    }

    [Fact]
    public void Zero_or_negative_time_changes_nothing()
    {
        var t = new SimulatedTraffic(Center);
        var before = t.Targets.ToList();
        t.Advance(TimeSpan.Zero);
        t.Advance(TimeSpan.FromSeconds(-5));
        Assert.Equal(before.Select(x => x.Position).ToArray(), t.Targets.Select(x => x.Position).ToArray());
    }
}
