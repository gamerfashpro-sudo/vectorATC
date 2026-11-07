namespace VectorAtc.Core.Controller;

public enum LogSource
{
    Pilot,
    Controller,
    /// <summary>Объявление бортпроводника.</summary>
    Cabin,
    /// <summary>Сообщение наземной службы.</summary>
    Ground,
    /// <summary>Ошибка пилота, например неверный read-back.</summary>
    Violation
}

/// <summary>Code — ключ локализуемого текста (если задан, интерфейс показывает перевод по ключу, а Text служит запасным вариантом).</summary>
public sealed record LogEntry(DateTimeOffset Time, LogSource Source, string Text, string? Code = null);

/// <summary>Журнал радиообмена. Время берётся из TimeProvider, чтобы тесты были детерминированными.</summary>
public sealed class ExchangeLog
{
    private readonly List<LogEntry> _entries = new();
    private readonly TimeProvider _time;

    public ExchangeLog(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    public IReadOnlyList<LogEntry> Entries => _entries;

    public event EventHandler<LogEntry>? EntryAdded;

    public void Add(LogSource source, string text, string? code = null)
    {
        var entry = new LogEntry(_time.GetUtcNow(), source, text, code);
        _entries.Add(entry);
        EntryAdded?.Invoke(this, entry);
    }
}
