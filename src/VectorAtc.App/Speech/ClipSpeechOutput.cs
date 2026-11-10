using System.Globalization;
using VectorAtc.Core.Speech;

namespace VectorAtc.App.Speech;

/// <summary>
/// Озвучивание записанными фразами из папки <c>voices</c> (см. <see cref="ClipLibrary"/>). Реплику, которую нельзя собрать
/// из записей целиком, отдаёт запасному озвучиванию. Код проверен только сборкой, не запуском.
/// </summary>
public sealed class ClipSpeechOutput : ISpeechOutput, IVoiceFiles
{
    private readonly ClipLibrary _library;
    private readonly ISpeechOutput _fallback;
    private readonly WavQueue _queue = new();

    public ClipSpeechOutput(string voicesFolder, ISpeechOutput fallback)
    {
        _library = new ClipLibrary(voicesFolder);
        _fallback = fallback;
    }

    public IReadOnlyList<string> VoiceFiles => (_fallback as IVoiceFiles)?.VoiceFiles ?? Array.Empty<string>();

    public bool CanSpeak(CultureInfo culture) =>
        _library.HasVoice(culture, SpeechRole.Controller) || _fallback.CanSpeak(culture);

    public void Speak(string text, CultureInfo culture, SpeechRole role)
    {
        var plan = _library.Plan(text, culture, role);
        if (plan is null)
            _fallback.Speak(text, culture, role);
        else
            _queue.Enqueue(plan);
    }

    public void Stop()
    {
        _fallback.Stop();
        _queue.Clear();
    }
}
