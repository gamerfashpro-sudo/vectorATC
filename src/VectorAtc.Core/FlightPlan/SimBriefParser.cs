using System.Globalization;
using System.Text.Json;

namespace VectorAtc.Core.FlightPlan;

/// <summary>
/// Разбор ответа SimBrief (формат json=v2). Имена полей совпали в двух описаниях (типы SDK симулятора и Go-библиотека), настоящий
/// ответ в этой среде получить не удалось, поэтому разбор терпим: поля могут отсутствовать, числа приходят строками, а пустые значения
/// как пустой объект {}. Всё неизвестное читается как пустая строка.
/// </summary>
public static class SimBriefParser
{
    public static FlightPlanResult Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return FlightPlanResult.Fail(FlightPlanError.BadResponse);

            var status = Text(root, "fetch", "status");
            if (status.Length > 0 && !status.Equals("Success", StringComparison.OrdinalIgnoreCase))
                return FlightPlanResult.Fail(FlightPlanError.NotFound);

            var origin = Text(root, "origin", "icao_code");
            var destination = Text(root, "destination", "icao_code");
            if (origin.Length == 0 && destination.Length == 0)
                return FlightPlanResult.Fail(FlightPlanError.BadResponse);

            var pax = Text(root, "weights", "pax_count");
            return FlightPlanResult.Ok(new FlightPlanInfo(
                Callsign: Text(root, "atc", "callsign"),
                AirlineIcao: Text(root, "general", "icao_airline"),
                FlightNumber: Text(root, "general", "flight_number"),
                OriginIcao: origin,
                OriginRunway: Text(root, "origin", "plan_rwy"),
                DestinationIcao: destination,
                DestinationRunway: Text(root, "destination", "plan_rwy"),
                AircraftIcao: Text(root, "aircraft", "icaocode"),
                Registration: Text(root, "aircraft", "reg"),
                Route: Text(root, "general", "route"),
                Passengers: int.TryParse(pax, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? count : null));
        }
        catch (JsonException)
        {
            return FlightPlanResult.Fail(FlightPlanError.BadResponse);
        }
    }

    /// <summary>Значение по пути; строка и число читаются как текст, всё остальное (в том числе {}) как пустая строка.</summary>
    private static string Text(JsonElement element, params string[] path)
    {
        foreach (var name in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
                return "";
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()?.Trim() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            _ => "",
        };
    }
}
