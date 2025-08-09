using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class FileComparisonResult
{
    public string FilePath { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public long? SourceSize { get; set; }
    public long? TargetSize { get; set; }
    public DateTime? SourceModified { get; set; }
    public DateTime? TargetModified { get; set; }
    public bool Selected { get; set; } = true;
}