namespace SmartAssistant.API.Models
{
    public class ErrorResponseDto
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? DetailedError { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
