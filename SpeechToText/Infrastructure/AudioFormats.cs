using System.IO;

namespace SpeechToText.Infrastructure;

/// <summary>
/// Поддерживаемые аудиоформаты и вспомогательные функции форматирования.
/// </summary>
public static class AudioFormats
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".m4a", ".aac", ".ogg", ".flac", ".wma"
    };

    public const string Filter = "Аудиофайлы|*.wav;*.mp3;*.m4a;*.aac;*.ogg;*.flac;*.wma|Все файлы|*.*";

    public const string HumanReadableList = "WAV · MP3 · M4A · AAC · OGG · FLAC · WMA";

    public static bool IsSupported(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    public static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".m4a" => "audio/mp4",
        ".aac" => "audio/aac",
        ".ogg" => "audio/ogg",
        ".flac" => "audio/flac",
        ".wma" => "audio/x-ms-wma",
        _ => "application/octet-stream"
    };

    public static string FormatSize(long bytes)
    {
        const double kilobyte = 1024d;
        const double megabyte = kilobyte * 1024d;
        const double gigabyte = megabyte * 1024d;

        return bytes switch
        {
            < 1024 => $"{bytes} Б",
            < (long)megabyte => $"{bytes / kilobyte:0.0} КБ",
            < (long)gigabyte => $"{bytes / megabyte:0.0} МБ",
            _ => $"{bytes / gigabyte:0.00} ГБ"
        };
    }
}
