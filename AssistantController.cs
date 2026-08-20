using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAssistant.API.Data;
using SmartAssistant.API.Entities;
using SmartAssistant.API.Models;
using SmartAssistant.API.Services;

namespace SmartAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistantService;
    private readonly AppDbContext _context;

    public AssistantController(IAssistantService assistantService, AppDbContext context)
    {
        _assistantService = assistantService;
        _context = context;
    }

    [HttpPost("parse-task")]
    public async Task<ActionResult<CreateTaskDto>> ParseTask([FromBody] string naturalLanguageInput)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguageInput))
        {
            return BadRequest("Girdi metni boş olamaz.");
        }

        var parsedDto = await _assistantService.ParseTaskFromTextAsync(naturalLanguageInput);
        return Ok(parsedDto);
    }

    // 1. Yeni: Doğal dilden görevi doğrudan oluşturup DB'ye kaydeden uç nokta
    [HttpPost("quick-add")]
    public async Task<ActionResult<TaskItem>> QuickAddTask([FromBody] string naturalLanguageInput)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguageInput))
        {
            return BadRequest("Girdi metni boş olamaz.");
        }

        var createdTask = await _assistantService.CreateTaskFromTextAsync(naturalLanguageInput);
        return CreatedAtAction("QuickAddTask", new { id = createdTask.Id }, createdTask);
    }

    // 2. Yeni: Bekleyen görevleri AI ile analiz edip önceliklendirme tavsiyesi dönen uç nokta
    [HttpGet("prioritize")]
    public async Task<ActionResult<string>> PrioritizePendingTasks()
    {
        var pendingTasks = await _context.Tasks
            .Where(t => !t.IsCompleted)
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        if (!pendingTasks.Any())
        {
            return Ok(new { message = "Şu anda bekleyen tamamlanmamış bir göreviniz bulunmamaktadır." });
        }

        var advice = await _assistantService.PrioritizeTasksAsync(pendingTasks);
        return Ok(new { recommendations = advice });
    }

    [HttpPost("chat")]
    public async Task<ActionResult<string>> Chat([FromBody] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest("Mesaj boş olamaz.");
        }

        var reply = await _assistantService.ChatWithAssistantAsync(message);
        return Ok(new { response = reply });
    }
}