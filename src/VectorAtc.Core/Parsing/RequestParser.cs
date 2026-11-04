using System.Text;
using System.Text.RegularExpressions;

namespace VectorAtc.Core.Parsing;

/// <summary>
/// Шаблонный разбор реплики пилота (текст или результат распознавания): позывной и намерение.
/// Без языковой модели: только словари из <see cref="Vocabulary"/>.
/// </summary>
public sealed class RequestParser
{
    // Печатный позывной вида N172SP, UAL123, TBM9: 3–7 латинских букв/цифр, есть и буква, и цифра.
    private static readonly Regex TypedCallsign =
        new("^(?=.*[0-9])(?=.*[a-z])[a-z0-9]{3,7}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public PilotRequest Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = Tokenize(text);
        return new PilotRequest(ExtractCallsign(tokens), ClassifyIntent(tokens), text);
    }

    private static string[] Tokenize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? (ch == 'ё' ? 'е' : ch) : ' ');

        // «x-ray» / «икс-рей» после замены дефиса распались на два слова
        var normalized = " " + string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)) + " ";
        normalized = normalized.Replace(" x ray ", " xray ").Replace(" икс рей ", " иксрей ");
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string? ExtractCallsign(string[] tokens)
    {
        var run = new StringBuilder();

        string? Flush()
        {
            var candidate = run.ToString();
            run.Clear();
            return candidate.Length >= 3 && candidate.Any(char.IsLetter) && candidate.Any(char.IsDigit)
                ? candidate
                : null;
        }

        foreach (var token in tokens)
        {
            var piece = CallsignPiece(token);
            if (piece is not null)
            {
                run.Append(piece);
                continue;
            }

            var found = Flush();
            if (found is not null) return found;
        }

        return Flush();
    }

    private static string? CallsignPiece(string token)
    {
        if (Vocabulary.Digits.TryGetValue(token, out var digit)) return digit.ToString();
        if (Vocabulary.Letters.TryGetValue(token, out var letter)) return letter.ToString();
        if (token.All(char.IsAsciiDigit)) return token;
        if (TypedCallsign.IsMatch(token)) return token.ToUpperInvariant();
        return null;
    }

    private static IntentType ClassifyIntent(string[] tokens)
    {
        foreach (var (intent, phrases) in Vocabulary.Intents)
            foreach (var phrase in phrases)
                if (ContainsPhrase(tokens, phrase.Split(' ')))
                    return intent;

        return IntentType.Unknown;
    }

    private static bool ContainsPhrase(string[] tokens, string[] phrase)
    {
        for (var start = 0; start + phrase.Length <= tokens.Length; start++)
        {
            var all = true;
            for (var k = 0; k < phrase.Length && all; k++)
                all = Matches(tokens[start + k], phrase[k]);
            if (all) return true;
        }

        return false;
    }

    private static bool Matches(string token, string pattern) =>
        pattern.EndsWith('*')
            ? token.StartsWith(pattern[..^1], StringComparison.Ordinal)
            : token == pattern;
}
