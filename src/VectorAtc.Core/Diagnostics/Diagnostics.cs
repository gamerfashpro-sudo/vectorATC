namespace VectorAtc.Core.Diagnostics;

public enum DiagnosticsLevel
{
    Info,
    Ok,
    Warn,
    Fail
}

/// <summary>Одна строка диагностики: что проверяли, что получилось и насколько это хорошо.</summary>
public sealed record DiagnosticsItem(string Label, string Value, DiagnosticsLevel Level);

/// <summary>Счётчики живого подключения к симулятору (есть у реального источника, у демо нет).</summary>
public interface ISimDiagnostics
{
    long FramesReceived { get; }

    /// <summary>Когда пришёл последний кадр данных; null, если ещё не приходил.</summary>
    DateTimeOffset? LastFrameAt { get; }

    /// <summary>Код последнего исключения SimConnect, 0 если не было.</summary>
    uint LastExceptionId { get; }
}
