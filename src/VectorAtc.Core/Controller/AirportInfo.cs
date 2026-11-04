using VectorAtc.Core.Models;

namespace VectorAtc.Core.Controller;

/// <summary>
/// Данные аэропорта для диспетчера (этап 1: один аэропорт, маршрут задан вручную).
/// TaxiRoute: одиночные буквы рулёжных дорожек по порядку. Названия вида «A1» пока не поддерживаются проверкой read-back.
/// </summary>
public sealed record AirportInfo(
    string Icao,
    string ActiveRunway,
    IReadOnlyList<string> TaxiRoute,
    GeoPoint HoldPoint,
    double HoldRadiusMeters,
    int TowerFrequencyKhz);
