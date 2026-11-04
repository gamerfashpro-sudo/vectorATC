using System.Globalization;

namespace VectorAtc.Core.Phraseology;

/// <summary>ICAO — международная фразеология, Us — американская.</summary>
public enum PhraseStyle
{
    Icao,
    Us
}

public enum PhraseId
{
    RadioCheckReply,
    TaxiClearance,
    PushbackApproved,
    SayAgainRequest
}

/// <summary>Шаблоны ответов диспетчера. Подстановки в тексте шаблона: {callsign}, {runway}, {route}.</summary>
public interface IPhraseBook
{
    /// <exception cref="ArgumentException">Для шаблона не передано значение подстановки.</exception>
    string Render(PhraseId id, PhraseStyle style, CultureInfo culture, IReadOnlyDictionary<string, string> values);
}
