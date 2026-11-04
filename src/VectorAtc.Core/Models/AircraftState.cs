namespace VectorAtc.Core.Models;

public readonly record struct GeoPoint(double LatitudeDeg, double LongitudeDeg);

/// <summary>Состояние самолёта игрока. Частоты COM в кГц (121.900 МГц = 121900), чтобы поддержать шаг 8.33.</summary>
public sealed record AircraftState(
    GeoPoint Position,
    double AltitudeFeet,
    double HeadingMagneticDeg,
    double GroundSpeedKnots,
    double VerticalSpeedFpm,
    bool OnGround,
    int Com1ActiveKhz,
    int Com2ActiveKhz);
