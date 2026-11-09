using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VectorAtc.Core.Speech;

/// <summary>
/// Библиотека записанных фраз. Лежит в папке <c>voices/&lt;язык&gt;/&lt;роль&gt;/</c> рядом с программой; в ней файл
/// <c>manifest.json</c> вида <c>{"phrases": {"ноябрь": "noyabr.mp3", "рулите на предварительный старт": "rt_hold.mp3"}}</c>.
/// Реплика собирается из фраз манифеста: берётся самое длинное совпадение слов, поэтому целые стандартные фразы
/// звучат одной записью, а позывной, цифры и рулёжки собираются из коротких клипов. Если хоть одно слово реплики
/// не покрыто записью, план не строится (возвращается null), и решает вызывающий код.
/// </summary>
public sealed class ClipLibrary
{
    private const string ManifestName = "manifest.json";
    private readonly string _root;

    public ClipLibrary(string root) => _root = root;

    /// <summary>Есть ли хотя бы одна запись для языка и роли.</summary>
    public bool HasVoice(CultureInfo culture, SpeechRole role) => Load(culture, role) is not null;

    /// <summary>Пути к файлам по порядку воспроизведения или null, если реплику нельзя собрать целиком из записей.</summary>
    public IReadOnlyList<string>? Plan(string text, CultureInfo culture, SpeechRole role)
    {
        var manifest = Load(culture, role);
        if (manifest is null)
            return null;
        var words = Words(text);
        if (words.Count == 0)
            return null;

        var files = new List<string>();
        var i = 0;
        while (i < words.Count)
        {
            var matched = false;
            for (var len = Math.Min(manifest.MaxWords, words.Count - i); len >= 1; len--)
            {
                var key = string.Join(' ', words.Skip(i).Take(len));
                if (manifest.Files.TryGetValue(key, out var file))
                {
                    files.Add(file);
                    i += len;
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return null;
        }
        return files;
    }

    /// <summary>Слова реплики в нижнем регистре без знаков препинания; «ё» приравнивается к «е».</summary>
    public static IReadOnlyList<string> Words(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? (ch == 'ё' ? 'е' : ch) : ' ');
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed record Manifest(Dictionary<string, string> Files, int MaxWords);

    private Manifest? Load(CultureInfo culture, SpeechRole role)
    {
        try
        {
            var dir = Path.Combine(_root, culture.TwoLetterISOLanguageName, role.ToString().ToLowerInvariant());
            var path = Path.Combine(dir, ManifestName);
            if (!File.Exists(path))
                return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("phrases", out var phrases) || phrases.ValueKind != JsonValueKind.Object)
                return null;

            var files = new Dictionary<string, string>();
            var max = 1;
            foreach (var p in phrases.EnumerateObject())
            {
                var key = string.Join(' ', Words(p.Name));
                var name = p.Value.GetString();
                if (key.Length == 0 || string.IsNullOrWhiteSpace(name))
                    continue;
                var full = Path.GetFullPath(Path.Combine(dir, name));
                // файл обязан лежать внутри папки голоса и существовать: иначе запись считается отсутствующей
                if (!full.StartsWith(Path.GetFullPath(dir) + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(full))
                    continue;
                files[key] = full;
                max = Math.Max(max, key.Count(c => c == ' ') + 1);
            }
            return files.Count == 0 ? null : new Manifest(files, max);
        }
        catch (Exception)
        {
            return null; // битый манифест = голоса нет, приложение продолжает работать
        }
    }
}
