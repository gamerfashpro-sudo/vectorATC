using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using VectorAtc.Core.Phraseology;

namespace VectorAtc.Localization;

/// <summary>Шаблоны фраз из ресурсов Phrases(.ru).resx. Ключ ресурса: «Id.Style», например «TaxiClearance.Icao».</summary>
public sealed class PhraseBook : IPhraseBook
{
    private static readonly Regex Placeholder = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private readonly ResourceManager _resources =
        new("VectorAtc.Localization.Phrases", typeof(PhraseBook).GetTypeInfo().Assembly);

    public string Render(PhraseId id, PhraseStyle style, CultureInfo culture, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(values);

        var key = $"{id}.{style}";
        var template = _resources.GetString(key, culture)
            ?? throw new KeyNotFoundException($"Phrase template not found: {key}");

        return Placeholder.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            return values.TryGetValue(name, out var value)
                ? value
                : throw new ArgumentException($"Missing value '{name}' for phrase {key}.", nameof(values));
        });
    }
}
