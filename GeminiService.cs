using SmartAssistant.API.Data;
using SmartAssistant.API.Entities;
using SmartAssistant.API.Models;
using System.Text;
using System.Text.Json;

namespace SmartAssistant.API.Services;

public class GeminiService : IAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly AppDbContext _context;

    public GeminiService(HttpClient httpClient, IConfiguration configuration, AppDbContext context)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
        _context = context;
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

    public async Task<TaskItem> CreateTaskFromTextAsync(string naturalLanguageInput)
    {
        // 1. Yapay zekâ ile metni DTO'ya dönüştür
        var dto = await ParseTaskFromTextAsync(naturalLanguageInput);

        // 2. Entity nesnesini oluştur
        var taskItem = new TaskItem
        {
            Title = string.IsNullOrWhiteSpace(dto.Title) ? naturalLanguageInput : dto.Title,
            Description = dto.Description,
            DueDate = dto.DueDate,
            Priority = string.IsNullOrWhiteSpace(dto.Priority) ? "Medium" : dto.Priority,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "Genel" : dto.Category,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        // 3. Veritabanına kaydet
        _context.Tasks.Add(taskItem);
        await _context.SaveChangesAsync();

        return taskItem;
    }

    public async Task<string> PrioritizeTasksAsync(IEnumerable<TaskItem> pendingTasks)
    {
        var tasksSummary = JsonSerializer.Serialize(pendingTasks.Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.DueDate,
            t.Priority,
            t.Category
        }));

        var prompt = $@"
Sen akıllı bir üretkenlik ve görev yönetimi asistanısın.
Aşağıda kullanıcının henüz tamamlanmamış görev listesi JSON olarak verilmiştir:

{tasksSummary}

Bugünün tarihi: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.

Lütfen bu görevleri önem ve teslim tarihine göre analiz et.
Kullanıcıya samimi, motive edici ve net bir dille:
1. Bugün/İlk olarak hangi göreve odaklanması gerektiğini,
2. Görevleri hangi sırayla yapmasının en verimli olacağını,
3. Varsa acil riskleri (yaklaşan teslim tarihi vb.) maddeler halinde özetle.";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        return await CallGeminiApiAsync(requestBody);
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