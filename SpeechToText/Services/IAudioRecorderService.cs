using SpeechToText.Models;

namespace SpeechToText.Services;

public interface IAudioRecorderService : IDisposable
{
    bool IsRecording { get; }

    /// <summary>Уровень сигнала от 0 до 1, вызывается из потока записи.</summary>
    event EventHandler<double>? LevelChanged;

    IReadOnlyList<AudioDeviceInfo> GetDevices();

    void Start(int deviceNumber);

    /// <summary>Асинхронно останавливает запись и возвращает путь к WAV-файлу (null, если запись не удалась).</summary>
    Task<string?> StopAsync();
}
