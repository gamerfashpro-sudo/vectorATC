namespace VectorAtc.Core.Speech;

/// <summary>
/// Какой русский голос Piper озвучивает какую роль и как вызывать piper.exe. Piper запускается отдельным процессом
/// (<c>piper.exe --model файл.onnx --output_file файл.wav</c>, текст в stdin) и работает без интернета.
/// Какие из голосов мужские, а какой женский, взято из моих допущений по названиям и на слух не проверено.
/// </summary>
public static class PiperVoices
{
    public static string ModelName(SpeechRole role) => role switch
    {
        SpeechRole.Cabin => "ru_RU-irina-medium",
        SpeechRole.Pilot => "ru_RU-dmitri-medium",
        _ => "ru_RU-denis-medium"
    };

    /// <summary>Полный путь к модели в папке голосов или null, если файлов модели нет.</summary>
    public static string? FindModel(string voicesFolder, SpeechRole role)
    {
        var model = Path.Combine(voicesFolder, ModelName(role) + ".onnx");
        return File.Exists(model) && File.Exists(model + ".json") ? model : null;
    }

    public static string BuildArguments(string modelPath, string wavPath) =>
        $"--model \"{modelPath}\" --output_file \"{wavPath}\" --quiet";
}
