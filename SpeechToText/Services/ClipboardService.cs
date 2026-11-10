using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace SpeechToText.Services;

public interface IClipboardService
{
    void SetText(string text);
}

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        // Буфер обмена может быть занят другим процессом — пробуем несколько раз.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
        }

        Clipboard.SetText(text);
    }
}
