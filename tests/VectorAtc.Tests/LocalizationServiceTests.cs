using System.Collections;
using System.Globalization;
using System.Resources;
using VectorAtc.Localization;
using Xunit;

namespace VectorAtc.Tests;

public class LocalizationServiceTests
{
    private static CultureInfo Ru => CultureInfo.GetCultureInfo("ru");

    [Fact]
    public void Default_language_is_english()
    {
        var svc = new LocalizationService();
        Assert.Equal("Connected to simulator", svc["Sim.Connected"]);
    }

    [Fact]
    public void Switching_to_russian_changes_strings_without_restart()
    {
        var svc = new LocalizationService();
        svc.SetCulture(Ru);
        Assert.Equal("Подключено к симулятору", svc["Sim.Connected"]);
    }

    [Fact]
    public void LanguageChanged_fires_only_on_actual_change()
    {
        var svc = new LocalizationService();
        var count = 0;
        svc.LanguageChanged += (_, _) => count++;
        svc.SetCulture(Ru);
        svc.SetCulture(Ru);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Unknown_key_returns_marker_not_exception()
    {
        var svc = new LocalizationService();
        Assert.Equal("!No.Such.Key!", svc["No.Such.Key"]);
    }

    [Fact]
    public void Unsupported_language_is_rejected()
    {
        var svc = new LocalizationService();
        Assert.Throws<ArgumentException>(() => svc.SetCulture(CultureInfo.GetCultureInfo("de")));
    }

    [Fact]
    public void English_and_russian_resources_have_identical_keys()
    {
        var rm = new ResourceManager("VectorAtc.Localization.Strings", typeof(LocalizationService).Assembly);
        static HashSet<string> Keys(ResourceSet set) =>
            set.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToHashSet();

        var en = Keys(rm.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!);
        var ru = Keys(rm.GetResourceSet(Ru, true, false)!);
        Assert.True(en.SetEquals(ru));
    }
}
