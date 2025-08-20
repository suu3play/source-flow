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
    public int Length { get; set; }
    public bool IsLeftSide { get; set; }
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