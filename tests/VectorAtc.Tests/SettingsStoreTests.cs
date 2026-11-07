using VectorAtc.Core.Settings;
using Xunit;

namespace VectorAtc.Tests;

public class SettingsStoreTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vatc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Missing_file_gives_empty_settings()
    {
        var s = new JsonSettingsStore(Path.Combine(TempDir(), "settings.json")).Load();
        Assert.Equal(new UserSettings(), s);
    }

    [Fact]
    public void Saved_settings_round_trip_including_missing_folders()
    {
        var path = Path.Combine(TempDir(), "sub", "dir", "settings.json");
        var store = new JsonSettingsStore(path);
        var saved = new UserSettings("ru", "Us", "RA-12345", false, 80);

        Assert.True(store.Save(saved));
        Assert.Equal(saved, new JsonSettingsStore(path).Load());
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Second_save_replaces_the_first()
    {
        var path = Path.Combine(TempDir(), "settings.json");
        var store = new JsonSettingsStore(path);
        store.Save(new UserSettings(Language: "en"));
        store.Save(new UserSettings(Language: "ru"));
        Assert.Equal("ru", store.Load().Language);
    }

    [Fact]
    public void Corrupt_file_gives_empty_settings_instead_of_an_exception()
    {
        var path = Path.Combine(TempDir(), "settings.json");
        File.WriteAllText(path, "{ not json");
        Assert.Equal(new UserSettings(), new JsonSettingsStore(path).Load());
    }

    [Fact]
    public void Unknown_fields_and_other_casing_are_tolerated()
    {
        var path = Path.Combine(TempDir(), "settings.json");
        File.WriteAllText(path, "{ \"language\": \"ru\", \"somethingNew\": 5 }");
        Assert.Equal("ru", new JsonSettingsStore(path).Load().Language);
    }

    [Fact]
    public void Saving_to_an_impossible_place_returns_false()
    {
        var dir = TempDir();
        var blocker = Path.Combine(dir, "file");
        File.WriteAllText(blocker, "x");
        Assert.False(new JsonSettingsStore(Path.Combine(blocker, "settings.json")).Save(new UserSettings()));
    }
}
