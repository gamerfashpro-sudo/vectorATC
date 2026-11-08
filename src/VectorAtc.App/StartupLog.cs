using System.Diagnostics;
using System.IO;
using System.Text;

namespace VectorAtc.App;

/// <summary>
/// Журнал запуска: %APPDATA%\VectorATC\startup.log. Пишет время от старта процесса до показа окна и текст любой необработанной
/// ошибки. Нужен, чтобы при «не запускается» было видно причину. Сам никогда не бросает исключений.
/// </summary>
internal static class StartupLog
{
    private static readonly string PathToLog = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VectorATC", "startup.log");

    public static string FilePath => PathToLog;

    public static void Write(string message)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(PathToLog);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Не даём журналу расти бесконечно: после 200 КБ начинаем заново.
            if (File.Exists(PathToLog) && new FileInfo(PathToLog).Length > 200_000)
                File.Delete(PathToLog);

            var sinceStart = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
            File.AppendAllText(PathToLog,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} (+{sinceStart:F1} c от запуска процесса) {message}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch (Exception)
        {
            // журнал необязателен
        }
    }
}
