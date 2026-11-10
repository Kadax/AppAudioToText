using System.Windows;
using SpeechToText.Models;
using SpeechToText.Services;
using SpeechToText.ViewModels;

namespace SpeechToText;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = ApiSettings.Load();

        _viewModel = new MainViewModel(
            new HttpAudioRecognitionService(settings),
            new AudioRecorderService(),
            new DialogService(),
            new ClipboardService(),
            settings);

        var window = new MainWindow
        {
            DataContext = _viewModel
        };

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        _viewModel = null;

        base.OnExit(e);
    }
}
