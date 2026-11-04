using VectorAtc.Core.Models;

namespace VectorAtc.Core.Abstractions;

public enum SimConnectionState
{
    Disconnected,
    Connecting,
    Connected
}

/// <summary>
/// Источник данных симулятора. Логика диспетчера зависит только от этого интерфейса,
/// поэтому её можно тестировать на моке без запущенного симулятора.
/// </summary>
public interface ISimDataSource
{
    SimConnectionState ConnectionState { get; }

    /// <summary>Последнее известное состояние. null, пока нет подключения или данных.</summary>
    AircraftState? Current { get; }

    event EventHandler<SimConnectionState>? ConnectionStateChanged;
    event EventHandler<AircraftState>? AircraftStateUpdated;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
