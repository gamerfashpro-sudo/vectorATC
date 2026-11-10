using System.Diagnostics;
using System.Globalization;
using System.IO;
using VectorAtc.Core.Speech;

namespace VectorAtc.App.Speech;

/// <summary>
/// Русская озвучка голосами Piper: офлайн, отдельным процессом piper.exe (папка <c>piper</c>: piper.exe и модели .onnx).
/// Реплики синтезируются строго по очереди, чтобы порядок речи не нарушался. Если piper.exe или модели нет, реплику
/// отдаёт запасному озвучиванию (кроме ответов игрока: их запасной голос не читает). Проверено только сборкой.
/// </summary>
public sealed class PiperSpeechOutput : ISpeechOutput, IVoiceFiles
{
    private readonly string _folder;
    private readonly ISpeechOutput _fallback;
    private readonly WavQueue _queue = new();
    private readonly object _gate = new();
    private Task _chain = Task.CompletedTask;
    private int _generation;

    public PiperSpeechOutput(string piperFolder, ISpeechOutput fallback)
    {
        _folder = piperFolder;
        _fallback = fallback;
    }

    public IReadOnlyList<string> VoiceFiles => new[]
    {
        Exe,
        Path.Combine(Voices, PiperVoices.ModelName(SpeechRole.Controller) + ".onnx"),
        Path.Combine(Voices, PiperVoices.ModelName(SpeechRole.Cabin) + ".onnx"),
        Path.Combine(Voices, PiperVoices.ModelName(SpeechRole.Pilot) + ".onnx")
    };

    private string Exe => Path.Combine(_folder, "piper.exe");
    private string Voices => Path.Combine(_folder, "voices");

    public bool CanSpeak(CultureInfo culture) =>
        (IsRussian(culture) && File.Exists(Exe) && PiperVoices.FindModel(Voices, SpeechRole.Controller) is not null)
        || _fallback.CanSpeak(culture);

    public void Speak(string text, CultureInfo culture, SpeechRole role)
    {
        var model = IsRussian(culture) && File.Exists(Exe) ? PiperVoices.FindModel(Voices, role) : null;
        if (model is null)
        {
            _fallback.Speak(text, culture, role);
            return;
        }

        var spoken = RussianSpokenForm.Normalize(text);
        int generation;
        lock (_gate)
        {
            generation = _generation;
            _chain = _chain.ContinueWith(_ => Synthesize(spoken, model, generation), TaskScheduler.Default);
        }
    }

    public void Stop()
    {
        lock (_gate)
            _generation++; // уже поставленные в очередь синтезы после этого отбрасываются
        _fallback.Stop();
        _queue.Clear();
    }

    private void Synthesize(string text, string model, int generation)
    {
        try
        {
            if (generation != Volatile.Read(ref _generation))
                return;
            var wav = Path.Combine(Path.GetTempPath(), "vatc-" + Guid.NewGuid().ToString("N") + ".wav");
            var info = new ProcessStartInfo(Exe, PiperVoices.BuildArguments(model, wav))
            {
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _folder
            };
            using var process = Process.Start(info);
            if (process is null)
                return;
            process.StandardInput.WriteLine(text);
            process.StandardInput.Close();
            if (!process.WaitForExit(20000))
            {
                process.Kill(true);
                return;
            }
            if (File.Exists(wav) && generation == Volatile.Read(ref _generation))
                _queue.Enqueue(new[] { wav }, deleteAfterPlay: true);
            else if (File.Exists(wav))
                File.Delete(wav);
        }
        catch (Exception)
        {
            // озвучивание необязательно: сбой Piper не должен ронять приложение
        }
    }

    private static bool IsRussian(CultureInfo culture) => culture.TwoLetterISOLanguageName == "ru";
}
