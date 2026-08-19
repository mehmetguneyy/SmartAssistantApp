using System.Text;
using System.Text.Json;
using SmartAssistant.API.Models;

namespace SmartAssistant.API.Services;

public class GeminiService : IAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
    }

    public async Task<CreateTaskDto> ParseTaskFromTextAsync(string naturalLanguageInput)
    {
        var prompt = $@"
Aşağıdaki kullanıcı isteğini analiz et ve görev bilgilerini çıkar.
SADECE geçerli bir JSON nesnesi döndür, markdown veya başka açıklama ekleme.

Format:
{{
  ""title"": ""görev başlığı"",
  ""description"": ""görev açıklaması veya detay"",
  ""dueDate"": ""YYYY-MM-DDTHH:mm:ssZ formatında tarih/saat veya null"",
  ""priority"": ""Low"", ""Medium"" veya ""High"",
  ""category"": ""Kategori adı (ör. Spor, Yazılım, Ders, Kişisel, İş)""
}}

Bugünün tarihi: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.

Kullanıcı Girdisi: ""{naturalLanguageInput}""";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var responseText = await CallGeminiApiAsync(requestBody);

        try
        {
            var cleanedJson = responseText.Trim().Replace("```json", "").Replace("```", "").Trim();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<CreateTaskDto>(cleanedJson, options);
            return result ?? new CreateTaskDto { Title = naturalLanguageInput };
        }
        catch
        {
            return new CreateTaskDto { Title = naturalLanguageInput };
        }
    }

    public async Task<string> ChatWithAssistantAsync(string userMessage)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = userMessage } } }
            }
        };

        return await CallGeminiApiAsync(requestBody);
    }

    private async Task<string> CallGeminiApiAsync(object requestBody)
    {
        // En güncel flash model endpoint'i
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={_apiKey}";
        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, jsonContent);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return $"AI Servisi Hatası ({response.StatusCode}): {responseBody}";
        }

        using var doc = JsonDocument.Parse(responseBody);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return text ?? string.Empty;
    }
}