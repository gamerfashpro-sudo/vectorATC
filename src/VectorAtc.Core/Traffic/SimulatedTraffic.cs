using VectorAtc.Core.Models;

namespace VectorAtc.Core.Traffic;

/// <summary>
/// Демонстрационный трафик вокруг заданной точки: борты летят прямо по курсу с постоянной скоростью (счисление пути).
/// Рейсы, позывные и маршруты вымышленные и нужны, чтобы показать таблицу и карту; это не данные симулятора и не реальные рейсы.
/// Борт, вылетевший за пределы <see cref="RadiusNm"/>, возвращается в начальную точку.
/// </summary>
public sealed class SimulatedTraffic : ITrafficSource
{
    private const double MetersPerNm = 1852.0;

    private sealed class Flight
    {
        public required TrafficTarget Start { get; init; }
        public required TrafficTarget Now { get; set; }
    }

    private static readonly (string Cs, string Al, string Ac, string From, string To, double North, double East, double Alt, double Hdg, double Gs)[] Template =
    {
        ("AFL1432", "AFL", "A320", "UUEE", "ULLI", 20, -40, 24000, 320, 430),
        ("SBI312", "SBI", "A321", "UUDD", "URSS", 45, 25, 32000, 200, 450),
        ("DLH846", "DLH", "A333", "EDDF", "UUEE", -30, -60, 37000, 80, 480),
        ("UTA611", "UTA", "B738", "UUEE", "UUOB", 30, 5, 15000, 190, 390),
        ("THY418", "THY", "A321", "LTFM", "UUEE", -35, -10, 12000, 15, 340),
        ("SDM6361", "SDM", "B738", "UUEE", "LEMD", 5, 50, 28000, 270, 440),
        ("UAE132", "UAE", "B77W", "OMDB", "UUEE", -55, 35, 39000, 330, 500),
        ("AFL2127", "AFL", "SU95", "UUEE", "ULLI", 0.3, 0.2, 0, 0, 0),
    };

    private readonly List<Flight> _flights = new();
    private readonly GeoPoint _center;

    public SimulatedTraffic(GeoPoint center, double radiusNm = 120)
    {
        _center = center;
        RadiusNm = radiusNm;
        foreach (var t in Template)
        {
            var position = Offset(center, t.North, t.East);
            var target = new TrafficTarget(t.Cs, t.Al, t.Ac, t.From, t.To, position, t.Alt, t.Hdg, t.Gs,
                OnGround: t.Gs == 0, TrafficSource.Simulated);
            _flights.Add(new Flight { Start = target, Now = target });
        }
    }

    public double RadiusNm { get; }

    public IReadOnlyList<TrafficTarget> Targets => _flights.Select(f => f.Now).ToList();

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero)
            return;

        foreach (var flight in _flights)
        {
            var t = flight.Now;
            if (t.OnGround || t.GroundSpeedKnots <= 0)
                continue;

            var distanceNm = t.GroundSpeedKnots * elapsed.TotalHours;
            var heading = t.HeadingDeg * Math.PI / 180.0;
            var position = Offset(t.Position, distanceNm * Math.Cos(heading), distanceNm * Math.Sin(heading));

            flight.Now = Geo.Geo.DistanceMeters(_center, position) / MetersPerNm > RadiusNm
                ? flight.Start
                : t with { Position = position };
        }
    }

    /// <summary>Смещение точки на север/восток в морских милях (плоское приближение, годится для десятков миль).</summary>
    private static GeoPoint Offset(GeoPoint from, double northNm, double eastNm)
    {
        var lat = from.LatitudeDeg + northNm / 60.0;
        var lon = from.LongitudeDeg + eastNm / (60.0 * Math.Cos(from.LatitudeDeg * Math.PI / 180.0));
        return new GeoPoint(lat, lon);
    }
}
