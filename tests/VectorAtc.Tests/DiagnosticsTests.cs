using System.Globalization;
using VectorAtc.Core.Abstractions;
using VectorAtc.Core.Diagnostics;
using VectorAtc.Core.Speech;
using VectorAtc.Localization;
using VectorAtc.ViewModels;
using Xunit;

namespace VectorAtc.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Locator_lists_exe_folder_base_folder_and_sdk_folders_in_order_without_duplicates()
    {
        var list = SimConnectLocator.Candidates("app", "app", "sdk24", "sdk20");

        Assert.Equal(new[]
        {
            Path.Combine("app", "SimConnect.dll"),
            Path.Combine("sdk24", "SimConnect SDK", "lib", "SimConnect.dll"),
            Path.Combine("sdk20", "SimConnect SDK", "lib", "SimConnect.dll"),
        }, list.ToArray());
    }

    [Fact]
    public void Locator_skips_missing_values()
    {
        var list = SimConnectLocator.Candidates(null, "base", null, "");
        Assert.Equal(new[] { Path.Combine("base", "SimConnect.dll") }, list.ToArray());
    }

    private sealed class FakeCounters : ISimDiagnostics
    {
        public long FramesReceived { get; set; }
        public DateTimeOffset? LastFrameAt { get; set; }
        public uint LastExceptionId { get; set; }
    }

    private sealed class FakeVoice : ISpeechOutput, ISpeechInput
    {
        public bool CanSpeak(CultureInfo culture) => culture.TwoLetterISOLanguageName == "en";
        public void Speak(string text, CultureInfo culture, SpeechRole role) { }
        public void Stop() { }
        public bool CanListen(CultureInfo culture) => culture.TwoLetterISOLanguageName == "ru";
        public void StartListening(CultureInfo culture) { }
        public void StopListening() { }
        public event EventHandler<string>? Recognized { add { } remove { } }
        public event EventHandler? NotUnderstood { add { } remove { } }
        public event EventHandler? Failed { add { } remove { } }
    }

    private sealed class FakeVoiceWithFiles : ISpeechInput, IVoiceFiles
    {
        public IReadOnlyList<string> VoiceFiles => new[] { "m/ggml-small.bin" };
        public bool CanListen(CultureInfo culture) => false;
        public void StartListening(CultureInfo culture) { }
        public void StopListening() { }
        public event EventHandler<string>? Recognized { add { } remove { } }
        public event EventHandler? NotUnderstood { add { } remove { } }
        public event EventHandler? Failed { add { } remove { } }
    }

    [Fact]
    public void Voice_model_files_are_listed_with_their_presence()
    {
        var missing = Builder(Array.Empty<string>()).Build(SimConnectionState.Disconnected, false, null, null, new FakeVoiceWithFiles());
        Assert.Contains(missing, i => i.Value == "m/ggml-small.bin (missing)");

        var present = Builder(new[] { "m/ggml-small.bin" }).Build(SimConnectionState.Disconnected, false, null, null, new FakeVoiceWithFiles());
        Assert.Contains(present, i => i.Value == "m/ggml-small.bin (exists)");
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static DiagnosticsBuilder Builder(IEnumerable<string> existing, bool is64 = true) =>
        new(new LocalizationService(), () => Now, existing.ToHashSet().Contains,
            () => new[] { "a/SimConnect.dll", "b/SimConnect.dll" }, is64, "TestOS 1.0");

    private static DiagnosticsItem Find(IReadOnlyList<DiagnosticsItem> items, string label) => items.Single(i => i.Label == label);

    [Fact]
    public void Library_found_is_ok_and_names_the_first_existing_path()
    {
        var items = Builder(new[] { "b/SimConnect.dll" }).Build(SimConnectionState.Disconnected, false, null, null, null);
        var lib = Find(items, "SimConnect.dll");
        Assert.Equal(DiagnosticsLevel.Ok, lib.Level);
        Assert.Equal("found: b/SimConnect.dll", lib.Value);
        Assert.Contains(items, i => i.Label == "Searched" && i.Value == "b/SimConnect.dll (exists)");
        Assert.Contains(items, i => i.Label == "Searched" && i.Value == "a/SimConnect.dll (missing)");
    }

    [Fact]
    public void Library_missing_is_a_warning_for_simulator_but_only_info_for_demo()
    {
        var sim = Builder(Array.Empty<string>()).Build(SimConnectionState.Disconnected, false, null, null, null);
        var demo = Builder(Array.Empty<string>()).Build(SimConnectionState.Disconnected, true, null, null, null);
        Assert.Equal(DiagnosticsLevel.Warn, Find(sim, "SimConnect.dll").Level);
        Assert.Equal(DiagnosticsLevel.Info, Find(demo, "SimConnect.dll").Level);
    }

    [Fact]
    public void Thirty_two_bit_process_is_a_failure()
    {
        var items = Builder(Array.Empty<string>(), is64: false).Build(SimConnectionState.Disconnected, true, null, null, null);
        Assert.Equal(DiagnosticsLevel.Fail, Find(items, "Process").Level);
        Assert.Equal("32-bit", Find(items, "Process").Value);
    }

    [Fact]
    public void Frame_counters_show_age_and_health()
    {
        var fresh = new FakeCounters { FramesReceived = 42, LastFrameAt = Now.AddSeconds(-1.5) };
        var items = Builder(Array.Empty<string>()).Build(SimConnectionState.Connected, false, fresh, null, null);
        Assert.Equal("42", Find(items, "Data frames received").Value);
        Assert.Equal("1.5 s ago", Find(items, "Last frame").Value);
        Assert.Equal(DiagnosticsLevel.Ok, Find(items, "Last frame").Level);
        Assert.Equal(DiagnosticsLevel.Ok, Find(items, "Connection").Level);

        var stale = new FakeCounters { FramesReceived = 42, LastFrameAt = Now.AddSeconds(-10) };
        Assert.Equal(DiagnosticsLevel.Warn,
            Find(Builder(Array.Empty<string>()).Build(SimConnectionState.Connected, false, stale, null, null), "Last frame").Level);
    }

    [Fact]
    public void Connected_without_any_frame_is_a_warning_and_exception_code_is_a_failure()
    {
        var none = new FakeCounters { FramesReceived = 0, LastFrameAt = null, LastExceptionId = 7 };
        var items = Builder(Array.Empty<string>()).Build(SimConnectionState.Connected, false, none, null, null);
        Assert.Equal("never", Find(items, "Last frame").Value);
        Assert.Equal(DiagnosticsLevel.Warn, Find(items, "Last frame").Level);
        Assert.Equal("7", Find(items, "Last simulator error code").Value);
        Assert.Equal(DiagnosticsLevel.Fail, Find(items, "Last simulator error code").Level);
    }

    [Fact]
    public void Demo_source_has_no_frame_counters()
    {
        var items = Builder(Array.Empty<string>()).Build(SimConnectionState.Connected, true, null, null, null);
        Assert.DoesNotContain(items, i => i.Label == "Data frames received");
    }

    [Fact]
    public void Voice_availability_is_reported_per_language()
    {
        var v = new FakeVoice();
        var items = Builder(Array.Empty<string>()).Build(SimConnectionState.Disconnected, true, null, v, v);
        Assert.Equal("available", Find(items, "Voice output EN").Value);
        Assert.Equal("not available", Find(items, "Voice output RU").Value);
        Assert.Equal("not available", Find(items, "Voice input EN").Value);
        Assert.Equal("available", Find(items, "Voice input RU").Value);
    }

    [Fact]
    public void Report_text_has_one_line_per_item_with_level_prefix()
    {
        var text = DiagnosticsBuilder.ToText(new[]
        {
            new DiagnosticsItem("A", "1", DiagnosticsLevel.Ok),
            new DiagnosticsItem("   B", "2", DiagnosticsLevel.Fail),
        });
        Assert.Equal("[OK] A: 1" + Environment.NewLine + "[FAIL] B: 2", text);
    }
}

public class SimConnectLocatorDriveTests
{
    [Fact]
    public void Default_sdk_folder_is_probed_on_every_given_drive_after_the_other_places()
    {
        var list = SimConnectLocator.Candidates("exe", "base", null, null, new[] { "C:/", "D:/" });
        Assert.Equal(4, list.Count);
        Assert.EndsWith("SimConnect.dll", list[0]);
        Assert.Contains("MSFS 2024 SDK", list[2]);
        Assert.StartsWith("C:", list[2]);
        Assert.StartsWith("D:", list[3]);
    }
}
