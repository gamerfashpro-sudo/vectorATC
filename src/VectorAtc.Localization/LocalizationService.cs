using System.Globalization;
using System.Reflection;
using System.Resources;

namespace VectorAtc.Localization;

public sealed class LocalizationService : ILocalizationService
{
    public static readonly IReadOnlyList<string> SupportedLanguages = new[] { "en", "ru" };

    private readonly ResourceManager _resources =
        new("VectorAtc.Localization.Strings", typeof(LocalizationService).GetTypeInfo().Assembly);

    private CultureInfo _culture = CultureInfo.GetCultureInfo("en");

    public CultureInfo Culture => _culture;

    public event EventHandler? LanguageChanged;

    public string this[string key]
    {
        get
        {
            var value = _resources.GetString(key, _culture);
            return value ?? $"!{key}!";
        }
    }

    public string Format(string key, params object[] args) =>
        string.Format(_culture, this[key], args);

    public void SetCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var language = culture.TwoLetterISOLanguageName;
        if (!SupportedLanguages.Contains(language))
            throw new ArgumentException($"Unsupported language: {language}", nameof(culture));

        if (_culture.TwoLetterISOLanguageName == language)
            return;

        _culture = CultureInfo.GetCultureInfo(language);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
