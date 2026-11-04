namespace VectorAtc.Core.Parsing;

/// <summary>
/// Словари распознавания (не тексты интерфейса, поэтому не в ресурсах). Русский и английский
/// объединены: пилоты смешивают языки, а распознавание часто даёт английские слова в русской речи.
/// Фраза в списке намерений: слова через пробел; слово с «*» на конце сопоставляется по началу слова.
/// </summary>
internal static class Vocabulary
{
    public static readonly IReadOnlyDictionary<string, char> Digits = new Dictionary<string, char>
    {
        ["zero"] = '0', ["one"] = '1', ["two"] = '2', ["three"] = '3', ["tree"] = '3', ["four"] = '4',
        ["fower"] = '4', ["five"] = '5', ["fife"] = '5', ["six"] = '6', ["seven"] = '7', ["eight"] = '8',
        ["nine"] = '9', ["niner"] = '9',
        ["ноль"] = '0', ["нуль"] = '0', ["один"] = '1', ["два"] = '2', ["три"] = '3', ["четыре"] = '4',
        ["пять"] = '5', ["шесть"] = '6', ["семь"] = '7', ["восемь"] = '8', ["девять"] = '9',
    };

    public static readonly IReadOnlyDictionary<string, char> Letters = new Dictionary<string, char>
    {
        ["alpha"] = 'A', ["alfa"] = 'A', ["bravo"] = 'B', ["charlie"] = 'C', ["delta"] = 'D', ["echo"] = 'E',
        ["foxtrot"] = 'F', ["golf"] = 'G', ["hotel"] = 'H', ["india"] = 'I', ["juliet"] = 'J', ["juliett"] = 'J',
        ["kilo"] = 'K', ["lima"] = 'L', ["mike"] = 'M', ["november"] = 'N', ["oscar"] = 'O', ["papa"] = 'P',
        ["quebec"] = 'Q', ["romeo"] = 'R', ["sierra"] = 'S', ["tango"] = 'T', ["uniform"] = 'U', ["victor"] = 'V',
        ["whiskey"] = 'W', ["whisky"] = 'W', ["xray"] = 'X', ["yankee"] = 'Y', ["zulu"] = 'Z',
        ["альфа"] = 'A', ["браво"] = 'B', ["чарли"] = 'C', ["дельта"] = 'D', ["эхо"] = 'E', ["фокстрот"] = 'F',
        ["гольф"] = 'G', ["отель"] = 'H', ["индия"] = 'I', ["джульетта"] = 'J', ["кило"] = 'K', ["лима"] = 'L',
        ["майк"] = 'M', ["новембер"] = 'N', ["оскар"] = 'O', ["папа"] = 'P', ["квебек"] = 'Q', ["ромео"] = 'R',
        ["сьерра"] = 'S', ["сиерра"] = 'S', ["танго"] = 'T', ["юниформ"] = 'U', ["виктор"] = 'V', ["виски"] = 'W',
        ["иксрей"] = 'X', ["янки"] = 'Y', ["зулу"] = 'Z',
    };

    /// <summary>Намерения в порядке приоритета: срочные выше обычных.</summary>
    public static readonly IReadOnlyList<(IntentType Intent, string[] Phrases)> Intents = new (IntentType, string[])[]
    {
        (IntentType.Emergency, new[] { "mayday", "pan pan", "майдей", "мейдей", "мэйдэй", "пан пан" }),
        (IntentType.SayAgain, new[] { "say again", "repeat", "повтор*" }),
        (IntentType.RadioCheck, new[] { "radio check", "проверка связи", "как слышите" }),
        (IntentType.RequestPushback, new[] { "pushback", "push back", "пушбэк", "пушбек", "буксировк*" }),
        (IntentType.RequestTaxi, new[] { "taxi", "рул*" }),
        (IntentType.ReadyForDeparture, new[]
        {
            "ready for departure", "ready for takeoff", "ready for take off", "ready to depart",
            "готов* к взлет*", "готов* к вылет*",
        }),
    };

    private static readonly string[] Nato =
    {
        "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliett", "Kilo", "Lima", "Mike",
        "November", "Oscar", "Papa", "Quebec", "Romeo", "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "Xray", "Yankee", "Zulu",
    };

    /// <summary>Название рулёжной дорожки для передачи: «A» → «Alpha»; прочие имена возвращаются как есть.</summary>
    public static string LetterName(string taxiway) =>
        taxiway.Length == 1 && char.IsAsciiLetter(taxiway[0]) ? Nato[char.ToUpperInvariant(taxiway[0]) - 'A'] : taxiway;
}
