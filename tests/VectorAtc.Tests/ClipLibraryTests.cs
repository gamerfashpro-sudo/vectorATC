using System.Globalization;
using VectorAtc.Core.Speech;
using Xunit;

namespace VectorAtc.Tests;

public class ClipLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vatc-clips-" + Guid.NewGuid().ToString("N"));
    private static readonly CultureInfo Ru = new("ru-RU");

    private string Voice(SpeechRole role) => Path.Combine(_root, "ru", role.ToString().ToLowerInvariant());

    private void Write(SpeechRole role, string manifestJson, params string[] files)
    {
        Directory.CreateDirectory(Voice(role));
        foreach (var f in files)
            File.WriteAllText(Path.Combine(Voice(role), f), "x");
        File.WriteAllText(Path.Combine(Voice(role), "manifest.json"), manifestJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Fact]
    public void Missing_folder_means_no_voice()
    {
        var lib = new ClipLibrary(_root);
        Assert.False(lib.HasVoice(Ru, SpeechRole.Controller));
        Assert.Null(lib.Plan("ноябрь", Ru, SpeechRole.Controller));
    }

    [Fact]
    public void Longest_phrase_wins_and_short_clips_fill_the_rest()
    {
        Write(SpeechRole.Controller,
            "{\"phrases\":{\"ноябрь\":\"a.mp3\",\"один\":\"b.mp3\",\"рулите на предварительный старт\":\"c.mp3\",\"рулите\":\"d.mp3\"}}",
            "a.mp3", "b.mp3", "c.mp3", "d.mp3");
        var lib = new ClipLibrary(_root);

        var plan = lib.Plan("Ноябрь один, рулите на предварительный старт.", Ru, SpeechRole.Controller);

        Assert.NotNull(plan);
        Assert.Equal(new[] { "a.mp3", "b.mp3", "c.mp3" }, plan!.Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Uncovered_word_gives_no_plan()
    {
        Write(SpeechRole.Controller, "{\"phrases\":{\"ноябрь\":\"a.mp3\"}}", "a.mp3");
        var lib = new ClipLibrary(_root);

        Assert.Null(lib.Plan("ноябрь семь", Ru, SpeechRole.Controller));
    }

    [Fact]
    public void Yo_matches_ye_and_case_is_ignored()
    {
        Write(SpeechRole.Cabin, "{\"phrases\":{\"самолёт\":\"a.mp3\"}}", "a.mp3");
        var lib = new ClipLibrary(_root);

        Assert.NotNull(lib.Plan("САМОЛЕТ", Ru, SpeechRole.Cabin));
    }

    [Fact]
    public void Roles_are_separate_voices()
    {
        Write(SpeechRole.Controller, "{\"phrases\":{\"ноябрь\":\"a.mp3\"}}", "a.mp3");
        var lib = new ClipLibrary(_root);

        Assert.True(lib.HasVoice(Ru, SpeechRole.Controller));
        Assert.False(lib.HasVoice(Ru, SpeechRole.Pilot));
    }

    [Fact]
    public void Entry_without_file_or_outside_folder_is_ignored_and_broken_manifest_is_no_voice()
    {
        Write(SpeechRole.Controller, "{\"phrases\":{\"ноябрь\":\"нет.mp3\",\"один\":\"../x.mp3\"}}");
        var lib = new ClipLibrary(_root);
        Assert.False(lib.HasVoice(Ru, SpeechRole.Controller));

        Write(SpeechRole.Pilot, "{ это не json");
        Assert.False(lib.HasVoice(Ru, SpeechRole.Pilot));
    }
}
