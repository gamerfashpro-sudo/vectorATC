using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using VectorAtc.Core.Speech;

namespace VectorAtc.App.Speech;

/// <summary>
/// Озвучивание записанными фразами из папки <c>voices</c> (см. <see cref="ClipLibrary"/>). Реплику, которую нельзя собрать
/// из записей целиком, читает запасной голос Windows (кроме ответов игрока: их запасной голос не читает).
/// Код проверен только сборкой, не запуском: воспроизведение на настоящем Windows ещё не пробовалось.
/// </summary>
public sealed class ClipSpeechOutput : ISpeechOutput
{
    private readonly ClipLibrary _library;
    private readonly ISpeechOutput _fallback;
    private readonly Queue<string> _queue = new();
    private MediaPlayer? _player;
    private bool _playing;

    public ClipSpeechOutput(string voicesFolder, ISpeechOutput fallback)
    {
        _library = new ClipLibrary(voicesFolder);
        _fallback = fallback;
    }

    public bool CanSpeak(CultureInfo culture) =>
        _library.HasVoice(culture, SpeechRole.Controller) || _fallback.CanSpeak(culture);

    public void Speak(string text, CultureInfo culture, SpeechRole role)
    {
        var plan = _library.Plan(text, culture, role);
        if (plan is null)
        {
            _fallback.Speak(text, culture, role);
            return;
        }
        OnUi(() =>
        {
            foreach (var file in plan)
                _queue.Enqueue(file);
            if (!_playing)
                PlayNext();
        });
    }

    public void Stop()
    {
        _fallback.Stop();
        OnUi(() =>
        {
            _queue.Clear();
            _playing = false;
            _player?.Stop();
        });
    }

    private void PlayNext()
    {
        if (_queue.Count == 0)
        {
            _playing = false;
            return;
        }
        _playing = true;
        try
        {
            _player ??= CreatePlayer();
            _player.Open(new Uri(_queue.Dequeue(), UriKind.Absolute));
            _player.Play();
        }
        catch (Exception)
        {
            PlayNext(); // сбой одного файла не должен останавливать остальную речь
        }
    }

    private MediaPlayer CreatePlayer()
    {
        var player = new MediaPlayer();
        player.MediaEnded += (_, _) => PlayNext();
        player.MediaFailed += (_, _) => PlayNext();
        return player;
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }
}
