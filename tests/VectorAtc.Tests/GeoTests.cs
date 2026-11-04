using VectorAtc.Core.Geo;
using VectorAtc.Core.Models;
using Xunit;

namespace VectorAtc.Tests;

public class GeoTests
{
    [Fact]
    public void Distance_to_same_point_is_zero()
    {
        var p = new GeoPoint(55.0, 37.0);
        Assert.Equal(0.0, Geo.DistanceMeters(p, p));
    }

    [Fact]
    public void One_degree_of_latitude_is_about_111_km()
    {
        var d = Geo.DistanceMeters(new GeoPoint(55.0, 37.0), new GeoPoint(56.0, 37.0));
        Assert.True(Math.Abs(d - 111_195) < 100);
    }

    [Fact]
    public void Distance_is_symmetric()
    {
        var a = new GeoPoint(55.0, 37.0);
        var b = new GeoPoint(55.3, 37.9);
        Assert.Equal(Geo.DistanceMeters(a, b), Geo.DistanceMeters(b, a));
    }
}
