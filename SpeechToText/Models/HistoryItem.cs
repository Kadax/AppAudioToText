using System.IO;
using SpeechToText.Infrastructure;

namespace SpeechToText.Models;

/// <summary>
/// Запись в истории распознаваний текущей сессии.
/// </summary>
public sealed class HistoryItem
{
    public required string FileName { get; init; }

    public required string FilePath { get; init; }

    public required string Text { get; init; }

    public required string FileInfoText { get; init; }

    public string? State { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.Now;

    public string TimeText => CreatedAt.ToString("HH:mm:ss");

    public bool IsError => string.Equals(State, "Error", StringComparison.OrdinalIgnoreCase);

    public string StateText => IsError ? "Ошибка" : "Готово";

    public string Preview
    {
        get
        {
            var text = Text.ReplaceLineEndings(" ").Trim();
            return text.Length <= 140 ? text : text[..140] + "…";
        }
    }

    public static HistoryItem Create(string filePath, string text, string? state)
    {
        var info = new FileInfo(filePath);

        return new HistoryItem
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            Text = text,
            State = state,
            FileInfoText = info.Exists
                ? $"{AudioFormats.FormatSize(info.Length)} · {info.Extension.TrimStart('.').ToUpperInvariant()}"
                : "файл недоступен"
        };
    }
}
