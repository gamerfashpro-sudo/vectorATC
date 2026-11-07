using System.Globalization;
using VectorAtc.Core.Abstractions;
using VectorAtc.Core.Diagnostics;
using VectorAtc.Core.Speech;
using VectorAtc.Localization;

namespace VectorAtc.ViewModels;

/// <summary>
/// Собирает отчёт «что происходит с подключением и звуком». Всё внешнее (время, наличие файлов, список путей, разрядность)
/// передаётся снаружи, поэтому сборку можно проверить тестами без симулятора.
/// </summary>
public sealed class DiagnosticsBuilder
{
    private readonly ILocalizationService _loc;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<IReadOnlyList<string>> _candidates;
    private readonly bool _is64Bit;
    private readonly string _os;

    public DiagnosticsBuilder(
        ILocalizationService loc,
        Func<DateTimeOffset>? now = null,
        Func<string, bool>? fileExists = null,
        Func<IReadOnlyList<string>>? candidates = null,
        bool? is64Bit = null,
        string? os = null)
    {
        _loc = loc;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _fileExists = fileExists ?? File.Exists;
        _candidates = candidates ?? SimConnectLocator.ForThisProcess;
        _is64Bit = is64Bit ?? Environment.Is64BitProcess;
        _os = os ?? Environment.OSVersion.ToString();
    }

    public IReadOnlyList<DiagnosticsItem> Build(
        SimConnectionState state, bool isDemo, ISimDiagnostics? counters, ISpeechOutput? voice, ISpeechInput? mic)
    {
        var items = new List<DiagnosticsItem>
        {
            new(_loc["Diag.System"], $"{_os}", DiagnosticsLevel.Info),
            new(_loc["Diag.Process"], _is64Bit ? "64-bit" : "32-bit", _is64Bit ? DiagnosticsLevel.Ok : DiagnosticsLevel.Fail),
            new(_loc["Diag.Source"], _loc[isDemo ? "Diag.SourceDemo" : "Diag.SourceSim"], DiagnosticsLevel.Info),
        };

        var paths = _candidates();
        var found = paths.Where(_fileExists).ToList();
        items.Add(found.Count > 0
            ? new DiagnosticsItem(_loc["Diag.Library"], string.Format(CultureInfo.InvariantCulture, _loc["Diag.LibraryFound"], found[0]), DiagnosticsLevel.Ok)
            : new DiagnosticsItem(_loc["Diag.Library"], _loc["Diag.LibraryMissing"], isDemo ? DiagnosticsLevel.Info : DiagnosticsLevel.Warn));
        foreach (var path in paths)
            items.Add(new DiagnosticsItem("   " + _loc[_fileExists(path) ? "Diag.Exists" : "Diag.NotExists"], path, DiagnosticsLevel.Info));

        items.Add(new DiagnosticsItem(_loc["Diag.Connection"], _loc["Diag.State." + state],
            state switch { SimConnectionState.Connected => DiagnosticsLevel.Ok, SimConnectionState.Connecting => DiagnosticsLevel.Warn, _ => DiagnosticsLevel.Info }));

        if (counters is not null)
        {
            var last = counters.LastFrameAt;
            string age;
            DiagnosticsLevel ageLevel;
            if (last is null)
            {
                age = _loc["Diag.Never"];
                ageLevel = state == SimConnectionState.Connected ? DiagnosticsLevel.Warn : DiagnosticsLevel.Info;
            }
            else
            {
                var seconds = Math.Max(0, (_now() - last.Value).TotalSeconds);
                age = string.Format(CultureInfo.InvariantCulture, _loc["Diag.SecondsAgo"], seconds);
                ageLevel = seconds < 3 ? DiagnosticsLevel.Ok : DiagnosticsLevel.Warn;
            }

            items.Add(new DiagnosticsItem(_loc["Diag.Frames"], counters.FramesReceived.ToString(CultureInfo.InvariantCulture),
                counters.FramesReceived > 0 ? DiagnosticsLevel.Ok : DiagnosticsLevel.Info));
            items.Add(new DiagnosticsItem(_loc["Diag.LastFrame"], age, ageLevel));
            items.Add(new DiagnosticsItem(_loc["Diag.Exception"],
                counters.LastExceptionId == 0 ? _loc["Diag.None"] : counters.LastExceptionId.ToString(CultureInfo.InvariantCulture),
                counters.LastExceptionId == 0 ? DiagnosticsLevel.Ok : DiagnosticsLevel.Fail));
        }

        foreach (var (code, culture) in new[] { ("EN", "en"), ("RU", "ru") })
        {
            var c = CultureInfo.GetCultureInfo(culture);
            if (voice is not null)
                items.Add(Flag(_loc["Diag.VoiceOut"] + " " + code, voice.CanSpeak(c)));
            if (mic is not null)
                items.Add(Flag(_loc["Diag.VoiceIn"] + " " + code, mic.CanListen(c)));
        }

        return items;
    }

    private DiagnosticsItem Flag(string label, bool ok) =>
        new(label, _loc[ok ? "Diag.Available" : "Diag.Unavailable"], ok ? DiagnosticsLevel.Ok : DiagnosticsLevel.Warn);

    /// <summary>Текст отчёта для копирования и отправки: по строке на пункт.</summary>
    public static string ToText(IEnumerable<DiagnosticsItem> items) =>
        string.Join(Environment.NewLine, items.Select(i =>
            $"[{i.Level.ToString().ToUpperInvariant()}] {i.Label.TrimStart()}: {i.Value}"));
}
