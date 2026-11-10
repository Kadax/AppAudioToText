using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using SpeechToText.Infrastructure;
using SpeechToText.Models;

namespace SpeechToText.Services;

/// <summary>
/// Отправляет аудиофайл на сервис распознавания (POST /api/Audio, multipart/form-data).
/// </summary>
public sealed class HttpAudioRecognitionService : IAudioRecognitionService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _fileFieldName;

    public HttpAudioRecognitionService(ApiSettings settings)
    {
        _baseUrl = settings.BaseUrl;
        _fileFieldName = settings.FileFieldName;

        var baseUri = new Uri(_baseUrl.EndsWith('/') ? _baseUrl : _baseUrl + "/", UriKind.Absolute);
        _httpClient = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<RecognitionResult> RecognizeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new AudioRecognitionException("Аудиофайл не найден. Выберите файл заново.");
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            using var content = new MultipartFormDataContent();
            using var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(AudioFormats.GetContentType(filePath));
            content.Add(fileContent, _fileFieldName, Path.GetFileName(filePath));

            using var response = await _httpClient.PostAsync("api/Audio", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var details = string.IsNullOrWhiteSpace(body) ? string.Empty : $" Ответ сервера: {Truncate(body, 300)}";
                var hint = (int)response.StatusCode >= 500
                    ? " Похоже, сервис не смог обработать аудиофайл — проверьте его журнал."
                    : string.Empty;

                throw new AudioRecognitionException(
                    $"Сервис вернул ошибку {(int)response.StatusCode} ({response.ReasonPhrase}).{details}{hint}");
            }

            var result = TryParse(body) ?? new RecognitionResult { Text = body, State = "Success" };

            if (string.IsNullOrWhiteSpace(result.Text))
            {
                var state = string.IsNullOrWhiteSpace(result.State) ? "Error" : result.State;
                throw new AudioRecognitionException(
                    $"Сервис не вернул текст распознавания (state: {state}). " +
                    $"Проверьте формат файла и имя поля «{_fileFieldName}» в appsettings.json.");
            }

            return result;
        }
        catch (AudioRecognitionException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            throw new AudioRecognitionException("Сервер не ответил за отведённое время. Попробуйте ещё раз.");
        }
        catch (HttpRequestException ex)
        {
            throw new AudioRecognitionException(
                $"Не удалось подключиться к сервису распознавания по адресу {_baseUrl}. " +
                "Проверьте, что сервис запущен и доступен в сети.",
                ex);
        }
        catch (IOException ex)
        {
            throw new AudioRecognitionException("Не удалось прочитать аудиофайл.", ex);
        }
    }

    private static RecognitionResult? TryParse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RecognitionResult>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";

    public void Dispose() => _httpClient.Dispose();
}
