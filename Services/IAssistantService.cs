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
}