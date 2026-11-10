using System.IO;

namespace SpeechToText.Infrastructure;

/// <summary>
/// Записи с микрофона сохраняются в подпапку audio рядом с приложением.
/// </summary>
public static class RecordingStorage
{
    public static string RootDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "audio");

    public static string CreateNewPath()
    {
        Directory.CreateDirectory(RootDirectory);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var path = Path.Combine(RootDirectory, $"recording_{timestamp}.wav");

        var index = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(RootDirectory, $"recording_{timestamp}_{index++}.wav");
        }

        return path;
    }
}
