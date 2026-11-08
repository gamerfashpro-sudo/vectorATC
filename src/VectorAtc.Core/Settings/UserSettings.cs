using System.Text.Json;

namespace VectorAtc.Core.Settings;

/// <summary>Настройки, которые приложение помнит между запусками. null означает «не задано, использовать умолчание».</summary>
public sealed record UserSettings(
    string? Language = null,
    string? Style = null,
    string? Callsign = null,
    bool? VoiceEnabled = null,
    double? MapRangeNm = null,
    string? SimBriefId = null);

public interface ISettingsStore
{
    /// <summary>Читает настройки. Нет файла или он повреждён: возвращает пустые настройки, исключений не бросает.</summary>
    UserSettings Load();

    /// <summary>Сохраняет настройки. false, если записать не удалось (например, папка недоступна); исключений не бросает.</summary>
    bool Save(UserSettings settings);
}

/// <summary>Хранит настройки в JSON-файле. Запись идёт через временный файл, чтобы сбой не оставил половину файла.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;

    public JsonSettingsStore(string path) => _path = path;

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new UserSettings();
            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_path), Options) ?? new UserSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new UserSettings();
        }
    }

    public bool Save(UserSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
