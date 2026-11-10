using System.Globalization;
using VectorAtc.Core.Speech;

namespace VectorAtc.App.Speech;

/// <summary>Слушает основным распознавателем (Whisper), если для языка он доступен, иначе запасным (Windows).</summary>
public sealed class CompositeSpeechInput : ISpeechInput
{
    private readonly WhisperSpeechInput _primary;
    private readonly ISpeechInput _fallback;
    private ISpeechInput? _active;

    public event EventHandler<string>? Recognized;
    public event EventHandler? NotUnderstood;
    public event EventHandler? Failed;

    public CompositeSpeechInput(WhisperSpeechInput primary, ISpeechInput fallback)
    {
        _primary = primary;
        _fallback = fallback;
        foreach (var input in new ISpeechInput[] { primary, fallback })
        {
            input.Recognized += (_, text) => Recognized?.Invoke(this, text);
            input.NotUnderstood += (_, _) => NotUnderstood?.Invoke(this, EventArgs.Empty);
            input.Failed += (_, _) => Failed?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool CanListen(CultureInfo culture) => _primary.CanListen(culture) || _fallback.CanListen(culture);

    public void StartListening(CultureInfo culture)
    {
        _primary.RememberLanguage(culture);
        _active = _primary.CanListen(culture) ? _primary : _fallback;
        _active.StartListening(culture);
    }

    public void StopListening() => _active?.StopListening();
}
