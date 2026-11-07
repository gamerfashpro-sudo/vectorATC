using VectorAtc.Core.Geo;
using VectorAtc.Core.Models;
using Xunit;

namespace VectorAtc.Tests;

public class MapProjectionTests
{
    private static readonly GeoPoint Center = new(55.0, 37.0);

    private static bool Near(double a, double b, double eps = 0.01) => Math.Abs(a - b) < eps;

    [Fact]
    public void Center_maps_to_the_middle_of_the_window()
    {
        var p = new MapProjection(Center, 40, 800, 600).Project(Center);
        Assert.True(Near(p.X, 400));
        Assert.True(Near(p.Y, 300));
    }

    [Fact]
    public void Range_reaches_the_edge_of_the_shorter_side()
    {
        var m = new MapProjection(Center, 40, 800, 600);   // меньшая сторона 600: радиус 300 пикселей на 40 миль
        Assert.True(Near(m.PixelsPerNm, 7.5));
        var north = m.Project(new GeoPoint(55.0 + 40.0 / 60.0, 37.0));
        Assert.True(Near(north.X, 400));
        Assert.True(Near(north.Y, 0));
    }

    [Fact]
    public void North_is_up_and_east_is_right()
    {
        var m = new MapProjection(Center, 40, 800, 600);
        Assert.True(m.Project(new GeoPoint(55.1, 37.0)).Y < 300);
        Assert.True(m.Project(new GeoPoint(54.9, 37.0)).Y > 300);
        Assert.True(m.Project(new GeoPoint(55.0, 37.1)).X > 400);
        Assert.True(m.Project(new GeoPoint(55.0, 36.9)).X < 400);
    }

    [Fact]
    public void East_distance_uses_cosine_of_latitude()
    {
        var m = new MapProjection(Center, 40, 800, 600);
        var p = m.Project(new GeoPoint(55.0, 37.0 + 1.0));          // 1 градус долготы на 55° ≈ 34,4 мили
        var expectedNm = 60.0 * Math.Cos(55.0 * Math.PI / 180.0);
        Assert.True(Near(p.X - 400, expectedNm * 7.5, 0.05));
    }

    [Fact]
    public void Projection_distance_matches_real_distance_for_small_offsets()
    {
        var m = new MapProjection(Center, 40, 800, 600);
        var other = new GeoPoint(55.2, 37.3);
        var p = m.Project(other);
        var pixels = Math.Sqrt((p.X - 400) * (p.X - 400) + (p.Y - 300) * (p.Y - 300));
        var realNm = Geo.DistanceMeters(Center, other) / 1852.0;
        Assert.True(Math.Abs(pixels / m.PixelsPerNm - realNm) < 0.1);
    }

    [Fact]
    public void Visibility_respects_margin_and_zero_size_does_not_crash()
    {
        var m = new MapProjection(Center, 40, 800, 600);
        Assert.True(m.IsVisible((10, 10)));
        Assert.False(m.IsVisible((-5, 10)));
        Assert.True(m.IsVisible((-5, 10), margin: 10));

        var empty = new MapProjection(Center, 40, 0, 0);
        var p = empty.Project(new GeoPoint(56, 38));
        Assert.True(Near(p.X, 0) && Near(p.Y, 0));
    }
}
