using System.Text;
using System.Text.RegularExpressions;

namespace VectorAtc.Core.Speech;

/// <summary>
/// Приводит русскую реплику к тому виду, в котором её произносят: цифры словами по одной, латинские буквы позывного
/// названиями фонетического алфавита, «Alpha» как «Альфа». Нужна записанным голосам: клипы лежат по словам.
/// Написания русских названий букв и «десятичная» для частоты взяты из моих допущений и с практикой пока не сверены.
/// </summary>
public static class RussianSpokenForm
{
    private static readonly string[] Digits =
        { "ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };

    private static readonly string[] Letters =
    {
        "Альфа", "Браво", "Чарли", "Дельта", "Эко", "Фокстрот", "Гольф", "Отель", "Индия", "Джульетт", "Кило", "Лима", "Майк",
        "Новембер", "Оскар", "Папа", "Кебек", "Ромео", "Сьерра", "Танго", "Юниформ", "Виктор", "Виски", "Икс-рей", "Янки", "Зулу"
    };

    private static readonly string[] EnglishNames =
    {
        "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliett", "Kilo", "Lima", "Mike",
        "November", "Oscar", "Papa", "Quebec", "Romeo", "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "Xray", "Yankee", "Zulu"
    };

    private static readonly Regex Token = new(@"[A-Za-z0-9]+(?:\.[0-9]+)?", RegexOptions.Compiled);

    public static string Normalize(string text) => Token.Replace(text, m => Spell(m.Value));

    private static string Spell(string token)
    {
        var english = Array.FindIndex(EnglishNames, n => string.Equals(n, token, StringComparison.OrdinalIgnoreCase));
        if (english >= 0)
            return Letters[english];

        var sb = new StringBuilder();
        for (var i = 0; i < token.Length; i++)
        {
            var ch = token[i];
            string word;
            if (ch == '.')
                word = "десятичная";
            else if (char.IsAsciiDigit(ch))
                word = Digits[ch - '0'];
            else if (i == token.Length - 1 && i > 0 && char.IsAsciiDigit(token[i - 1]) && "LRC".Contains(char.ToUpperInvariant(ch)))
                word = char.ToUpperInvariant(ch) switch { 'L' => "левая", 'R' => "правая", _ => "центральная" };
            else
                word = Letters[char.ToUpperInvariant(ch) - 'A'];
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(word);
        }
        return sb.ToString();
    }
}
