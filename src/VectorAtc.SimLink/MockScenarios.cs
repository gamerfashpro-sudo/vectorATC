using VectorAtc.Core.Models;

namespace VectorAtc.SimLink;

/// <summary>Готовые сценарии для мока. Координаты условные (тестовые), не реального аэропорта.</summary>
public static class MockScenarios
{
    private const int Ground = 121_900; // кГц
    private const int Tower = 118_100;  // кГц

    /// <summary>Стоянка → руление → остановка у ВПП. 5 кадров.</summary>
    public static IReadOnlyList<AircraftState> ParkedToHoldShort() => new[]
    {
        Frame(55.0000, 37.0000, 0,   0,  true, Ground),
        Frame(55.0003, 37.0000, 0,   8,  true, Ground),
        Frame(55.0010, 37.0004, 90, 15,  true, Ground),
        Frame(55.0010, 37.0012, 90, 12,  true, Ground),
        Frame(55.0010, 37.0018, 90,  0,  true, Ground),
    };

    private static AircraftState Frame(double lat, double lon, double hdg, double gs, bool onGround, int com1) =>
        new(new GeoPoint(lat, lon), AltitudeFeet: 400, HeadingMagneticDeg: hdg, GroundSpeedKnots: gs,
            VerticalSpeedFpm: 0, OnGround: onGround, Com1ActiveKhz: com1, Com2ActiveKhz: Tower);
}
