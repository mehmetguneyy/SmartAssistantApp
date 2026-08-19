using SmartAssistant.API.Models;

namespace SmartAssistant.API.Services;

public interface IAssistantService
{
    // Doðal dil metnini analiz edip görev nesnesine dönüþtürür
    Task<CreateTaskDto> ParseTaskFromTextAsync(string naturalLanguageInput);

    // Kullanýcýnýn sorusuna yanýt üretir (Asistan sohbeti için)
    Task<string> ChatWithAssistantAsync(string userMessage);
}