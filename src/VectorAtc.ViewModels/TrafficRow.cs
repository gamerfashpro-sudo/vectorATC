namespace VectorAtc.ViewModels;

/// <summary>Строка таблицы «кто куда летит».</summary>
public sealed record TrafficRow(
    string Callsign, string Airline, string Aircraft, string Route, string Altitude, string Speed, string Distance);
