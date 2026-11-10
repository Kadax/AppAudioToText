namespace SpeechToText.Services;

public interface IDialogService
{
    string? PickAudioFile();

    /// <summary>Показывает диалог сохранения и записывает текст. Возвращает путь или null.</summary>
    string? SaveTranscript(string text, string suggestedFileName);
}
