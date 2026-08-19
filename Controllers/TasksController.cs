using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartAssistant.API.Data;
using SmartAssistant.API.Entities;
using SmartAssistant.API.Models;

namespace SmartAssistant.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly AppDbContext _context;

    // Dependency Injection ile DbContext enjekte ediliyor
    public TasksController(AppDbContext context)
    {
        _context = context;
    }

    // 1. GET: api/tasks (Tüm görevleri getir)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasks()
    {
        return await _context.Tasks
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // 2. GET: api/tasks/{id} (Tek bir görevi getir)
    [HttpGet("{id}")]
    public async Task<ActionResult<TaskItem>> GetTask(int id)
    {
        var task = await _context.Tasks.FindAsync(id);

        if (task == null)
        {
            return NotFound(new { message = $"ID'si {id} olan görev bulunamadı." });
        }

        return Ok(task);
    }

    // 3. POST: api/tasks (Yeni görev ekle)
    [HttpPost]
    public async Task<ActionResult<TaskItem>> CreateTask([FromBody] CreateTaskDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var taskItem = new TaskItem
        {
            Title = dto.Title,
            Description = dto.Description,
            DueDate = dto.DueDate,
            Priority = dto.Priority,
            Category = dto.Category,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(taskItem);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTask), new { id = taskItem.Id }, taskItem);
    }

    // 4. PUT: api/tasks/{id} (Görevi güncelle)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskDto dto)
    {
        var taskItem = await _context.Tasks.FindAsync(id);

        if (taskItem == null)
        {
            return NotFound(new { message = $"ID'si {id} olan görev bulunamadı." });
        }

        taskItem.Title = dto.Title;
        taskItem.Description = dto.Description;
        taskItem.DueDate = dto.DueDate;
        taskItem.Priority = dto.Priority;
        taskItem.Category = dto.Category;
        taskItem.IsCompleted = dto.IsCompleted;

        await _context.SaveChangesAsync();

        return Ok(taskItem);
    }

    // 5. DELETE: api/tasks/{id} (Görevi sil)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var taskItem = await _context.Tasks.FindAsync(id);

        if (taskItem == null)
        {
            return NotFound(new { message = $"ID'si {id} olan görev bulunamadı." });
        }

        _context.Tasks.Remove(taskItem);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Görev başarıyla silindi.", deletedId = id });
    }
}