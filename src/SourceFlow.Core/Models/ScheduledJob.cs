using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class ScheduledJob
{
    public int Id { get; set; }
    public string JobName { get; set; } = "";
    public string Description { get; set; } = "";
    public string CronExpression { get; set; } = "";
    public ScheduleStatus Status { get; set; } = ScheduleStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastExecutionTime { get; set; }
    public DateTime? NextExecutionTime { get; set; }
    public string ReleaseConfigurationJson { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public string? LastErrorMessage { get; set; }
}

public class ScheduleExecution
{
    public int Id { get; set; }
    public int ScheduledJobId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public ScheduleExecutionStatus Status { get; set; } = ScheduleExecutionStatus.Running;
    public string? ErrorMessage { get; set; }
    public int FilesProcessed { get; set; }
    public string? BackupPath { get; set; }
    public int RetryCount { get; set; }
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
}

public class ScheduleStatistics
{
    public int TotalJobs { get; set; }
    public int ActiveJobs { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public double SuccessRate => TotalExecutions > 0 ? (SuccessfulExecutions * 100.0 / TotalExecutions) : 0;
}