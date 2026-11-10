using System.Windows;
using SpeechToText.Infrastructure;
using SpeechToText.ViewModels;

namespace SpeechToText;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        var hasAudioFile = TryGetAudioFile(e.Data) is not null;
        e.Effects = hasAudioFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;

        if (ViewModel is { } viewModel)
        {
            viewModel.IsDragOver = hasAudioFile;
        }
    }

    private void OnWindowDragLeave(object sender, DragEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsDragOver = false;
        }
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsDragOver = false;
        }

        if (TryGetAudioFile(e.Data) is { } path)
        {
            ViewModel?.SetFileFromPath(path);
        }

        e.Handled = true;
    }

    private static string? TryGetAudioFile(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop)
            || data.GetData(DataFormats.FileDrop) is not string[] files
            || files.Length == 0)
        {
            return null;
        }

        return files.FirstOrDefault(AudioFormats.IsSupported) ?? files[0];
    }
}
