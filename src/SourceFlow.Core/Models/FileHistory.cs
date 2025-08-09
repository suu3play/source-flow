using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class FileHistory
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int Version { get; set; }
    public string HashValue { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime ModifiedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public ChangeType ChangeType { get; set; }
}