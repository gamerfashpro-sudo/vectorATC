using VectorAtc.Core.Abstractions;
using VectorAtc.Core.Models;

namespace VectorAtc.SimLink;

/// <summary>
/// Мок симулятора для тестов и режима без симулятора. Без таймеров: кадры подаются вручную
/// через <see cref="Advance"/> или <see cref="Push"/>, поэтому тесты детерминированы.
/// </summary>
public sealed class MockSimDataSource : ISimDataSource
{
    private readonly Queue<AircraftState> _script;

    public MockSimDataSource(IEnumerable<AircraftState>? script = null)
    {
        _script = new Queue<AircraftState>(script ?? Enumerable.Empty<AircraftState>());
    }

    public SimConnectionState ConnectionState { get; private set; } = SimConnectionState.Disconnected;

    public AircraftState? Current { get; private set; }

    public event EventHandler<SimConnectionState>? ConnectionStateChanged;
    public event EventHandler<AircraftState>? AircraftStateUpdated;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetConnectionState(SimConnectionState.Connecting);
        SetConnectionState(SimConnectionState.Connected);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        Current = null; // устаревшие данные после отключения вводят в заблуждение
        SetConnectionState(SimConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    /// <summary>Подаёт следующий кадр сценария. false, если нет подключения или кадры кончились.</summary>
    public bool Advance()
    {
        if (ConnectionState != SimConnectionState.Connected || _script.Count == 0)
            return false;

        Push(_script.Dequeue());
        return true;
    }

    /// <summary>Подаёт произвольный кадр. Требует подключения.</summary>
    public void Push(AircraftState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (ConnectionState != SimConnectionState.Connected)
            throw new InvalidOperationException("Mock simulator is not connected.");

        Current = state;
        AircraftStateUpdated?.Invoke(this, state);
    }

    private void SetConnectionState(SimConnectionState value)
    {
        if (ConnectionState == value)
            return;

        ConnectionState = value;
        ConnectionStateChanged?.Invoke(this, value);
    }
}
