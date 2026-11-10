namespace VectorAtc.Core.Speech;

/// <summary>Файлы, которые нужны озвучке или распознаванию (модели, piper.exe): диагностика показывает, есть ли они.</summary>
public interface IVoiceFiles
{
    IReadOnlyList<string> VoiceFiles { get; }
}
