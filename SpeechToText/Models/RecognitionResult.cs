namespace SpeechToText.Models;

/// <summary>
/// Ответ сервиса распознавания: text — распознанный текст, state — состояние обработки.
/// </summary>
public sealed class RecognitionResult
{
    public string? Text { get; set; }

    public string? State { get; set; }
}
