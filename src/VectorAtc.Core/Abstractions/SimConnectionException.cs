namespace VectorAtc.Core.Abstractions;

public enum SimConnectionFailure
{
    /// <summary>Не найдена (или не подошла по разрядности) библиотека SimConnect.dll.</summary>
    LibraryNotFound,

    /// <summary>Библиотека есть, но подключиться не удалось: симулятор не запущен или отказал в соединении.</summary>
    OpenFailed,

    /// <summary>Подключились, но симулятор отклонил описание или запрос данных.</summary>
    RequestFailed
}

/// <summary>Ошибка подключения к симулятору. Вид ошибки (Kind) интерфейс переводит на язык пользователя.</summary>
public sealed class SimConnectionException : Exception
{
    public SimConnectionException(SimConnectionFailure kind, string message, Exception? inner = null)
        : base(message, inner) => Kind = kind;

    public SimConnectionFailure Kind { get; }
}
