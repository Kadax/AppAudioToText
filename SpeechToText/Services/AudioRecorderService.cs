using System.IO;
using NAudio.Wave;
using SpeechToText.Infrastructure;
using SpeechToText.Models;

namespace SpeechToText.Services;

/// <summary>
/// Запись с микрофона в WAV (PCM 16 бит, моно, 44,1 кГц).
/// Остановка записи и финализация файла выполняются асинхронно, вне потока интерфейса.
/// </summary>
public sealed class AudioRecorderService : IAudioRecorderService
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    private readonly object _stateSync = new();
    private readonly object _writeSync = new();
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _currentPath;
    private TaskCompletionSource? _stoppedTcs;
    private bool _disposed;

    public bool IsRecording { get; private set; }

    public event EventHandler<double>? LevelChanged;

    public IReadOnlyList<AudioDeviceInfo> GetDevices()
    {
        var devices = new List<AudioDeviceInfo>();

        try
        {
            for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                var name = string.IsNullOrWhiteSpace(capabilities.ProductName)
                    ? $"Устройство {i + 1}"
                    : capabilities.ProductName;
                devices.Add(new AudioDeviceInfo(i, name));
            }
        }
        catch
        {
            // Устройства записи могут быть недоступны — возвращаем пустой список.
        }

        return devices;
    }

    public void Start(int deviceNumber)
    {
        if (IsRecording)
        {
            return;
        }

        var path = RecordingStorage.CreateNewPath();

        var waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(44100, 16, 1),
            BufferMilliseconds = 50,
            NumberOfBuffers = 3
        };

        var writer = new WaveFileWriter(path, waveIn.WaveFormat);
        var stoppedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        waveIn.DataAvailable += OnDataAvailable;
        waveIn.RecordingStopped += (_, _) => stoppedTcs.TrySetResult();

        lock (_stateSync)
        {
            _waveIn = waveIn;
            _writer = writer;
            _currentPath = path;
            _stoppedTcs = stoppedTcs;
            IsRecording = true;
        }

        try
        {
            waveIn.StartRecording();
        }
        catch
        {
            lock (_stateSync)
            {
                DetachInternal(waveIn);
            }

            TryDelete(path);
            throw;
        }
    }

    public async Task<string?> StopAsync()
    {
        WaveInEvent? waveIn;
        TaskCompletionSource? stoppedTcs;
        string? path;

        lock (_stateSync)
        {
            waveIn = _waveIn;
            stoppedTcs = _stoppedTcs;
            path = _currentPath;
            IsRecording = false;
        }

        if (waveIn is null)
        {
            return null;
        }

        // Останавливаем устройство и ждём, пока драйвер отдаст последний буфер.
        try
        {
            await Task.Run(waveIn.StopRecording).ConfigureAwait(false);
        }
        catch
        {
            // Устройство могло быть отключено — ниже всё равно выполним очистку.
        }

        if (stoppedTcs is not null)
        {
            try
            {
                await stoppedTcs.Task.WaitAsync(StopTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Драйвер не прислал RecordingStopped — закрываем файл принудительно.
            }
            catch
            {
                // Запись уже была остановлена.
            }
        }

        // Финализация WAV (сброс буферов на диск) — отдельным потоком, чтобы не блокировать UI.
        await Task.Run(() =>
        {
            lock (_stateSync)
            {
                DetachInternal(waveIn);
            }
        }).ConfigureAwait(false);

        return path is not null && File.Exists(path) ? path : null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0)
        {
            return;
        }

        lock (_writeSync)
        {
            if (_writer is null)
            {
                return;
            }

            try
            {
                _writer.Write(e.Buffer, 0, e.BytesRecorded);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }

        double peak = 0;
        for (var i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            var sample = BitConverter.ToInt16(e.Buffer, i);
            var value = Math.Abs(sample / 32768d);
            if (value > peak)
            {
                peak = value;
            }
        }

        LevelChanged?.Invoke(this, Math.Min(1d, peak));
    }

    /// <summary>
    /// Освобождает ресурсы записи и закрывает WAV-файл.
    /// Вызывается под <see cref="_stateSync"/>. Если передан <paramref name="expectedWaveIn"/>,
    /// очистка выполняется только когда он всё ещё активен.
    /// </summary>
    private void DetachInternal(WaveInEvent? expectedWaveIn = null)
    {
        if (expectedWaveIn is not null && !ReferenceEquals(_waveIn, expectedWaveIn))
        {
            return;
        }

        var waveIn = _waveIn;
        var writer = _writer;

        _waveIn = null;
        _writer = null;
        _currentPath = null;
        _stoppedTcs = null;

        if (waveIn is not null)
        {
            waveIn.DataAvailable -= OnDataAvailable;

            try
            {
                waveIn.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        lock (_writeSync)
        {
            try
            {
                writer?.Dispose();
            }
            catch
            {
                // ignore
            }
        }
    }

    private static void TryDelete(string path)
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
            // ignore
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (IsRecording)
            {
                StopAsync().GetAwaiter().GetResult();
            }
        }
        catch
        {
            // ignore
        }
    }
}
