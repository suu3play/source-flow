using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class LineDiff
{
    public int LineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
    public int? CorrespondingLineNumber { get; set; }
    public string? OriginalContent { get; set; }
    public List<CharacterDiff> CharacterDiffs { get; set; } = [];
}

public class CharacterDiff
{
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public ChangeType ChangeType { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class FileDiffResult
{
    public string LeftFilePath { get; set; } = string.Empty;
    public string RightFilePath { get; set; } = string.Empty;
    public List<LineDiff> LeftLines { get; set; } = [];
    public List<LineDiff> RightLines { get; set; } = [];
    public string LeftFileContent { get; set; } = string.Empty;
    public string RightFileContent { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public bool HasChanges => LeftLines.Any(l => l.ChangeType != ChangeType.NoChange) || 
                             RightLines.Any(l => l.ChangeType != ChangeType.NoChange);
}