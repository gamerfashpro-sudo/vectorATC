using System.Text;

namespace VectorAtc.Core.Parsing;

/// <summary>Нормализация реплики: нижний регистр, «ё»→«е», всё кроме букв и цифр становится пробелом.</summary>
internal static class Tokenizer
{
    public static string[] Tokenize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? (ch == 'ё' ? 'е' : ch) : ' ');

        // «x-ray» / «икс-рей» после замены дефиса распались на два слова
        var normalized = " " + string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)) + " ";
        normalized = normalized.Replace(" x ray ", " xray ").Replace(" икс рей ", " иксрей ");
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
