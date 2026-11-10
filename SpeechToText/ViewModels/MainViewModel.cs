using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpeechToText.Infrastructure;
using SpeechToText.Models;
using SpeechToText.Services;

namespace SpeechToText.ViewModels;

public enum StatusKind
{
    Idle,
    Busy,
    Success,
    Error
}

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MaxHistoryItems = 50;

    private readonly IAudioRecognitionService _recognitionService;
    private readonly IAudioRecorderService _recorderService;
    private readonly IDialogService _dialogService;
    private readonly IClipboardService _clipboardService;
    private readonly ApiSettings _settings;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _recordingTimer;
    private readonly Stopwatch _recordingStopwatch = new();

    private CancellationTokenSource? _recognitionCts;
    private bool _disposed;

    public MainViewModel(
        IAudioRecognitionService recognitionService,
        IAudioRecorderService recorderService,
        IDialogService dialogService,
        IClipboardService clipboardService,
        ApiSettings settings)
    {
        _recognitionService = recognitionService;
        _recorderService = recorderService;
        _dialogService = dialogService;
        _clipboardService = clipboardService;
        _settings = settings;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _recordingTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _recordingTimer.Tick += OnRecordingTimerTick;

        _recorderService.LevelChanged += OnLevelChanged;

        RefreshDeviceList();
    }

    public string ServiceUrl => _settings.BaseUrl;

    public ObservableCollection<HistoryItem> History { get; } = [];

    public ObservableCollection<AudioDeviceInfo> Devices { get; } = [];

    // ----- Состояние формы -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFileMode))]
    [NotifyPropertyChangedFor(nameof(IsMicrophoneMode))]
    [NotifyPropertyChangedFor(nameof(CurrentAudioPath))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand))]
    private AudioInputMode _mode = AudioInputMode.File;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    [NotifyPropertyChangedFor(nameof(FileName))]
    [NotifyPropertyChangedFor(nameof(FileInfoText))]
    [NotifyPropertyChangedFor(nameof(CurrentAudioPath))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand))]
    private string? _selectedFilePath;

    [ObservableProperty]
    private AudioDeviceInfo? _selectedDevice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecording))]
    [NotifyPropertyChangedFor(nameof(RecordingHintText))]
    [NotifyPropertyChangedFor(nameof(CurrentAudioPath))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearRecordingCommand))]
    private string? _recordedFilePath;

    [ObservableProperty]
    private string _recordedFileName = string.Empty;

    [ObservableProperty]
    private string _recordedFileInfo = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingHintText))]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecognizeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRecordingCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _recordingTimeText = "00:00";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMicLevelHigh))]
    [NotifyPropertyChangedFor(nameof(IsMicLevelClipping))]
    private double _microphoneLevel;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(WordCountText))]
    [NotifyPropertyChangedFor(nameof(CharCountText))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _resultText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultStateText))]
    [NotifyPropertyChangedFor(nameof(IsResultError))]
    private string? _resultState;

    [ObservableProperty]
    private string _statusText = "Готов к работе";

    [ObservableProperty]
    private StatusKind _statusKind = StatusKind.Idle;

    [ObservableProperty]
    private HistoryItem? _selectedHistoryItem;

    // ----- Вычисляемые свойства -----

    public bool IsFileMode => Mode == AudioInputMode.File;

    public bool IsMicrophoneMode => Mode == AudioInputMode.Microphone;

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SelectedFilePath);

    public string FileName => string.IsNullOrWhiteSpace(SelectedFilePath)
        ? string.Empty
        : Path.GetFileName(SelectedFilePath);

    public string FileInfoText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                return string.Empty;
            }

            var info = new FileInfo(SelectedFilePath);
            return $"{AudioFormats.FormatSize(info.Length)} · {info.Extension.TrimStart('.').ToUpperInvariant()}";
        }
    }

    public bool HasRecording => !string.IsNullOrWhiteSpace(RecordedFilePath);

    public bool HasResult => !string.IsNullOrWhiteSpace(ResultText);

    public bool IsResultError => string.Equals(ResultState, "Error", StringComparison.OrdinalIgnoreCase);

    public string ResultStateText => string.IsNullOrWhiteSpace(ResultState)
        ? string.Empty
        : IsResultError ? "Ошибка" : "Готово";

    public string WordCountText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ResultText))
            {
                return "Слов: 0";
            }

            var count = ResultText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            return $"Слов: {count}";
        }
    }

    public string CharCountText => $"Символов: {ResultText.Length}";

    public bool HasHistory => History.Count > 0;

    public string? CurrentAudioPath => Mode == AudioInputMode.File ? SelectedFilePath : RecordedFilePath;

    public bool CanRecognize => !IsBusy && !IsRecording && !string.IsNullOrWhiteSpace(CurrentAudioPath);

    public bool CanToggleRecording => IsRecording || (!IsBusy && SelectedDevice is not null);

    public bool IsMicLevelHigh => MicrophoneLevel >= 65;

    public bool IsMicLevelClipping => MicrophoneLevel >= 90;

    public string RecordingHintText => IsRecording
        ? "Идёт запись… нажмите, чтобы остановить"
        : HasRecording
            ? "Запись готова"
            : "Нажмите, чтобы начать запись";

    // ----- Команды -----

    [RelayCommand]
    private void ChooseFile()
    {
        var path = _dialogService.PickAudioFile();
        if (path is not null)
        {
            SetFileFromPath(path);
        }
    }

    [RelayCommand]
    private void ClearFile()
    {
        SelectedFilePath = null;
        SetStatus(StatusKind.Idle, "Файл убран. Выберите другой файл или запишите речь.");
    }

    [RelayCommand(CanExecute = nameof(CanToggleRecording))]
    private async Task ToggleRecordingAsync()
    {
        if (IsRecording)
        {
            await StopRecordingAsync();
        }
        else
        {
            StartRecording();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRecognize))]
    private async Task RecognizeAsync()
    {
        var path = CurrentAudioPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _recognitionCts?.Cancel();
        _recognitionCts?.Dispose();

        var cts = new CancellationTokenSource();
        _recognitionCts = cts;

        IsBusy = true;
        SetStatus(StatusKind.Busy, "Распознавание… Это может занять некоторое время.");

        try
        {
            var result = await _recognitionService.RecognizeAsync(path, cts.Token);

            SelectedHistoryItem = null;
            ResultState = result.State;
            ResultText = result.Text?.Trim() ?? string.Empty;
            AddHistoryItem(path, ResultText, result.State);

            SetStatus(StatusKind.Success, "Распознавание завершено. Результат добавлен в историю.");
        }
        catch (OperationCanceledException)
        {
            SetStatus(StatusKind.Idle, "Распознавание отменено.");
        }
        catch (AudioRecognitionException ex)
        {
            SetStatus(StatusKind.Error, ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, $"Неожиданная ошибка: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            cts.Dispose();
            if (ReferenceEquals(_recognitionCts, cts))
            {
                _recognitionCts = null;
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void Copy()
    {
        _clipboardService.SetText(ResultText);
        SetStatus(StatusKind.Success, "Текст скопирован в буфер обмена.");
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private void Save()
    {
        var sourceName = CurrentAudioPath is null
            ? "transcript"
            : Path.GetFileNameWithoutExtension(CurrentAudioPath);

        var savedPath = _dialogService.SaveTranscript(ResultText, $"{sourceName}_transcript.txt");
        if (savedPath is not null)
        {
            SetStatus(StatusKind.Success, $"Сохранено: {savedPath}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasHistory))]
    private void ClearHistory()
    {
        History.Clear();
        SelectedHistoryItem = null;

        OnPropertyChanged(nameof(HasHistory));
        ClearHistoryCommand.NotifyCanExecuteChanged();

        SetStatus(StatusKind.Idle, "История очищена.");
    }

    [RelayCommand(CanExecute = nameof(HasRecording))]
    private void ClearRecording()
    {
        if (RecordedFilePath is not null)
        {
            DeleteRecording(RecordedFilePath);
        }

        RecordedFilePath = null;
        RecordedFileName = string.Empty;
        RecordedFileInfo = string.Empty;

        SetStatus(StatusKind.Idle, "Запись удалена.");
    }

    [RelayCommand]
    private void RefreshDevices() => RefreshDeviceList();

    // ----- Запись с микрофона -----

    private void StartRecording()
    {
        if (SelectedDevice is null)
        {
            SetStatus(StatusKind.Error, "Микрофон не найден. Подключите устройство записи и нажмите «Обновить».");
            return;
        }

        try
        {
            _recorderService.Start(SelectedDevice.DeviceNumber);

            _recordingStopwatch.Restart();
            RecordingTimeText = "00:00";
            MicrophoneLevel = 0;
            _recordingTimer.Start();
            IsRecording = true;
            Mode = AudioInputMode.Microphone;

            SetStatus(StatusKind.Busy, "Идёт запись… Нажмите «Стоп», чтобы завершить.");
        }
        catch (Exception ex)
        {
            SetStatus(
                StatusKind.Error,
                $"Не удалось начать запись: {ex.Message}. Проверьте, что микрофон подключён и не занят другим приложением.");
        }
    }

    private async Task StopRecordingAsync()
    {
        _recordingTimer.Stop();
        _recordingStopwatch.Stop();
        IsRecording = false;
        MicrophoneLevel = 0;
        RecordingTimeText = "00:00";

        // Останавливаем запись и сохраняем WAV вне потока интерфейса.
        IsBusy = true;
        SetStatus(StatusKind.Busy, "Сохранение записи…");

        string? path = null;

        try
        {
            path = await _recorderService.StopAsync();
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Error, $"Не удалось остановить запись: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            if (StatusKind != StatusKind.Error)
            {
                SetStatus(StatusKind.Error, "Запись не сохранилась. Попробуйте ещё раз.");
            }

            return;
        }

        RecordedFilePath = path;
        RecordedFileName = Path.GetFileName(path);
        RecordedFileInfo = BuildRecordingInfo(path);

        SetStatus(StatusKind.Success, $"Запись готова: {RecordedFileName}. Нажмите «Распознать».");
    }

    private static string BuildRecordingInfo(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? $"{AudioFormats.FormatSize(info.Length)} · WAV · моно 44,1 кГц"
                : "WAV · моно 44,1 кГц";
        }
        catch (IOException)
        {
            return "WAV · моно 44,1 кГц";
        }
    }

    private void OnRecordingTimerTick(object? sender, EventArgs e)
    {
        var elapsed = _recordingStopwatch.Elapsed;
        RecordingTimeText = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }

    private void OnLevelChanged(object? sender, double level)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            if (!IsRecording)
            {
                return;
            }

            var scaled = Math.Min(100d, level * 300d);
            MicrophoneLevel = scaled > MicrophoneLevel ? scaled : Math.Max(0d, MicrophoneLevel * 0.75d);
        }));
    }

    // ----- Вспомогательные методы -----

    public void SetFileFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (IsRecording)
        {
            SetStatus(StatusKind.Idle, "Сначала остановите запись, затем выберите файл.");
            return;
        }

        if (IsBusy)
        {
            SetStatus(StatusKind.Busy, "Дождитесь завершения текущего распознавания.");
            return;
        }

        if (!File.Exists(path))
        {
            SetStatus(StatusKind.Error, "Файл не найден. Возможно, он был перемещён или удалён.");
            return;
        }

        if (!AudioFormats.IsSupported(path))
        {
            var extension = Path.GetExtension(path).TrimStart('.');
            SetStatus(
                StatusKind.Error,
                string.IsNullOrWhiteSpace(extension)
                    ? $"Файл не является аудиофайлом. Поддерживаются: {AudioFormats.HumanReadableList}."
                    : $"Формат «{extension.ToUpperInvariant()}» не поддерживается. Доступны: {AudioFormats.HumanReadableList}.");
            return;
        }

        Mode = AudioInputMode.File;
        SelectedFilePath = path;
        SetStatus(StatusKind.Idle, $"Выбран файл: {Path.GetFileName(path)}");
    }

    private void AddHistoryItem(string path, string text, string? state)
    {
        History.Insert(0, HistoryItem.Create(path, text, state));

        while (History.Count > MaxHistoryItems)
        {
            History.RemoveAt(History.Count - 1);
        }

        OnPropertyChanged(nameof(HasHistory));
        ClearHistoryCommand.NotifyCanExecuteChanged();
    }

    private void RefreshDeviceList()
    {
        var previousDeviceNumber = SelectedDevice?.DeviceNumber;

        Devices.Clear();
        foreach (var device in _recorderService.GetDevices())
        {
            Devices.Add(device);
        }

        SelectedDevice = Devices.FirstOrDefault(device => device.DeviceNumber == previousDeviceNumber)
            ?? Devices.FirstOrDefault();

        ToggleRecordingCommand.NotifyCanExecuteChanged();

        if (Devices.Count == 0)
        {
            if (Mode == AudioInputMode.Microphone)
            {
                SetStatus(StatusKind.Error, "Микрофон не найден. Подключите устройство записи и нажмите «Обновить».");
            }
        }
    }

    private void DeleteRecording(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Файл может быть занят — удалить не удалось.
        }
    }

    private void SetStatus(StatusKind kind, string message)
    {
        StatusKind = kind;
        StatusText = message;
    }

    partial void OnModeChanged(AudioInputMode value)
    {
        OnPropertyChanged(nameof(IsFileMode));
        OnPropertyChanged(nameof(IsMicrophoneMode));

        if (value == AudioInputMode.Microphone)
        {
            RefreshDeviceList();
        }
    }

    partial void OnSelectedDeviceChanged(AudioDeviceInfo? value)
    {
        ToggleRecordingCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedHistoryItemChanged(HistoryItem? value)
    {
        if (value is null)
        {
            return;
        }

        ResultState = value.State;
        ResultText = value.Text;
        SetStatus(StatusKind.Idle, $"Показан результат: {value.FileName} ({value.TimeText}).");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _recordingTimer.Stop();
        _recordingTimer.Tick -= OnRecordingTimerTick;
        _recorderService.LevelChanged -= OnLevelChanged;

        try
        {
            if (_recorderService.IsRecording)
            {
                _recorderService.StopAsync().GetAwaiter().GetResult();
            }
        }
        catch
        {
            // ignore
        }

        _recognitionCts?.Cancel();
        _recognitionCts?.Dispose();
        _recognitionCts = null;

        _recorderService.Dispose();
        (_recognitionService as IDisposable)?.Dispose();
    }
}
