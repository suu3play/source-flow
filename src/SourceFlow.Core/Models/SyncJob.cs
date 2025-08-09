using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class SyncJob
{
    public int Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string ServerHost { get; set; } = string.Empty;
    public DateTime SyncStart { get; set; }
    public DateTime? SyncEnd { get; set; }
    public SyncStatus Status { get; set; }
    public int FilesSynced { get; set; }
    public int ErrorsCount { get; set; }
    public string? LogMessage { get; set; }
}