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