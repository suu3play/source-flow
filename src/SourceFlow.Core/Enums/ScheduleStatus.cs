namespace SourceFlow.Core.Enums;

public enum ScheduleStatus
{
    Active,
    Inactive,
    Paused,
    Error,
    Completed
}

public enum ScheduleExecutionStatus
{
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled,
    Retrying
}