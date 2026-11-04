using System.Text;
using VectorAtc.Core.Parsing;

namespace VectorAtc.Core.Controller;

public enum ReadbackError
{
    Callsign,
    Runway,
    Route
}

public sealed record ReadbackResult(IReadOnlyList<ReadbackError> Errors)
{
    public bool IsCorrect => Errors.Count == 0;
}

/// <summary>
/// Проверка повторения клиренса на руление. Требуется: свой позывной, номер полосы после слова «runway»/«полоса»,
/// рулёжные дорожки по порядку сразу после «via»/«по». Суффикс полосы (L/R/C) пока не проверяется.
/// </summary>
public static class ReadbackChecker
{
    public static ReadbackResult Check(string pilotText, string callsign, AirportInfo airport)
    {
        ArgumentNullException.ThrowIfNull(pilotText);
        var tokens = Tokenizer.Tokenize(pilotText);
        var errors = new List<ReadbackError>();

        if (!Mentions(pilotText, callsign))
            errors.Add(ReadbackError.Callsign);

        var expectedRunway = new string(airport.ActiveRunway.TakeWhile(char.IsDigit).ToArray());
        if (ReadRunwayDigits(tokens) != expectedRunway)
            errors.Add(ReadbackError.Runway);

        var expectedRoute = string.Concat(airport.TaxiRoute.Select(r => r.ToUpperInvariant()));
        if (!ReadRoute(tokens).StartsWith(expectedRoute, StringComparison.Ordinal))
            errors.Add(ReadbackError.Route);

        return new ReadbackResult(errors);
    }

    /// <summary>
    /// Упоминается ли позывной в реплике. Сравнение по вхождению в последовательность букв и цифр,
    /// потому что соседние слова («alpha bravo November one…») склеиваются с позывным в одну серию.
    /// </summary>
    internal static bool Mentions(string text, string callsign) =>
        RequestParser.CallsignRuns(Tokenizer.Tokenize(text))
            .Any(run => run.Contains(callsign, StringComparison.OrdinalIgnoreCase));

    private static string ReadRunwayDigits(string[] tokens)
    {
        var i = Array.FindIndex(tokens, t => t == "runway" || t.StartsWith("полос", StringComparison.Ordinal));
        if (i < 0) return "";

        var digits = new StringBuilder();
        for (var j = i + 1; j < tokens.Length; j++)
        {
            if (Vocabulary.Digits.TryGetValue(tokens[j], out var d)) digits.Append(d);
            else if (tokens[j].All(char.IsAsciiDigit)) digits.Append(tokens[j]);
            else break;
        }

        return digits.ToString();
    }

    private static string ReadRoute(string[] tokens)
    {
        var i = Array.FindIndex(tokens, t => t == "via" || t == "по");
        if (i < 0) return "";

        var letters = new StringBuilder();
        for (var j = i + 1; j < tokens.Length; j++)
        {
            var t = tokens[j];
            if (t is "рд" or "taxiway") continue;
            if (Vocabulary.Letters.TryGetValue(t, out var l)) letters.Append(l);
            else if (t.Length == 1 && char.IsAsciiLetter(t[0])) letters.Append(char.ToUpperInvariant(t[0]));
            else break;
        }

        return letters.ToString();
    }
}
