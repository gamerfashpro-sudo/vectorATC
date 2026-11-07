using System.Globalization;
using System.Speech.Recognition;
using VectorAtc.Core.Speech;

namespace VectorAtc.App.Speech;

/// <summary>
/// Распознавание встроенным в Windows распознавателем (офлайн, без скачиваний). Качество ниже, чем у отдельных нейросетевых
/// моделей, и зависит от установленного языка и настройки микрофона. Код проверен только сборкой, не запуском.
/// События приходят в потоке, в котором создан объект (поток интерфейса).
/// </summary>
public sealed class WindowsSpeechInput : ISpeechInput
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;
    private SpeechRecognitionEngine? _engine;
    private readonly List<string> _heard = new();

    public event EventHandler<string>? Recognized;
    public event EventHandler? NotUnderstood;
    public event EventHandler? Failed;

    public bool CanListen(CultureInfo culture) => Find(culture) is not null;

    public void StartListening(CultureInfo culture)
    {
        if (_engine is not null)
            return;

        try
        {
            var info = Find(culture);
            if (info is null)
            {
                Post(() => Failed?.Invoke(this, EventArgs.Empty));
                return;
            }

            _heard.Clear();
            var engine = new SpeechRecognitionEngine(info);
            engine.LoadGrammar(new DictationGrammar());
            engine.SetInputToDefaultAudioDevice();
            engine.SpeechRecognized += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Result.Text))
                    lock (_heard) _heard.Add(e.Result.Text);
            };
            engine.RecognizeCompleted += (_, _) => Finish();
            _engine = engine;
            engine.RecognizeAsync(RecognizeMode.Multiple);
        }
        catch (Exception)
        {
            Release();
            Post(() => Failed?.Invoke(this, EventArgs.Empty));
        }
    }

    public void StopListening()
    {
        try
        {
            _engine?.RecognizeAsyncStop();
        }
        catch (Exception)
        {
            Release();
            Post(() => Failed?.Invoke(this, EventArgs.Empty));
        }
    }

    private void Finish()
    {
        string text;
        lock (_heard) text = string.Join(" ", _heard).Trim();
        Release();
        Post(() =>
        {
            if (text.Length == 0) NotUnderstood?.Invoke(this, EventArgs.Empty);
            else Recognized?.Invoke(this, text);
        });
    }

    private void Release()
    {
        var engine = _engine;
        _engine = null;
        try
        {
            engine?.Dispose();
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

    private static RecognizerInfo? Find(CultureInfo culture)
    {
        try
        {
            return SpeechRecognitionEngine.InstalledRecognizers()
                .FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
