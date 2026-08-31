using Microsoft.EntityFrameworkCore;
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
    public async Task<TaskEnrichmentDto> EnrichTaskAsync(int taskId, string taskTitle, string? description, string category)
    {
        var prompt = $@"
Kullanıcının verilen görevini incele. Bu görevi tamamlamayı kolaylaştırmak için:
1. Zorluk derecesini belirle ('Easy', 'Medium', 'Hard').
2. Görevle ilgili 3-4 adet kısa ve anlamlı etiket (Tag) öner.
3. Görevi verimli bitirmesi için 2-3 adet hap ipucu/tavsiye (ActionTip) üret.

SADECE aşağıdaki JSON formatında yanıt ver. Markdown etiketi veya ek metin ekleme:
{{
  ""difficultyLevel"": ""Medium"",
  ""suggestedTags"": [""#Ders"", ""#Fizik"", ""#Sınav""] ,
  ""actionTips"": [
    ""Önceki yılların çıkmış sorularını çözün."",
    ""Formül kağıdı hazırlayarak çalışın.""
  ]
}}

Görev Başlığı: ""{taskTitle}""
Görev Açıklaması: ""{description ?? "Açıklama yok"}""
Kategori: ""{category}""";

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

            using var doc = JsonDocument.Parse(cleanedJson);
            var root = doc.RootElement;

            var tags = new List<string>();
            if (root.TryGetProperty("suggestedTags", out var tagsElem))
            {
                tags = JsonSerializer.Deserialize<List<string>>(tagsElem.GetRawText()) ?? new();
            }

            var tips = new List<string>();
            if (root.TryGetProperty("actionTips", out var tipsElem))
            {
                tips = JsonSerializer.Deserialize<List<string>>(tipsElem.GetRawText()) ?? new();
            }

            return new TaskEnrichmentDto
            {
                TaskId = taskId,
                TaskTitle = taskTitle,
                DifficultyLevel = root.GetProperty("difficultyLevel").GetString() ?? "Medium",
                SuggestedTags = tags,
                ActionTips = tips
            };
        }
        catch
        {
            return new TaskEnrichmentDto
            {
                TaskId = taskId,
                TaskTitle = taskTitle,
                DifficultyLevel = "Medium",
                SuggestedTags = new List<string> { "#Genel", "#Plan" },
                ActionTips = new List<string> { "Görevi küçük parçalara bölerek başlayın." }
            };
        }
    }

    public async Task<HabitRecommendationResultDto> RecommendHabitsAsync(IEnumerable<TaskItem> allTasks)
    {
        var tasksSummary = allTasks.Select(t => new
        {
            t.Title,
            t.Category,
            t.IsCompleted
        }).ToList();

        var prompt = $@"
Aşağıdaki görev listesini incele:
{JsonSerializer.Serialize(tasksSummary)}

Bu kullanıcının görev geçmişine göre 3 adet sürdürülebilir alışkanlık/rutin önerisi üret.
SADECE geçerli bir JSON nesnesi döndür. Markdown (```json) etiketi veya fazladan hiçbir açıklama ekleme.

Format:
{{
  ""habitAnalysisSummary"": ""Görev geçmişinize göre spor, yazılım ve planlama odaklı alışkanlıklar önerilmiştir."",
  ""recommendedHabits"": [
    {{
      ""title"": ""Haftalık Kod Gözden Geçirme"",
      ""category"": ""Yazılım"",
      ""frequency"": ""Weekly"",
      ""bestTimeOfDay"": ""Morning"",
      ""reason"": ""Teknik kaliteyi artırmak için.""
    }}
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
            var result = JsonSerializer.Deserialize<HabitRecommendationResultDto>(cleanedJson, options);
            if (result != null && result.RecommendedHabits != null && result.RecommendedHabits.Any())
            {
                return result;
            }

            // Eğer doğrudan deserialize olmadıysa manuel parse dene
            using var doc = JsonDocument.Parse(cleanedJson);
            var root = doc.RootElement;
            var summary = root.GetProperty("habitAnalysisSummary").GetString() ?? "Alışkanlık analizi tamamlandı.";
            var list = JsonSerializer.Deserialize<List<RecommendedHabitDto>>(root.GetProperty("recommendedHabits").GetRawText(), options);

            return new HabitRecommendationResultDto
            {
                HabitAnalysisSummary = summary,
                RecommendedHabits = list ?? new List<RecommendedHabitDto>()
            };
        }
        catch (Exception ex)
        {
            return new HabitRecommendationResultDto
            {
                HabitAnalysisSummary = $"Analiz Hatası: {ex.Message} | Ham Yanıt: {responseText}",
                RecommendedHabits = new List<RecommendedHabitDto>()
            };
        }
    }

    public async Task<GoalPlanningResultDto> PlanGoalAndMilestonesAsync(GoalPlanningRequestDto request)
    {
        var prompt = $@"
Sen profesyonel bir proje yöneticisi ve stratejik hedef planlama koçusun.
Kullanıcının belirlediği büyük hedef şu şekildedir:
- Hedef: {request.GoalTitle}
- Hedeflenen Süre: {request.TargetDuration ?? "Belirtilmedi"}
- Ek Açıklama / Kapsam: {request.AdditionalDetails ?? "Yok"}

Bu hedefi inceleyerek:
1. SMART prensiplerine uygun stratejik bir özet oluştur.
2. Hedefi 3-4 mantıksal aşamaya/kilometre taşına (Milestone) böl.
3. Her kilometre taşı için sıra numarası, başlık, tahmini süre, somut başarı kriteri (Success Criteria) ve doğrudan uygulanabilir 2-3 alt aksiyon görevi (ActionTasks) tanımla.

SADECE aşağıdaki JSON formatında yanıt ver. Markdown (```json) etiketi veya başka açıklama ekleme:
{{
  ""goalTitle"": ""{request.GoalTitle}"",
  ""strategicSummary"": ""Hedefe ulaşmak için belirlenen stratejik yol haritası özeti."",
  ""milestones"": [
    {{
      ""milestoneOrder"": 1,
      ""milestoneTitle"": ""Temel Gereksinimlerin ve Mimarinin Belirlenmesi"",
      ""estimatedDuration"": ""1 Hafta"",
      ""successCriteria"": ""Mimari şablonun çıkarılması ve ortamın kurulması"",
      ""actionTasks"": [
        ""Gerekli kütüphaneleri araştır ve listele"",
        ""Geliştirme ortamını hazırla""
      ]
    }}
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
            var result = JsonSerializer.Deserialize<GoalPlanningResultDto>(cleanedJson, options);
            return result ?? new GoalPlanningResultDto { GoalTitle = request.GoalTitle, StrategicSummary = "Hedef planlandı." };
        }
        catch (Exception ex)
        {
            return new GoalPlanningResultDto
            {
                GoalTitle = request.GoalTitle,
                StrategicSummary = $"Hedef ayrıştırma sırasında hata oluştu: {ex.Message} | Ham yanıt: {responseText}",
                Milestones = new List<MilestoneDto>()
            };
        }
    }

    public async Task<TaskRiskAnalysisResultDto> AnalyzeTaskRisksAndProcrastinationAsync()
    {
        var pendingTasks = await _context.Tasks
            .Where(t => !t.IsCompleted)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                t.Category,
                t.Priority,
                DueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd HH:mm") : "Belirtilmemiş"
            })
            .ToListAsync();

        if (!pendingTasks.Any())
        {
            return new TaskRiskAnalysisResultDto
            {
                GeneralAssessment = "Sistemde analiz edilecek bekleyen aktif görev bulunmamaktadır.",
                HighRiskTasks = new List<ProcrastinationRiskDto>()
            };
        }

        var tasksJson = JsonSerializer.Serialize(pendingTasks);

        var prompt = $@"
Sen üretkenlik ve erteleme (procrastination) üzerine uzmanlaşmış bir AI koçusun.
Aşağıdaki bekleyen görevleri teslim tarihleri, öncelikleri ve karmaşıklıklarına göre analiz et:
{tasksJson}

Gereksinimler:
1. Genel durum değerlendirmesi (GeneralAssessment) yap.
2. Risk taşıyan veya ertelenme ihtimali yüksek görevleri tespit et.
3. Her riskli görev için:
   - Risk Seviyesi (RiskLevel: 'Critical', 'Moderate', 'Low'),
   - Riskin teknik/zaman gerekçesi (RiskReason),
   - Kullanıcıyı ertelemeye iten olası psikolojik bariyer (ProcrastinationTrigger - örn. belirsizlik, büyük iş yükü, mükemmeliyetçilik),
   - Kullanıcının göreve hemen başlamasını sağlayacak somut, çok basit 5 dakikalık ilk adım (FiveMinuteMicroAction).

SADECE aşağıdaki JSON formatında yanıt ver. Markdown etiketi veya açıklama ekleme:
{{
  ""generalAssessment"": ""Genel risk durumu özeti..."",
  ""highRiskTasks"": [
    {{
      ""taskId"": 1,
      ""taskTitle"": ""Görev Başlığı"",
      ""riskLevel"": ""Critical"",
      ""riskReason"": ""Teslim tarihi yakın ve yüksek öncelikli."",
      ""procrastinationTrigger"": ""Görevin kapsamının geniş görünmesi."",
      ""fiveMinuteMicroAction"": ""Sadece ilk taslak için 3 maddelik bir not defteri aç.""
    }}
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
            var result = JsonSerializer.Deserialize<TaskRiskAnalysisResultDto>(cleanedJson, options);
            return result ?? new TaskRiskAnalysisResultDto { GeneralAssessment = "Risk analizi tamamlandı." };
        }
        catch (Exception ex)
        {
            return new TaskRiskAnalysisResultDto
            {
                GeneralAssessment = $"Risk analizi sırasında hata oluştu: {ex.Message} | Ham yanıt: {responseText}",
                HighRiskTasks = new List<ProcrastinationRiskDto>()
            };
        }
    }

    public async Task<WeeklyRetrospectiveResultDto> GenerateWeeklyRetrospectiveAsync()
    {
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);

        var tasks = await _context.Tasks
            .Where(t => t.CreatedAt >= oneWeekAgo || (t.DueDate.HasValue && t.DueDate.Value >= oneWeekAgo) || !t.IsCompleted)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Category,
                t.Priority,
                t.IsCompleted,
                DueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd HH:mm") : "Belirtilmemiş"
            })
            .ToListAsync();

        if (!tasks.Any())
        {
            return new WeeklyRetrospectiveResultDto
            {
                ProductivityScore = 100,
                WeeklySummary = "Son bir haftaya ait analiz edilecek görev verisi bulunmamaktadır.",
                KeyAchievements = new List<string> { "Sistemde yeni bir çalışma dönemi başlatıldı." }
            };
        }

        var tasksJson = JsonSerializer.Serialize(tasks);

        var prompt = $@"
Sen çevik (Agile) yönetim ve kişisel verimlilik üzerine uzmanlaşmış kıdemli bir AI Koçusun.
Aşağıda kullanıcının son 1 haftadaki görev kayıtları yer almaktadır:
{tasksJson}

Gereksinimler:
1. Görevlerin tamamlanma durumuna, öncelik dağılımına ve teslim tarihlerine göre 0-100 arasında bir 'productivityScore' belirle.
2. Haftalık genel durum özeti (weeklySummary) yaz.
3. Öne çıkan başarıları ve tamamlanan önemli işleri listele (keyAchievements).
4. Zaman yönetiminde yaşanan aksamaları, ötelenen veya tamamlanamayan görevlerin oluşturduğu darboğazları tespit et (bottlenecksAndChallenges).
5. Gelecek hafta performansı artırmak için 3 adet somut aksiyon adımı öner (nextWeekActionPlan).
6. Öne çıkan kategorilere özel kısa analizler üret (categoryInsights).

SADECE aşağıdaki JSON formatında yanıt ver. Markdown etiketi veya açıklama ekleme:
{{
  ""productivityScore"": 85,
  ""weeklySummary"": ""Haftalık performans özeti..."",
  ""keyAchievements"": [""Başarı 1"", ""Başarı 2""],
  ""bottlenecksAndChallenges"": [""Darboğaz 1"", ""Darboğaz 2""],
  ""nextWeekActionPlan"": [""Eylem 1"", ""Eylem 2"", ""Eylem 3""],
  ""categoryInsights"": [
    {{
      ""category"": ""Yazılım"",
      ""observation"": ""Backend görevlerinde yüksek tamamlama oranı yakalandı.""
    }}
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
            var result = JsonSerializer.Deserialize<WeeklyRetrospectiveResultDto>(cleanedJson, options);
            return result ?? new WeeklyRetrospectiveResultDto { WeeklySummary = "Retrospektif başarıyla oluşturuldu." };
        }
        catch (Exception ex)
        {
            return new WeeklyRetrospectiveResultDto
            {
                WeeklySummary = $"Retrospektif oluşturulurken hata oluştu: {ex.Message} | Ham yanıt: {responseText}"
            };
        }
    }

    public async Task<TaskSequenceAnalysisResultDto> AnalyzeTaskDependenciesAndSequencingAsync()
    {
        var pendingTasks = await _context.Tasks
            .Where(t => !t.IsCompleted)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                t.Category,
                t.Priority,
                DueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd HH:mm") : "Belirtilmemiş"
            })
            .ToListAsync();

        if (!pendingTasks.Any())
        {
            return new TaskSequenceAnalysisResultDto
            {
                Summary = "Analiz edilecek bekleyen aktif görev bulunmamaktadır.",
                OptimalExecutionOrderTaskIds = new List<int>()
            };
        }

        var tasksJson = JsonSerializer.Serialize(pendingTasks);

        var prompt = $@"
Sen yazılım mühendisliği, proje yönetimi ve kritik yol yöntemi (CPM) konularında uzman bir AI analistisin.
Aşağıda kullanıcının sistemde bekleyen aktif görev listesi bulunmaktadır:
{tasksJson}

Gereksinimler:
1. Görev başlıkları ve açıklamalarındaki mantıksal bağları incele. Hangi görev tamamlanmadan diğerinin başlayamayacağını belirle (DependsOnTaskIds).
2. Tüm görevlerin icra edilmesi gereken en verimli ve mantıklı çalışma sırasını ID listesi olarak çıkar (OptimalExecutionOrderTaskIds).
3. Kritik yolda yer alan en hayati adımları vurgula (CriticalPathHighlights).
4. Olası tıkanma risklerini ve mantık uyuşmazlıklarını uyar olarak listele (WarningsAndBlockers).

SADECE aşağıdaki JSON formatında yanıt ver. Markdown etiketi veya ek metin ekleme:
{{
  ""summary"": ""Görev bağımlılık analizi özeti..."",
  ""optimalExecutionOrderTaskIds"": [1, 3, 2],
  ""taskDependencies"": [
    {{
      ""taskId"": 2,
      ""taskTitle"": ""Görev Başlığı"",
      ""dependsOnTaskIds"": [1],
      ""dependencyReason"": ""1 numaralı görev tamamlanmadan bu işe başlanamaz.""
    }}
  ],
  ""criticalPathHighlights"": [""Kritik yol vurgusu 1""],
  ""warningsAndBlockers"": [""Uyarı veya darboğaz uyarısı""]
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
            var result = JsonSerializer.Deserialize<TaskSequenceAnalysisResultDto>(cleanedJson, options);
            return result ?? new TaskSequenceAnalysisResultDto { Summary = "Bağımlılık analizi tamamlandı." };
        }
        catch (Exception ex)
        {
            return new TaskSequenceAnalysisResultDto
            {
                Summary = $"Analiz sırasında hata oluştu: {ex.Message} | Ham yanıt: {responseText}"
            };
        }
    }

    public async Task<TaskDeduplicationResultDto> AnalyzeTaskDeduplicationAsync()
    {
        var pendingTasks = await _context.Tasks
            .Where(t => !t.IsCompleted)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                t.Category,
                t.Priority,
                DueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd HH:mm") : "Belirtilmemiş"
            })
            .ToListAsync();

        if (!pendingTasks.Any())
        {
            return new TaskDeduplicationResultDto
            {
                Summary = "Sistemde analiz edilecek bekleyen görev bulunmamaktadır.",
                RedundantTaskCount = 0,
                DuplicateGroups = new List<DuplicateTaskGroupDto>()
            };
        }

        var tasksJson = JsonSerializer.Serialize(pendingTasks);

        var prompt = $@"
Sen veri tekilleştirme (deduplication) ve görev optimizasyonu konusunda uzman bir AI veri mimarısın.
Aşağıda sistemde bekleyen aktif görevler yer almaktadır:
{tasksJson}

Gereksinimler:
1. Semantik olarak birbirinin aynısı, tekrarı veya çok benzeri olan görevleri grupla.
2. Her grup için mükerrer görev ID'lerini listele (duplicateTaskIds).
3. Grubun ortak temasını (groupTheme) ve benzerlik gerekçesini (similarityReason) açıkla.
4. Bu görevlerin birleştirilmesi durumunda önerilen tek bir optimize görev başlığı (suggestedConsolidatedTitle) ve açıklaması (suggestedConsolidatedDescription) üret.
5. Uygun aksiyon tavsiyesi ver (recommendedAction: 'Merge', 'DeleteDuplicates', 'KeepSeparate').
6. Genel temizlik tavsiyelerini listele (cleanUpRecommendations).

SADECE aşağıdaki JSON formatında yanıt ver. Markdown etiketi veya ek metin ekleme:
{{
  ""summary"": ""Mükerrer görev analizi özeti..."",
  ""redundantTaskCount"": 3,
  ""duplicateGroups"": [
    {{
      ""duplicateTaskIds"": [7, 16],
      ""groupTheme"": ""Staj Raporlama Süreci"",
      ""similarityReason"": ""Aynı haftalık rapor işi birden fazla kez sisteme girilmiş."",
      ""suggestedConsolidatedTitle"": ""Haftalık Staj Raporunu Hazırla ve Deftere İşle"",
      ""suggestedConsolidatedDescription"": ""Tüm haftalık kazanımları toparlayıp defter formatına aktar."",
      ""recommendedAction"": ""Merge""
    }}
  ],
  ""cleanUpRecommendations"": [""Tavsiye 1"", ""Tavsiye 2""]
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
            var result = JsonSerializer.Deserialize<TaskDeduplicationResultDto>(cleanedJson, options);
            return result ?? new TaskDeduplicationResultDto { Summary = "Mükerrer görev analizi tamamlandı." };
        }
        catch (Exception ex)
        {
            return new TaskDeduplicationResultDto
            {
                Summary = $"Analiz sırasında hata oluştu: {ex.Message} | Ham yanıt: {responseText}"
            };
        }
    }

    public async Task<SmartSearchResultDto> SearchTasksWithNaturalLanguageAsync(string userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return new SmartSearchResultDto
            {
                InterpretedIntent = "Boş sorgu",
                SearchSummary = "Lütfen arama yapmak için bir ifade giriniz.",
                MatchedTasks = new List<MatchedTaskItemDto>()
            };
        }

        var tasks = await _context.Tasks
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                t.Category,
                t.Priority,
                t.IsCompleted,
                DueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd HH:mm") : "Belirtilmemiş"
            })
            .ToListAsync();

        if (!tasks.Any())
        {
            return new SmartSearchResultDto
            {
                InterpretedIntent = userQuery,
                SearchSummary = "Sistemde taranacak görev bulunmamaktadır.",
                MatchedTasks = new List<MatchedTaskItemDto>()
            };
        }

        var tasksJson = JsonSerializer.Serialize(tasks);

        var prompt = $@"
Sen doğal dil anlama (NLU) ve anlamsal arama (semantic search) konusunda uzman bir AI motorusun.
Kullanıcının Arama Sorgusu: ""{userQuery}""

Veritabanındaki Görev Listesi:
{tasksJson}

Gereksinimler:
1. Kullanıcının arama niyetini ve aradığı kriterleri özetle (InterpretedIntent).
2. Bu niyetle anlamsal olarak uyuşan veya doğrudan eşleşen görevleri bul.
3. Eşleşen her görev için:
   - 1-100 arası alaka puanı (RelevanceScore),
   - Görevin bu aramayla neden eşleştiğini açıklayan kısa gerekçe (MatchReason).
4. Görevleri en yüksek alaka puanından en düşüğe doğru sırala.
5. Genel bir arama sonucu özeti (SearchSummary) yaz.

SADECE aşağıdaki JSON formatında yanıt ver. Markdown etiketi veya ek metin ekleme:
{{
  ""interpretedIntent"": ""Kullanıcı yazılım alanındaki yüksek öncelikli işleri arıyor."",
  ""searchSummary"": ""Toplam 3 ilgili görev bulundu."",
  ""matchedTasks"": [
    {{
      ""taskId"": 5,
      ""taskTitle"": "".NET API Dokümantasyonu"",
      ""category"": ""Yazılım"",
      ""priority"": ""High"",
      ""relevanceScore"": 95,
      ""matchReason"": ""Sorgudaki yazılım ve yüksek öncelik kriterlerine tam uymaktadır.""
    }}
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
            var result = JsonSerializer.Deserialize<SmartSearchResultDto>(cleanedJson, options);
            return result ?? new SmartSearchResultDto { SearchSummary = "Arama tamamlandı." };
        }
        catch (Exception ex)
        {
            return new SmartSearchResultDto
            {
                InterpretedIntent = userQuery,
                SearchSummary = $"Arama sırasında hata oluştu: {ex.Message} | Ham yanıt: {responseText}",
                MatchedTasks = new List<MatchedTaskItemDto>()
            };
        }
    }


}