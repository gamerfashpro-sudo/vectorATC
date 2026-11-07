namespace VectorAtc.Core.Diagnostics;

/// <summary>
/// Где приложение ищет SimConnect.dll на компьютере пользователя (сама библиотека с приложением не поставляется).
/// Порядок: рядом с .exe, в папке приложения, затем в папке SDK симулятора по переменным окружения.
/// Имена переменных MSFS2024_SDK и MSFS_SDK взяты из документации SDK по памяти и не проверены на реальной установке.
/// После этого загрузчик Windows ищет библиотеку обычным способом (этот путь список не содержит).
/// </summary>
public static class SimConnectLocator
{
    public const string FileName = "SimConnect.dll";

    public static IReadOnlyList<string> Candidates(string? exeDirectory, string baseDirectory, string? sdk2024, string? sdkLegacy)
    {
        var list = new List<string>();

        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !list.Contains(path))
                list.Add(path);
        }

        if (!string.IsNullOrWhiteSpace(exeDirectory)) Add(Path.Combine(exeDirectory, FileName));
        if (!string.IsNullOrWhiteSpace(baseDirectory)) Add(Path.Combine(baseDirectory, FileName));
        if (!string.IsNullOrWhiteSpace(sdk2024)) Add(Path.Combine(sdk2024, "SimConnect SDK", "lib", FileName));
        if (!string.IsNullOrWhiteSpace(sdkLegacy)) Add(Path.Combine(sdkLegacy, "SimConnect SDK", "lib", FileName));
        return list;
    }

    /// <summary>Список для текущего процесса.</summary>
    public static IReadOnlyList<string> ForThisProcess() => Candidates(
        Path.GetDirectoryName(Environment.ProcessPath),
        AppContext.BaseDirectory,
        Environment.GetEnvironmentVariable("MSFS2024_SDK"),
        Environment.GetEnvironmentVariable("MSFS_SDK"));
}
