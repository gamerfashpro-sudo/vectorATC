using VectorAtc.Core.Models;

namespace VectorAtc.Core.Geo;

public static class Geo
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Расстояние по большому кругу (формула гаверсинусов), метры.</summary>
    public static double DistanceMeters(GeoPoint a, GeoPoint b)
    {
        static double Rad(double deg) => deg * Math.PI / 180.0;

        var dLat = Rad(b.LatitudeDeg - a.LatitudeDeg);
        var dLon = Rad(b.LongitudeDeg - a.LongitudeDeg);
        var h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(Rad(a.LatitudeDeg)) * Math.Cos(Rad(b.LatitudeDeg))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(h));
    }
}
