namespace SpeechToText.Models;

/// <summary>
/// Устройство записи, доступное через MME.
/// </summary>
public sealed record AudioDeviceInfo(int DeviceNumber, string Name);
