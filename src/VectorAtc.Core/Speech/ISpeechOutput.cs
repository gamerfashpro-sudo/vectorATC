using System.Globalization;

namespace VectorAtc.Core.Speech;

/// <summary>Кто говорит: от этого зависит выбор голоса (диспетчер и бортпроводник звучат по-разному, если голосов хватает).</summary>
public enum SpeechRole
{
    Controller,
    Cabin
}

/// <summary>Озвучивание реплик. Реализация работает без сети и без скачиваний.</summary>
public interface ISpeechOutput
{
    /// <summary>Есть ли на компьютере голос для этого языка. Без него реплика не озвучивается (русский текст английским голосом был бы бессмыслицей).</summary>
    bool CanSpeak(CultureInfo culture);

    /// <summary>Ставит реплику в очередь и сразу возвращает управление.</summary>
    void Speak(string text, CultureInfo culture, SpeechRole role);

    /// <summary>Прерывает речь и очищает очередь.</summary>
    void Stop();
}
