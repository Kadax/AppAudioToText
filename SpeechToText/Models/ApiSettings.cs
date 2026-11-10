using System.IO;
using System.Text.Json;

namespace SpeechToText.Models;

/// <summary>
/// Настройки взаимодействия с сервисом распознавания.
/// </summary>
public sealed class ApiSettings
{
    public const string DefaultBaseUrl = "http://192.168.4.38:8800/";
    public const string DefaultFileFieldName = "file";

    public string BaseUrl { get; init; } = DefaultBaseUrl;

    public string FileFieldName { get; init; } = DefaultFileFieldName;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Загружает настройки из appsettings.json рядом с исполняемым файлом.
    /// При отсутствии или ошибке чтения возвращает значения по умолчанию.
    /// </summary>
    public static ApiSettings Load(string? configPath = null)
    {
        configPath ??= Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        try
        {
            if (!File.Exists(configPath))
            {
                return new ApiSettings();
            }

            using var stream = File.OpenRead(configPath);
            var document = JsonSerializer.Deserialize<SettingsDocument>(stream, SerializerOptions);
            var api = document?.Api;
            if (api is null)
            {
                return new ApiSettings();
            }

            return new ApiSettings
            {
                BaseUrl = string.IsNullOrWhiteSpace(api.BaseUrl) ? DefaultBaseUrl : api.BaseUrl.Trim(),
                FileFieldName = string.IsNullOrWhiteSpace(api.FileFieldName) ? DefaultFileFieldName : api.FileFieldName.Trim()
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ApiSettings();
        }
    }

    private sealed class SettingsDocument
    {
        public ApiSection? Api { get; set; }
    }

    private sealed class ApiSection
    {
        public string? BaseUrl { get; set; }

        public string? FileFieldName { get; set; }
    }
}
