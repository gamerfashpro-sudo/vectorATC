namespace VectorAtc.Core.Models;

public enum TrafficSource
{
    /// <summary>Создан самим приложением.</summary>
    Simulated,
    /// <summary>Реальный борт из внешних данных (опционально).</summary>
    Live
}

/// <summary>Борт для карты и таблицы рейсов: кто, на чём и откуда куда летит.</summary>
public sealed record TrafficTarget(
    string Callsign,
    string AirlineIcao,
    string AircraftIcao,
    string OriginIcao,
    string DestinationIcao,
    GeoPoint Position,
    double AltitudeFeet,
    double HeadingDeg,
    double GroundSpeedKnots,
    bool OnGround,
    TrafficSource Source);
