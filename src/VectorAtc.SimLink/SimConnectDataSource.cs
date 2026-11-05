using System.Buffers.Binary;
using System.Runtime.InteropServices;
using VectorAtc.Core.Abstractions;
using VectorAtc.Core.Models;

namespace VectorAtc.SimLink;

/// <summary>
/// Данные игрока из работающего симулятора через SimConnect (опрос очереди сообщений в фоновом потоке).
/// События приходят в том контексте синхронизации, который был текущим при вызове <see cref="ConnectAsync"/>
/// (в окне это поток интерфейса). Здесь код не запускался: проверен только разбор сообщений (<see cref="SimConnectFrameParser"/>).
/// </summary>
public sealed class SimConnectDataSource : ISimDataSource, IDisposable
{
    private const uint DefineId = 1;
    private const uint RequestId = 1;
    private const string ClientName = "Vector ATC";

    private readonly object _gate = new();
    private IntPtr _handle;
    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private SynchronizationContext? _context;

    public SimConnectionState ConnectionState { get; private set; } = SimConnectionState.Disconnected;

    public AircraftState? Current { get; private set; }

    /// <summary>Код последнего исключения SimConnect (SIMCONNECT_EXCEPTION), 0 если не было. Для отладки.</summary>
    public uint LastExceptionId { get; private set; }

    public event EventHandler<SimConnectionState>? ConnectionStateChanged;
    public event EventHandler<AircraftState>? AircraftStateUpdated;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (ConnectionState != SimConnectionState.Disconnected)
                return;
            ConnectionState = SimConnectionState.Connecting;
        }

        _context = SynchronizationContext.Current;
        ConnectionStateChanged?.Invoke(this, SimConnectionState.Connecting);

        try
        {
            var handle = await Task.Run(OpenAndRequest, cancellationToken);
            lock (_gate)
            {
                _handle = handle;
                _cts = new CancellationTokenSource();
                _thread = new Thread(() => PumpMessages(handle, _cts.Token))
                {
                    IsBackground = true,
                    Name = "SimConnect pump",
                };
            }

            ConnectionState = SimConnectionState.Connected;
            ConnectionStateChanged?.Invoke(this, SimConnectionState.Connected);
            _thread.Start();
        }
        catch
        {
            ConnectionState = SimConnectionState.Disconnected;
            ConnectionStateChanged?.Invoke(this, SimConnectionState.Disconnected);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        Thread? thread;
        lock (_gate)
        {
            thread = _thread;
            _cts?.Cancel();
        }

        if (thread is not null)
            await Task.Run(() => thread.Join(TimeSpan.FromSeconds(3)));
    }

    public void Dispose() => DisconnectAsync().GetAwaiter().GetResult();

    private static IntPtr OpenAndRequest()
    {
        int hr;
        IntPtr handle;
        try
        {
            hr = SimConnectNative.SimConnect_Open(out handle, ClientName, IntPtr.Zero, 0, IntPtr.Zero, 0);
        }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            throw new SimConnectionException(SimConnectionFailure.LibraryNotFound, ex.Message, ex);
        }

        if (hr < 0)
            throw new SimConnectionException(SimConnectionFailure.OpenFailed, $"SimConnect_Open failed: 0x{hr:X8}");

        foreach (var (name, units) in SimConnectFrameParser.Variables)
        {
            hr = SimConnectNative.SimConnect_AddToDataDefinition(
                handle, DefineId, name, units, SimConnectNative.DataTypeFloat64, 0f, SimConnectNative.Unused);
            if (hr < 0)
            {
                SimConnectNative.SimConnect_Close(handle);
                throw new SimConnectionException(SimConnectionFailure.RequestFailed, $"AddToDataDefinition({name}) failed: 0x{hr:X8}");
            }
        }

        hr = SimConnectNative.SimConnect_RequestDataOnSimObject(
            handle, RequestId, DefineId, SimConnectNative.ObjectIdUser, SimConnectNative.PeriodSecond, 0, 0, 0, 0);
        if (hr < 0)
        {
            SimConnectNative.SimConnect_Close(handle);
            throw new SimConnectionException(SimConnectionFailure.RequestFailed, $"RequestDataOnSimObject failed: 0x{hr:X8}");
        }

        return handle;
    }

    private void PumpMessages(IntPtr handle, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var hr = SimConnectNative.SimConnect_GetNextDispatch(handle, out var pointer, out var size);
                if (hr < 0 || pointer == IntPtr.Zero || size < 12)
                {
                    token.WaitHandle.WaitOne(20);
                    continue;
                }

                var message = new byte[size];
                Marshal.Copy(pointer, message, 0, (int)size);
                if (!Dispatch(message))
                    break;
            }
        }
        finally
        {
            SimConnectNative.SimConnect_Close(handle);
            lock (_gate)
            {
                _handle = IntPtr.Zero;
                _thread = null;
                _cts = null;
            }

            Post(() =>
            {
                Current = null;
                ConnectionState = SimConnectionState.Disconnected;
                ConnectionStateChanged?.Invoke(this, SimConnectionState.Disconnected);
            });
        }
    }

    /// <summary>Возвращает false, если симулятор закрылся и опрос нужно остановить.</summary>
    private bool Dispatch(byte[] message)
    {
        var id = BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(8, 4));
        switch (id)
        {
            case SimConnectNative.RecvIdQuit:
                return false;

            case SimConnectNative.RecvIdException when message.Length >= 16:
                LastExceptionId = BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(12, 4));
                return true;

            case SimConnectFrameParser.RecvIdSimObjectData:
                if (SimConnectFrameParser.TryParseAircraft(message, RequestId, out var state) && state is not null)
                {
                    Post(() =>
                    {
                        Current = state;
                        AircraftStateUpdated?.Invoke(this, state);
                    });
                }

                return true;

            default:
                return true;
        }
    }

    private void Post(Action action)
    {
        var context = _context;
        if (context is null) action();
        else context.Post(_ => action(), null);
    }
}
