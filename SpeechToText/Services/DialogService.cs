using System.IO;
using System.Text;
using Microsoft.Win32;
using SpeechToText.Infrastructure;

namespace SpeechToText.Services;

public sealed class DialogService : IDialogService
{
    public string? PickAudioFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите аудиофайл",
            Filter = AudioFormats.Filter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveTranscript(string text, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Сохранить распознанный текст",
            Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
            DefaultExt = ".txt",
            AddExtension = true,
            FileName = suggestedFileName,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        File.WriteAllText(dialog.FileName, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return dialog.FileName;
    }
}
