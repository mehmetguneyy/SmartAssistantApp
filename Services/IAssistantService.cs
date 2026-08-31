using SmartAssistant.API.Entities;
using SmartAssistant.API.Models;

namespace SmartAssistant.API.Services;

public interface IAssistantService
{
    // Doðal dil metnini analiz edip görev nesnesine dönüþtürür
    Task<CreateTaskDto> ParseTaskFromTextAsync(string naturalLanguageInput);

    // Kullanýcýnýn sorusuna yanýt üretir (Asistan sohbeti için)
    Task<string> ChatWithAssistantAsync(string userMessage);
    // Doðal dille gelen görevi analiz edip doðrudan DB'ye ekler
    Task<TaskItem> CreateTaskFromTextAsync(string naturalLanguageInput);

    // Bekleyen görevleri analiz edip öncelik sýralamasý ve tavsiye üretir
    Task<string> PrioritizeTasksAsync(IEnumerable<TaskItem> pendingTasks);

    // Tamamlanan ve geciken görevleri analiz edip verimlilik raporu üretir
    Task<string> GenerateProductivitySummaryAsync(IEnumerable<TaskItem> allTasks);

    // Alt görevleri bölen metot imzasý
    Task<List<string>> DecomposeTaskAsync(string taskTitle, string? description);

    // Görevin tahmini tamamlanma süresini hesaplayan metot
    Task<TimeEstimationDto> EstimateTaskDurationAsync(int taskId, string taskTitle, string? description);

    // Günün görevlerini saat saat zaman bloklarýna ayýran metot
    Task<DailyScheduleResultDto> GenerateDailyScheduleAsync(DateTime targetDate, IEnumerable<TaskItem> tasks);

    // Belirli bir gündeki görev yoðunluðunu ve çakýþmalarý analiz eden metot
    Task<WorkloadAnalysisDto> AnalyzeWorkloadAndConflictsAsync(DateTime targetDate, IEnumerable<TaskItem> tasksOnDate);

    // Görevi analiz edip etiketler, zorluk seviyesi ve pratik ipuçlarý ekleyen metot
    Task<TaskEnrichmentDto> EnrichTaskAsync(int taskId, string taskTitle, string? description, string category);

    // Kullanýcýnýn görev geçmiþine göre periyodik alýþkanlýklar ve tekrarlayan görevler öneren metot
    Task<HabitRecommendationResultDto> RecommendHabitsAsync(IEnumerable<TaskItem> allTasks);

    // Kullanýcýnýn büyük hedeflerini analiz edip kilometre taþlarýna (milestones) ve alt aksiyon görevlerine bölen yapay zekâ planlama servisib
    Task<GoalPlanningResultDto> PlanGoalAndMilestonesAsync(GoalPlanningRequestDto request);

    // Bekleyen görevlerin teslim tarihi risklerini ve erteleme (procrastination) eðilimlerini analiz ederek 5 dakikalýk mikro baþlangýç adýmlarý üreten AI koçluk servisi
    Task<TaskRiskAnalysisResultDto> AnalyzeTaskRisksAndProcrastinationAsync();

    // Son bir haftadaki tamamlanan ve bekleyen görevleri analiz ederek verimlilik skoru, baþarýlar ve geliþim tavsiyeleri içeren haftalýk retrospektif üreten AI koçluk servisi
    Task<WeeklyRetrospectiveResultDto> GenerateWeeklyRetrospectiveAsync();

    // Bekleyen görevler arasýndaki mantýksal baðýmlýlýklarý analiz ederek ideal icra sýrasýný ve kritik yolu belirleyen AI servis katmaný
    Task<TaskSequenceAnalysisResultDto> AnalyzeTaskDependenciesAndSequencingAsync();

    // Sistemdeki mükerrer ve benzer görevleri tespit ederek birleþtirme ve temizleme önerileri sunan AI analiz servisi
    Task<TaskDeduplicationResultDto> AnalyzeTaskDeduplicationAsync();
}