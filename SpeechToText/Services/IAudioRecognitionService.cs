using SpeechToText.Models;

namespace SpeechToText.Services;

public interface IAudioRecognitionService
{
    Task<RecognitionResult> RecognizeAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ошибка распознавания с сообщением, пригодным для показа пользователю.
/// </summary>
public sealed class AudioRecognitionException : Exception
{
    public AudioRecognitionException(string message)
        : base(message)
    {
    }

    public AudioRecognitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
