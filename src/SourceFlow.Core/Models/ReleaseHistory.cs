using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class ReleaseHistory
{
    public int Id { get; set; }
    public string ReleaseName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public int FilesReleased { get; set; }
    public string? BackupPath { get; set; }
    public SyncStatus Status { get; set; }
}