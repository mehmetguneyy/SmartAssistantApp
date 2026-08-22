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

    [HttpGet("productivity-summary")]
    public async Task<ActionResult<string>> GetProductivitySummary()
    {
        var allTasks = await _context.Tasks.ToListAsync();

        if (!allTasks.Any())
        {
            return Ok(new { message = "Rapor oluşturmak için henüz sistemde kayıtlı görev bulunmuyor." });
        }

        var report = await _assistantService.GenerateProductivitySummaryAsync(allTasks);
        return Ok(new { productivityReport = report });
    }

    [HttpPost("decompose-task/{taskId}")]
    public async Task<ActionResult<SubtaskBreakdownDto>> DecomposeTask(int taskId)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
        {
            return NotFound($"ID {taskId} olan görev bulunamadı.");
        }

        var subtasks = await _assistantService.DecomposeTaskAsync(task.Title, task.Description);

        return Ok(new SubtaskBreakdownDto
        {
            TaskId = task.Id,
            TaskTitle = task.Title,
            Subtasks = subtasks
        });
    }

    [HttpGet("estimate-duration/{taskId}")]
    public async Task<ActionResult<TimeEstimationDto>> EstimateDuration(int taskId)
    {
        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null)
        {
            return NotFound($"ID {taskId} olan görev bulunamadı.");
        }

        var estimation = await _assistantService.EstimateTaskDurationAsync(task.Id, task.Title, task.Description);
        return Ok(estimation);
    }

    [HttpGet("daily-schedule")]
    public async Task<ActionResult<DailyScheduleResultDto>> GetDailySchedule([FromQuery] DateTime? date)
    {
        var targetDate = date ?? DateTime.UtcNow.Date;

        // Tamamlanmamış görevleri al
        var pendingTasks = await _context.Tasks
            .Where(t => !t.IsCompleted)
            .ToListAsync();

        if (!pendingTasks.Any())
        {
            return Ok(new DailyScheduleResultDto
            {
                TargetDate = targetDate,
                CoachNote = "Planlanacak bekleyen görev bulunamadı. Harika bir gün!"
            });
        }

        var schedule = await _assistantService.GenerateDailyScheduleAsync(targetDate, pendingTasks);
        return Ok(schedule);
    }

    [HttpGet("workload-analysis")]
    public async Task<ActionResult<WorkloadAnalysisDto>> GetWorkloadAnalysis([FromQuery] DateTime? date)
    {
        var targetDate = (date ?? DateTime.UtcNow).Date;
        var nextDay = targetDate.AddDays(1);

        // Seçilen tarihe denk gelen tamamlanmamış görevleri filtrele
        var tasksOnDate = await _context.Tasks
            .Where(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate.Value >= targetDate && t.DueDate.Value < nextDay)
            .ToListAsync();

        if (!tasksOnDate.Any())
        {
            return Ok(new WorkloadAnalysisDto
            {
                AnalyzedDate = targetDate,
                TotalTasksOnDate = 0,
                HasConflictOrOverload = false,
                RiskLevel = "Low",
                AIAnalysis = "Bu tarihte planlanmış bekleyen bir görev bulunmamaktadır. Gününüz tamamen serbest!",
                SuggestedAdjustments = new List<string>()
            });
        }

        var analysis = await _assistantService.AnalyzeWorkloadAndConflictsAsync(targetDate, tasksOnDate);
        return Ok(analysis);
    }
}