namespace VectorAtc.Core.FlightPlan;

/// <summary>Нужное приложению из плана полёта SimBrief. Пустая строка значит «в плане не указано».</summary>
public sealed record FlightPlanInfo(
    string Callsign,
    string AirlineIcao,
    string FlightNumber,
    string OriginIcao,
    string OriginRunway,
    string DestinationIcao,
    string DestinationRunway,
    string AircraftIcao,
    string Registration,
    string Route,
    int? Passengers);

public enum FlightPlanError
{
    None,

    /// <summary>SimBrief вернул ошибку: неверный ID или имя, либо у пользователя нет плана.</summary>
    NotFound,

    /// <summary>Нет связи или сервер недоступен.</summary>
    Network,

    /// <summary>Ответ получен, но в нём нет плана, который можно разобрать.</summary>
    BadResponse
}

public sealed record FlightPlanResult(FlightPlanInfo? Plan, FlightPlanError Error)
{
    public bool IsSuccess => Plan is not null;

    public static FlightPlanResult Ok(FlightPlanInfo plan) => new(plan, FlightPlanError.None);

    public static FlightPlanResult Fail(FlightPlanError error) => new(null, error);
}
