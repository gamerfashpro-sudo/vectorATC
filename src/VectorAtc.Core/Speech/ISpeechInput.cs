using System.Globalization;

namespace VectorAtc.Core.Speech;

/// <summary>Распознавание речи пилота по удержанию кнопки: нажали, сказали, отпустили. Работает без сети.</summary>
public interface ISpeechInput
{
    /// <summary>Есть ли на компьютере распознаватель для этого языка.</summary>
    bool CanListen(CultureInfo culture);

    /// <summary>Начинает слушать микрофон.</summary>
    void StartListening(CultureInfo culture);

    /// <summary>Заканчивает слушать; результат придёт событием <see cref="Recognized"/>, <see cref="NotUnderstood"/> или <see cref="Failed"/>.</summary>
    void StopListening();

    /// <summary>Распознанная фраза целиком (за одно нажатие). Приходит в потоке интерфейса.</summary>
    event EventHandler<string>? Recognized;

    /// <summary>Речь не распознана (тишина или неразборчиво).</summary>
    event EventHandler? NotUnderstood;

    /// <summary>Не удалось включить микрофон или распознаватель.</summary>
    event EventHandler? Failed;
}
