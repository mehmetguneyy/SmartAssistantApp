using System.ComponentModel.DataAnnotations;

namespace SmartAssistant.API.Models;

// Yeni görev eklerken istemciden beklediğimiz veri
public class CreateTaskDto
{
    [Required(ErrorMessage = "Görev başlığı zorunludur.")]
    [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
    public string Category { get; set; } = "Genel";
}

// Görevi güncellerken istemciden beklediğimiz veri
public class UpdateTaskDto
{
    [Required(ErrorMessage = "Görev başlığı zorunludur.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Category { get; set; } = "Genel";
    public bool IsCompleted { get; set; }
}

public class SubtaskBreakdownDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public List<string> Subtasks { get; set; } = new();
}

public class TaskStatsDto
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int PendingTasks { get; set; }
    public int HighPriorityPending { get; set; }
    public Dictionary<string, int> TasksByCategory { get; set; } = new();
}

public class BulkActionDto
{
    public List<int> TaskIds { get; set; } = new();
}

public class BulkActionResultDto
{
    public int AffectedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class TimeEstimationDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public int EstimatedMinutes { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}