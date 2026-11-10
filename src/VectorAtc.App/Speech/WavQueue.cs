using System.IO;
using System.Windows;
using System.Windows.Media;

namespace VectorAtc.App.Speech;

/// <summary>Очередь звуковых файлов: проигрывает по одному подряд. Работает из любого потока (переходит в поток окна).</summary>
public sealed class WavQueue
{
    private readonly Queue<string> _queue = new();
    private readonly HashSet<string> _deleteAfter = new();
    private MediaPlayer? _player;
    private bool _playing;
    private string? _current;

    public void Enqueue(IEnumerable<string> files, bool deleteAfterPlay = false) => OnUi(() =>
    {
        foreach (var f in files)
        {
            _queue.Enqueue(f);
            if (deleteAfterPlay)
                _deleteAfter.Add(f);
        }
        if (!_playing)
            PlayNext();
    });

    public void Clear() => OnUi(() =>
    {
        foreach (var f in _queue)
            Cleanup(f);
        _queue.Clear();
        _playing = false;
        _player?.Stop();
        if (_current is not null)
            Cleanup(_current);
        _current = null;
    });

    private void PlayNext()
    {
        if (_current is not null)
            Cleanup(_current);
        _current = null;
        if (_queue.Count == 0)
        {
            _playing = false;
            return;
        }
        _playing = true;
        _current = _queue.Dequeue();
        try
        {
            _player ??= CreatePlayer();
            _player.Open(new Uri(_current, UriKind.Absolute));
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

    private void Cleanup(string file)
    {
        if (!_deleteAfter.Remove(file))
            return;
        try
        {
            File.Delete(file);
        }
        catch (Exception)
        {
        }
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
