using System.Collections;
using System.Globalization;
using System.Resources;
using VectorAtc.Core.Phraseology;
using VectorAtc.Localization;
using Xunit;

namespace VectorAtc.Tests;

public class PhraseBookTests
{
    private static readonly PhraseBook Book = new();
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru");

    private static Dictionary<string, string> Values() => new()
    {
        ["callsign"] = "N172SP", ["runway"] = "27", ["route"] = "Alpha, Bravo", ["frequency"] = "118.1",
    };

    [Fact]
    public void Icao_taxi_clearance_in_english()
    {
        Assert.Equal("N172SP, taxi to holding point runway 27 via Alpha, Bravo",
            Book.Render(PhraseId.TaxiClearance, PhraseStyle.Icao, En, Values()));
    }

    [Fact]
    public void Us_taxi_clearance_in_english()
    {
        Assert.Equal("N172SP, runway 27, taxi via Alpha, Bravo",
            Book.Render(PhraseId.TaxiClearance, PhraseStyle.Us, En, Values()));
    }

    [Fact]
    public void Taxi_clearance_in_russian()
    {
        Assert.Equal("N172SP, рулите на предварительный старт полосы 27 по Alpha, Bravo",
            Book.Render(PhraseId.TaxiClearance, PhraseStyle.Icao, Ru, Values()));
    }

    [Fact]
    public void Missing_placeholder_value_throws()
    {
        var values = new Dictionary<string, string> { ["callsign"] = "N172SP" };
        Assert.Throws<ArgumentException>(() =>
            Book.Render(PhraseId.TaxiClearance, PhraseStyle.Icao, En, values));
    }

    [Fact]
    public void Extra_values_are_ignored()
    {
        var values = new Dictionary<string, string> { ["callsign"] = "N172SP", ["unused"] = "x" };
        Assert.Equal("N172SP, say again",
            Book.Render(PhraseId.SayAgainRequest, PhraseStyle.Icao, En, values));
    }

    [Fact]
    public void Every_phrase_and_style_renders_in_both_languages_without_leftover_placeholders()
    {
        foreach (var culture in new[] { En, Ru })
        foreach (var id in Enum.GetValues<PhraseId>())
        foreach (var style in Enum.GetValues<PhraseStyle>())
        {
            var text = Book.Render(id, style, culture, Values());
            Assert.True(!text.Contains('{') && !text.Contains('}'));
        }
    }

    [Fact]
    public void English_and_russian_phrase_resources_have_identical_keys()
    {
        var rm = new ResourceManager("VectorAtc.Localization.Phrases", typeof(PhraseBook).Assembly);
        static HashSet<string> Keys(ResourceSet set) =>
            set.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToHashSet();

        var en = Keys(rm.GetResourceSet(En, true, true)!);
        var ru = Keys(rm.GetResourceSet(Ru, true, false)!);
        Assert.True(en.SetEquals(ru));
    }
}
