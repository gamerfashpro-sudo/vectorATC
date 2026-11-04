namespace VectorAtc.Core.Parsing;

/// <summary>Что пилот хочет. Порядок разбора (приоритет) задан в <see cref="Vocabulary"/>.</summary>
public enum IntentType
{
    Unknown,
    Emergency,
    SayAgain,
    RadioCheck,
    RequestPushback,
    RequestTaxi,
    ReadyForDeparture
}

/// <summary>Результат разбора реплики пилота. Callsign нормализован (верхний регистр), null если не найден.</summary>
public sealed record PilotRequest(string? Callsign, IntentType Intent, string RawText);
