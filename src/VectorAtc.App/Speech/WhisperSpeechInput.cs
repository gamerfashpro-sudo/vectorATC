using System.Globalization;
using System.IO;
using NAudio.Wave;
using VectorAtc.Core.Speech;
using Whisper.net;

namespace VectorAtc.App.Speech;

/// <summary>
/// Распознавание речи нейросетью Whisper (через whisper.cpp): офлайн, русский и английский. Модель лежит в
/// <c>%LOCALAPPDATA%\VectorATC\whisper\ggml-small.bin</c> (скачивается скриптом установки). Микрофон пишется, пока кнопка
/// зажата; после отпускания запись распознаётся целиком. Проверено только сборкой, на настоящем Windows не запускалось.
/// </summary>
public sealed class WhisperSpeechInput : ISpeechInput, IVoiceFiles, IDisposable
{
    private const int SampleRate = 16000;
    private const string RussianHint =
        "Диспетчер, Земля. Рулите на предварительный старт полосы два семь по Альфа, Браво. Запрашиваю руление. Проверка связи. Повторите. Запрашиваю буксировку.";
    private const string EnglishHint =
        "Ground, request taxi. Taxi to holding point runway two seven via Alpha, Bravo. Radio check. Say again. Request pushback.";

    private readonly string _modelPath;
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private readonly object _gate = new();
    private WaveInEvent? _mic;
    private MemoryStream? _pcm;
    private WhisperFactory? _factory;

    public event EventHandler<string>? Recognized;
    public event EventHandler? NotUnderstood;
    public event EventHandler? Failed;

    public WhisperSpeechInput(string modelPath) => _modelPath = modelPath;

    public IReadOnlyList<string> VoiceFiles => new[] { _modelPath };

    public bool CanListen(CultureInfo culture) => File.Exists(_modelPath);

    public void StartListening(CultureInfo culture)
    {
        lock (_gate)
        {
            if (_mic is not null)
                return;
            try
            {
                _pcm = new MemoryStream();
                var mic = new WaveInEvent { WaveFormat = new WaveFormat(SampleRate, 16, 1), BufferMilliseconds = 100 };
                mic.DataAvailable += (_, e) =>
                {
                    lock (_gate)
                        _pcm?.Write(e.Buffer, 0, e.BytesRecorded);
                };
                mic.StartRecording();
                _mic = mic;
            }
            catch (Exception)
            {
                Release();
                Post(() => Failed?.Invoke(this, EventArgs.Empty));
            }
        }
    }

    public void StopListening()
    {
        byte[] pcm;
        lock (_gate)
        {
            if (_mic is null)
                return;
            try
            {
                _mic.StopRecording();
            }
            catch (Exception)
            {
            }
            pcm = _pcm?.ToArray() ?? Array.Empty<byte>();
            Release();
        }

        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        _ = Task.Run(() => Transcribe(pcm, _lastLanguage ?? language));
    }

    private string? _lastLanguage;

    /// <summary>Язык запоминается при начале записи: после отпускания кнопки язык интерфейса мог смениться.</summary>
    public void RememberLanguage(CultureInfo culture) => _lastLanguage = culture.TwoLetterISOLanguageName;

    private async Task Transcribe(byte[] pcm, string language)
    {
        try
        {
            // меньше 0,4 с или почти тишина: Whisper на тишине придумывает фразы, поэтому такую запись не распознаём
            if (pcm.Length < SampleRate * 2 * 0.4 || Peak(pcm) < 600)
            {
                Post(() => NotUnderstood?.Invoke(this, EventArgs.Empty));
                return;
            }

            _factory ??= WhisperFactory.FromPath(_modelPath);
            using var processor = _factory.CreateBuilder()
                .WithLanguage(language == "ru" ? "ru" : "en")
                .WithPrompt(language == "ru" ? RussianHint : EnglishHint)
                .Build();

            using var wav = new MemoryStream();
            using (var writer = new WaveFileWriter(wav, new WaveFormat(SampleRate, 16, 1)))
            {
                writer.Write(pcm, 0, pcm.Length);
            }
            wav.Position = 0;

            var parts = new List<string>();
            await foreach (var segment in processor.ProcessAsync(wav))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                    parts.Add(segment.Text.Trim());
            }

            var text = string.Join(" ", parts).Trim();
            if (text.Length == 0)
                Post(() => NotUnderstood?.Invoke(this, EventArgs.Empty));
            else
                Post(() => Recognized?.Invoke(this, text));
        }
        catch (Exception)
        {
            Post(() => Failed?.Invoke(this, EventArgs.Empty));
        }
    }

    private static int Peak(byte[] pcm)
    {
        var peak = 0;
        for (var i = 0; i + 1 < pcm.Length; i += 2)
            peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm, i)));
        return peak;
    }

    private void Release()
    {
        var mic = _mic;
        _mic = null;
        _pcm = null;
        try
        {
            mic?.Dispose();
        }
        catch (Exception)
        {
        }
    }

    private void Post(Action action)
    {
        if (_context is null) action();
        else _context.Post(_ => action(), null);
    }

    public void Dispose()
    {
        Release();
        _factory?.Dispose();
    }
}
