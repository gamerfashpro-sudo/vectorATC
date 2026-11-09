using System.Globalization;
using System.Speech.Synthesis;
using VectorAtc.Core.Speech;

namespace VectorAtc.App.Speech;

/// <summary>
/// Озвучивание встроенными голосами Windows (SAPI): офлайн, ничего скачивать не нужно. Какие языки доступны, зависит от
/// установленных в системе голосов: русский голос есть не на каждом компьютере. Код проверен только сборкой, не запуском.
/// </summary>
public sealed class WindowsSpeechOutput : ISpeechOutput, IDisposable
{
    private readonly SpeechSynthesizer _synth = new();

    public bool CanSpeak(CultureInfo culture) => FindVoice(culture, SpeechRole.Controller) is not null;

    public void Speak(string text, CultureInfo culture, SpeechRole role)
    {
        if (string.IsNullOrWhiteSpace(text) || role == SpeechRole.Pilot)
            return; // ответы игрока читаются только записанными фразами
        var voice = FindVoice(culture, role);
        if (voice is null)
            return;

        try
        {
            var builder = new PromptBuilder(culture);
            builder.StartVoice(voice);
            builder.AppendText(text);
            builder.EndVoice();
            _synth.SpeakAsync(builder);
        }
        catch (Exception)
        {
            // озвучивание необязательно: сбой голоса не должен ронять приложение
        }
    }

    public void Stop()
    {
        try
        {
            _synth.SpeakAsyncCancelAll();
        }
        catch (Exception)
        {
        }
    }

    public void Dispose() => _synth.Dispose();

    /// <summary>Подбирает голос языка: диспетчеру мужской, бортпроводнику женский, если есть; иначе любой доступный.</summary>
    private string? FindVoice(CultureInfo culture, SpeechRole role)
    {
        try
        {
            var voices = _synth.GetInstalledVoices()
                .Where(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName)
                .Select(v => v.VoiceInfo)
                .ToList();
            if (voices.Count == 0)
                return null;

            var wanted = role == SpeechRole.Cabin ? VoiceGender.Female : VoiceGender.Male;
            return (voices.FirstOrDefault(v => v.Gender == wanted) ?? voices[0]).Name;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
