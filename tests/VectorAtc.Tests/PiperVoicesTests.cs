using VectorAtc.Core.Speech;
using Xunit;

namespace VectorAtc.Tests;

public class PiperVoicesTests
{
    [Fact]
    public void Each_role_has_its_own_voice()
    {
        var names = new[] { SpeechRole.Controller, SpeechRole.Cabin, SpeechRole.Pilot }.Select(PiperVoices.ModelName).Distinct().ToList();
        Assert.Equal(3, names.Count);
    }

    [Fact]
    public void Model_is_found_only_with_onnx_and_json()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vatc-piper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var model = Path.Combine(dir, PiperVoices.ModelName(SpeechRole.Controller) + ".onnx");
            File.WriteAllText(model, "x");
            Assert.Null(PiperVoices.FindModel(dir, SpeechRole.Controller));

            File.WriteAllText(model + ".json", "{}");
            Assert.Equal(model, PiperVoices.FindModel(dir, SpeechRole.Controller));
            Assert.Null(PiperVoices.FindModel(dir, SpeechRole.Cabin));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Arguments_quote_paths_with_spaces()
    {
        Assert.Equal("--model \"C:\\a b\\m.onnx\" --output_file \"C:\\t\\o.wav\" --quiet",
            PiperVoices.BuildArguments("C:\\a b\\m.onnx", "C:\\t\\o.wav"));
    }
}
