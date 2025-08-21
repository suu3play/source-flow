namespace SourceFlow.Core.Models;

public class ReleaseConfiguration
{
    public string ReleaseName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public bool CreateBackup { get; set; } = true;
    public List<FileComparisonResult> SelectedFiles { get; set; } = new();
}

public class ReleaseResult
{
    public string ReleaseName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
    public List<string> Errors { get; set; } = new();
    public int FilesProcessed { get; set; }
    public int ErrorsCount { get; set; }
    public int Progress { get; set; }
    public string? BackupPath { get; set; }
    
    public TimeSpan Duration => EndTime - StartTime;
}

public class ReleaseStatistics
{
    public int TotalReleases { get; set; }
    public int SuccessfulReleases { get; set; }
    public int LastWeekReleases { get; set; }
    public int TotalFilesReleased { get; set; }
    public double SuccessRate { get; set; }
}