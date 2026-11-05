using System.Reflection;
using System.Runtime.InteropServices;

namespace VectorAtc.SimLink;

/// <summary>
/// Вызовы SimConnect.dll напрямую (P/Invoke). Библиотеку приложение с собой не возит: права на её распространение
/// для MSFS 2024 официально не подтверждены. Она ищется на компьютере пользователя: рядом с .exe, в SDK симулятора
/// (переменная окружения MSFS2024_SDK, папка «SimConnect SDK\lib»), затем обычным поиском Windows.
/// ВНИМАНИЕ: сигнатуры и числовые значения перечислений взяты из описания SimConnect SDK и в этой среде не запускались.
/// </summary>
internal static class SimConnectNative
{
    private const string Library = "SimConnect";

    public const uint DataTypeFloat64 = 4;
    public const uint PeriodSecond = 4;
    public const uint ObjectIdUser = 0;
    public const uint Unused = 0xFFFFFFFF;

    public const uint RecvIdException = 1;
    public const uint RecvIdOpen = 2;
    public const uint RecvIdQuit = 3;

    static SimConnectNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(SimConnectNative).Assembly, Resolve);
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != Library)
            return IntPtr.Zero;

        var candidates = new List<string>();
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(exeDir)) candidates.Add(Path.Combine(exeDir, "SimConnect.dll"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "SimConnect.dll"));

        var sdk = Environment.GetEnvironmentVariable("MSFS2024_SDK");
        if (!string.IsNullOrEmpty(sdk))
            candidates.Add(Path.Combine(sdk, "SimConnect SDK", "lib", "SimConnect.dll"));

        foreach (var path in candidates)
        {
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
                return handle;
        }

        return NativeLibrary.TryLoad("SimConnect.dll", assembly, searchPath, out var fallback) ? fallback : IntPtr.Zero;
    }

    [DllImport(Library, ExactSpelling = true)]
    public static extern int SimConnect_Open(
        out IntPtr phSimConnect,
        [MarshalAs(UnmanagedType.LPStr)] string szName,
        IntPtr hWnd,
        uint userEventWin32,
        IntPtr hEventHandle,
        uint configIndex);

    [DllImport(Library, ExactSpelling = true)]
    public static extern int SimConnect_Close(IntPtr hSimConnect);

    [DllImport(Library, ExactSpelling = true)]
    public static extern int SimConnect_AddToDataDefinition(
        IntPtr hSimConnect,
        uint defineId,
        [MarshalAs(UnmanagedType.LPStr)] string datumName,
        [MarshalAs(UnmanagedType.LPStr)] string unitsName,
        uint datumType,
        float epsilon,
        uint datumId);

    [DllImport(Library, ExactSpelling = true)]
    public static extern int SimConnect_RequestDataOnSimObject(
        IntPtr hSimConnect,
        uint requestId,
        uint defineId,
        uint objectId,
        uint period,
        uint flags,
        uint origin,
        uint interval,
        uint limit);

    [DllImport(Library, ExactSpelling = true)]
    public static extern int SimConnect_GetNextDispatch(IntPtr hSimConnect, out IntPtr ppData, out uint pcbData);
}
