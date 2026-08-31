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

public class TimeBlockDto
{
    public string TimeSlot { get; set; } = string.Empty; // Örn: "09:00 - 10:30"
    public string TaskTitle { get; set; } = string.Empty;
    public string Type { get; set; } = "DeepWork"; // DeepWork, ShortTask, Break
    public string Note { get; set; } = string.Empty;
}

public class DailyScheduleResultDto
{
    public DateTime TargetDate { get; set; }
    public List<TimeBlockDto> Schedule { get; set; } = new();
    public string CoachNote { get; set; } = string.Empty;
}

public class WorkloadAnalysisDto
{
    public DateTime AnalyzedDate { get; set; }
    public int TotalTasksOnDate { get; set; }
    public bool HasConflictOrOverload { get; set; }
    public string RiskLevel { get; set; } = "Low"; // Low, Medium, High
    public string AIAnalysis { get; set; } = string.Empty;
    public List<string> SuggestedAdjustments { get; set; } = new();
}

public class TaskEnrichmentDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string DifficultyLevel { get; set; } = "Medium"; // Easy, Medium, Hard
    public List<string> SuggestedTags { get; set; } = new();
    public List<string> ActionTips { get; set; } = new();
}

public class HabitRecommendationResultDto
{
    public string HabitAnalysisSummary { get; set; } = string.Empty;
    public List<RecommendedHabitDto> RecommendedHabits { get; set; } = new();
}

public class RecommendedHabitDto
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Weekly"; // Daily, Weekly, Monthly
    public string BestTimeOfDay { get; set; } = "Morning"; // Morning, Afternoon, Evening
    public string Reason { get; set; } = string.Empty;
}

public class GoalPlanningRequestDto
{
    public string GoalTitle { get; set; } = string.Empty;
    public string? TargetDuration { get; set; } // Örn: "2 Hafta", "1 Ay", "3 Ay"
    public string? AdditionalDetails { get; set; }
}

public class GoalPlanningResultDto
{
    public string GoalTitle { get; set; } = string.Empty;
    public string StrategicSummary { get; set; } = string.Empty;
    public List<MilestoneDto> Milestones { get; set; } = new();
}

public class MilestoneDto
{
    public int MilestoneOrder { get; set; }
    public string MilestoneTitle { get; set; } = string.Empty;
    public string EstimatedDuration { get; set; } = string.Empty;
    public string SuccessCriteria { get; set; } = string.Empty;
    public List<string> ActionTasks { get; set; } = new();
}

public class ProcrastinationRiskDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "Low"; // Critical, Moderate, Low
    public string RiskReason { get; set; } = string.Empty;
    public string ProcrastinationTrigger { get; set; } = string.Empty;
    public string FiveMinuteMicroAction { get; set; } = string.Empty;
}

public class TaskRiskAnalysisResultDto
{
    public string GeneralAssessment { get; set; } = string.Empty;
    public List<ProcrastinationRiskDto> HighRiskTasks { get; set; } = new();
}

public class RetrospectiveInsightDto
{
    public string Category { get; set; } = string.Empty;
    public string Observation { get; set; } = string.Empty;
}

public class WeeklyRetrospectiveResultDto
{
    public int ProductivityScore { get; set; }
    public string WeeklySummary { get; set; } = string.Empty;
    public List<string> KeyAchievements { get; set; } = new();
    public List<string> BottlenecksAndChallenges { get; set; } = new();
    public List<string> NextWeekActionPlan { get; set; } = new();
    public List<RetrospectiveInsightDto> CategoryInsights { get; set; } = new();
}

public class TaskDependencyItemDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public List<int> DependsOnTaskIds { get; set; } = new();
    public string DependencyReason { get; set; } = string.Empty;
}

public class TaskSequenceAnalysisResultDto
{
    public string Summary { get; set; } = string.Empty;
    public List<int> OptimalExecutionOrderTaskIds { get; set; } = new();
    public List<TaskDependencyItemDto> TaskDependencies { get; set; } = new();
    public List<string> CriticalPathHighlights { get; set; } = new();
    public List<string> WarningsAndBlockers { get; set; } = new();
}

public class DuplicateTaskGroupDto
{
    public List<int> DuplicateTaskIds { get; set; } = new();
    public string GroupTheme { get; set; } = string.Empty;
    public string SimilarityReason { get; set; } = string.Empty;
    public string SuggestedConsolidatedTitle { get; set; } = string.Empty;
    public string SuggestedConsolidatedDescription { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty; // Merge, DeleteDuplicates, KeepSeparate
}

public class TaskDeduplicationResultDto
{
    public string Summary { get; set; } = string.Empty;
    public int RedundantTaskCount { get; set; }
    public List<DuplicateTaskGroupDto> DuplicateGroups { get; set; } = new();
    public List<string> CleanUpRecommendations { get; set; } = new();
}


public class SmartSearchRequestDto
{
    public string Query { get; set; } = string.Empty;
}

public class MatchedTaskItemDto
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int RelevanceScore { get; set; } // 1-100
    public string MatchReason { get; set; } = string.Empty;
}

public class SmartSearchResultDto
{
    public string InterpretedIntent { get; set; } = string.Empty;
    public List<MatchedTaskItemDto> MatchedTasks { get; set; } = new();
    public string SearchSummary { get; set; } = string.Empty;
}

