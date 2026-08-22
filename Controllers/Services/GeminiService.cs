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

    public async Task<string> GenerateProductivitySummaryAsync(IEnumerable<TaskItem> allTasks)
    {
        var completed = allTasks.Where(t => t.IsCompleted).ToList();
        var pending = allTasks.Where(t => !t.IsCompleted).ToList();

        var summaryData = new
        {
            TotalTasks = allTasks.Count(),
            CompletedCount = completed.Count,
            PendingCount = pending.Count,
            CompletedTasks = completed.Select(t => new { t.Title, t.Category, t.Priority }),
            PendingTasks = pending.Select(t => new { t.Title, t.Category, t.DueDate, t.Priority })
        };

        var prompt = $@"
Sen profesyonel bir verimlilik koçusun.
Aşağıda kullanıcının görev verileri yer almaktadır:

{JsonSerializer.Serialize(summaryData)}

Bugünün tarihi: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC.

Lütfen kullanıcı için kapsamlı bir 'Verimlilik ve İlerleme Raporu' hazırla:
1. Genel Başarı Oranı (% kaç tamamlanmış, güçlü yanlar).
2. Hangi kategorilerde daha üretken olmuş, hangi alanlar aksamış.
3. Kalan görevleri bitirmesi için somut, pratik öneriler.
Dili yapıcı, profesyonel ve cesaretlendirici tut.";

        var requestBody = new
        {
            contents = new[]
            {
            new { parts = new[] { new { text = prompt } } }
        }
        };

        return await CallGeminiApiAsync(requestBody);
    }

    public async Task<List<string>> DecomposeTaskAsync(string taskTitle, string? description)
    {
        var prompt = $@"
Kullanıcının verilen ana görevini analiz et ve bu görevi tamamlamak için yapılması gereken 3 ila 5 adet somut, uygulanabilir ve net alt adıma (subtask) böl.
SADECE JSON string dizisi formatında yanıt ver, başka hiçbir metin veya markdown ekleme.

Format örneği:
[""1. Adım açıklaması"", ""2. Adım açıklaması"", ""3. Adım açıklaması""]

Görev Başlığı: ""{taskTitle}""
Görev Açıklaması: ""{description ?? "Açıklama yok"}""";

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
            var result = JsonSerializer.Deserialize<List<string>>(cleanedJson);
            return result ?? new List<string> { "Görevi planla ve başla", "İlerlemeyi kaydet", "Görevi tamamla" };
        }
        catch
        {
            return new List<string> { "Görevi planla ve başla", "İlerlemeyi kaydet", "Görevi tamamla" };
        }
    }

    public async Task<TimeEstimationDto> EstimateTaskDurationAsync(int taskId, string taskTitle, string? description)
    {
        var prompt = $@"
Kullanıcının verilen görevini incele ve bu görevin ortalama kaç dakika süreceğini ve gerekçesini tahmin et.
SADECE aşağıdaki formatta geçerli bir JSON nesnesi döndür, başka açıklama ekleme.

Format:
{{
  ""estimatedMinutes"": 45,
  ""reasoning"": ""Görevin kapsamı ve zorluk derecesine göre kısa açıklama.""
}}

Görev Başlığı: ""{taskTitle}""
Görev Açıklaması: ""{description ?? "Açıklama yok"}""";

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
            using var doc = JsonDocument.Parse(cleanedJson);
            var root = doc.RootElement;

            return new TimeEstimationDto
            {
                TaskId = taskId,
                TaskTitle = taskTitle,
                EstimatedMinutes = root.GetProperty("estimatedMinutes").GetInt32(),
                Reasoning = root.GetProperty("reasoning").GetString() ?? "Süre tahmini yapıldı."
            };
        }
        catch
        {
            return new TimeEstimationDto
            {
                TaskId = taskId,
                TaskTitle = taskTitle,
                EstimatedMinutes = 30,
                Reasoning = "Varsayılan ortalama süre atandı."
            };
        }
    }

    public async Task<DailyScheduleResultDto> GenerateDailyScheduleAsync(DateTime targetDate, IEnumerable<TaskItem> tasks)
    {
        var taskListSummary = JsonSerializer.Serialize(tasks.Select(t => new
        {
            t.Id,
            t.Title,
            t.Priority,
            t.Category,
            t.DueDate
        }));

        var prompt = $@"
Sen profesyonel bir zaman yönetimi ve üretkenlik asistanısın.
Aşağıda verilen görev listesini inceleyerek hedef tarih ({targetDate:yyyy-MM-dd}) için sabah 09:00 - 18:00 saatleri arasına uygun bir 'Time-Blocking' (Zaman Bloklama) günlük çalışma planı çıkar.
Mantıklı dinlenme aralıkları (Break) ve odak blokları (DeepWork) ekle.

SADECE geçerli bir JSON nesnesi döndür. Markdown (```json) etiketi veya başka açıklama metni kesinlikle ekleme.

Format:
{{
  ""targetDate"": ""{targetDate:yyyy-MM-ddTHH:mm:ssZ}"",
  ""coachNote"": ""Günün verimli geçmesi için kısa bir motivasyon/strateji notu."",
  ""schedule"": [
    {{
      ""timeSlot"": ""09:00 - 10:30"",
      ""taskTitle"": ""Görev Adı"",
      ""type"": ""DeepWork"",
      ""note"": ""Açıklama""
    }}
  ]
}}

Görevler:
{taskListSummary}";

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
            var cleanedJson = responseText.Trim();
            if (cleanedJson.StartsWith("```json")) cleanedJson = cleanedJson.Substring(7);
            if (cleanedJson.StartsWith("```")) cleanedJson = cleanedJson.Substring(3);
            if (cleanedJson.EndsWith("```")) cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
            cleanedJson = cleanedJson.Trim();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var result = JsonSerializer.Deserialize<DailyScheduleResultDto>(cleanedJson, options);
            return result ?? new DailyScheduleResultDto { TargetDate = targetDate, CoachNote = "Plan oluşturuldu." };
        }
        catch
        {
            return new DailyScheduleResultDto
            {
                TargetDate = targetDate,
                CoachNote = "Varsayılan şablon uygulandı.",
                Schedule = new List<TimeBlockDto>
            {
                new() { TimeSlot = "09:00 - 12:00", TaskTitle = "Öncelikli Görevler", Type = "DeepWork", Note = "Kesintisiz odaklanma" },
                new() { TimeSlot = "12:00 - 13:00", TaskTitle = "Öğle Molası", Type = "Break", Note = "Yemek ve dinlenme" },
                new() { TimeSlot = "13:00 - 17:00", TaskTitle = "Kalan Görevler", Type = "DeepWork", Note = "Rutin işleri tamamlama" }
            }
            };
        }
    }

    public async Task<WorkloadAnalysisDto> AnalyzeWorkloadAndConflictsAsync(DateTime targetDate, IEnumerable<TaskItem> tasksOnDate)
    {
        var taskList = tasksOnDate.Select(t => new
        {
            t.Id,
            t.Title,
            t.DueDate,
            t.Priority,
            t.Category
        }).ToList();

        var prompt = $@"
Sen profesyonel bir iş yükü optimizasyonu uzmanısın.
Aşağıda kullanıcının {targetDate:yyyy-MM-dd} tarihindeki bekleyen görevleri yer almaktadır:

{JsonSerializer.Serialize(taskList)}

Bu görevleri analiz et:
1. Görev sayısı, öncelikleri veya saat çakışmaları nedeniyle bir aşırı yüklenme (overload) veya risk var mı?
2. Risk seviyesini belirle ('Low', 'Medium', 'High').
3. Kullanıcının günü daha rahat yönetmesi için görevleri nasıl ertelemesi veya dengelemesi gerektiğine dair 2-4 somut öneri maddesi sun.

SADECE aşağıdaki JSON formatında yanıt ver. Markdown etiketi veya ek metin ekleme:
{{
  ""analyzedDate"": ""{targetDate:yyyy-MM-ddTHH:mm:ssZ}"",
  ""totalTasksOnDate"": {taskList.Count},
  ""hasConflictOrOverload"": true,
  ""riskLevel"": ""High"",
  ""aiAnalysis"": ""Günün değerlendirmesi ve çakışma özeti."",
  ""suggestedAdjustments"": [
    ""1. Öneri: X görevini yarına ertele"",
    ""2. Öneri: Y görevini sabah saatine çek""
  ]
}}";

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
            var cleanedJson = responseText.Trim();
            if (cleanedJson.StartsWith("```json")) cleanedJson = cleanedJson.Substring(7);
            if (cleanedJson.StartsWith("```")) cleanedJson = cleanedJson.Substring(3);
            if (cleanedJson.EndsWith("```")) cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
            cleanedJson = cleanedJson.Trim();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<WorkloadAnalysisDto>(cleanedJson, options);
            return result ?? new WorkloadAnalysisDto { AnalyzedDate = targetDate, TotalTasksOnDate = taskList.Count, AIAnalysis = "Analiz tamamlandı." };
        }
        catch
        {
            return new WorkloadAnalysisDto
            {
                AnalyzedDate = targetDate,
                TotalTasksOnDate = taskList.Count,
                HasConflictOrOverload = taskList.Count > 3,
                RiskLevel = taskList.Count > 3 ? "Medium" : "Low",
                AIAnalysis = "Otomatik kural tabanlı analiz uygulandı.",
                SuggestedAdjustments = new List<string> { "Görevleri öncelik sırasına göre tamamlayın." }
            };
        }
    }


}