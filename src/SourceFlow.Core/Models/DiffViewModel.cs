using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class DiffViewModel
{
    public FileDiffResult? DiffResult { get; set; }
    public DiffViewMode ViewMode { get; set; } = DiffViewMode.SideBySide;
    public bool ShowLineNumbers { get; set; } = true;
    public bool ShowWhitespace { get; set; } = false;
    public string SyntaxHighlighting { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public bool IsCaseSensitive { get; set; } = false;
    public List<SearchResult> SearchResults { get; set; } = [];
    public int CurrentSearchIndex { get; set; } = -1;
    public ScrollSyncSettings ScrollSync { get; set; } = new();
}

public enum DiffViewMode
{
    SideBySide,
    Inline,
    Unified
}

public class SearchResult
{
    public int LineNumber { get; set; }
    public int CharIndex { get; set; }
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public string MatchedText { get; set; } = string.Empty;
    public string ContextLine { get; set; } = string.Empty;
    public FileType FileType { get; set; }
    public ChangeType ChangeType { get; set; }
    public int CharacterPosition { get; set; }
    public string FilePath { get; set; } = string.Empty;
    
    [Obsolete("Use FileType instead")]
    public bool IsLeftSide 
    { 
        get => FileType == FileType.Left; 
        set => FileType = value ? FileType.Left : FileType.Right; 
    }
}

public class ReplaceResult
{
    public int ReplacedCount { get; set; }
    public List<ReplaceMatch> Matches { get; set; } = [];
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ReplaceMatch
{
    public int LineNumber { get; set; }
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public string OriginalText { get; set; } = string.Empty;
    public string ReplacedText { get; set; } = string.Empty;
    public FileType FileType { get; set; }
}

public enum FileType
{
    Left,
    Right,
    Both
}

public class ScrollSyncSettings
{
    public bool IsSyncEnabled { get; set; } = true;
    public double LeftScrollOffset { get; set; }
    public double RightScrollOffset { get; set; }
}

public class SyntaxHighlightingConfig
{
    public string Name { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string XshdResource { get; set; } = string.Empty;
    public Dictionary<string, string> ColorScheme { get; set; } = [];
    public bool IsBuiltIn { get; set; }
}