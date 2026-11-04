using System.Globalization;

namespace VectorAtc.Localization;

/// <summary>Строки интерфейса с переключением языка без перезапуска.</summary>
public interface ILocalizationService
{
    CultureInfo Culture { get; }

    /// <summary>Возвращает строку по ключу. Для неизвестного ключа возвращает "!ключ!".</summary>
    string this[string key] { get; }

    /// <summary>Строка с подстановкой аргументов (string.Format) в текущей культуре.</summary>
    string Format(string key, params object[] args);

    void SetCulture(CultureInfo culture);

    event EventHandler? LanguageChanged;
}
