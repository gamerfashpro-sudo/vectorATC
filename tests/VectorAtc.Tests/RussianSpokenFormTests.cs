using System.Globalization;
using VectorAtc.Core.Speech;
using Xunit;

namespace VectorAtc.Tests;

public class RussianSpokenFormTests
{
    [Fact]
    public void Callsign_is_spelled_by_letters_and_digits()
    {
        Assert.Equal("Новембер один семь два Сьерра Папа, рулите", RussianSpokenForm.Normalize("N172SP, рулите"));
    }

    [Fact]
    public void Runway_and_route_are_spoken()
    {
        Assert.Equal("полосы два семь по Альфа, Браво", RussianSpokenForm.Normalize("полосы 27 по Alpha, Bravo"));
        Assert.Equal("два семь левая", RussianSpokenForm.Normalize("27L"));
    }

    [Fact]
    public void Frequency_is_spoken_digit_by_digit()
    {
        Assert.Equal("один один восемь десятичная один", RussianSpokenForm.Normalize("118.1"));
    }

    [Fact]
    public void Russian_text_without_latin_or_digits_is_unchanged()
    {
        Assert.Equal("повторите", RussianSpokenForm.Normalize("повторите"));
    }

    [Fact]
    public void Library_plans_clips_for_normalized_text()
    {
        var root = Path.Combine(Path.GetTempPath(), "vatc-sf-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "ru", "controller");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.mp3"), "x");
            File.WriteAllText(Path.Combine(dir, "b.mp3"), "x");
            File.WriteAllText(Path.Combine(dir, "manifest.json"), "{\"phrases\":{\"два\":\"a.mp3\",\"семь\":\"b.mp3\"}}");

            var plan = new ClipLibrary(root).Plan("27", new CultureInfo("ru-RU"), SpeechRole.Controller);

            Assert.NotNull(plan);
            Assert.Equal(new[] { "a.mp3", "b.mp3" }, plan!.Select(Path.GetFileName).ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
