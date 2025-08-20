using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class VirtualizedDiffResult
{
    public string LeftFilePath { get; set; } = string.Empty;
    public string RightFilePath { get; set; } = string.Empty;
    public long LeftFileSize { get; set; }
    public long RightFileSize { get; set; }
    public int TotalLineCount { get; set; }
    public int ViewportSize { get; set; } = 1000; // 表示する行数
    public int CurrentViewportStart { get; set; } = 0;
    public List<DiffChunk> Chunks { get; set; } = [];
    public DiffStatistics Statistics { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public bool IsVirtualized => TotalLineCount > ViewportSize;
}

public class DiffChunk
{
    public int StartLineNumber { get; set; }
    public int LineCount { get; set; }
    public ChangeType ChangeType { get; set; }
    public bool IsLoaded { get; set; }
    public List<LineDiff> Lines { get; set; } = [];
    public string? PreviewText { get; set; }
}

public class DiffStatistics
{
    public int TotalLines { get; set; }
    public int AddedLines { get; set; }
    public int DeletedLines { get; set; }
    public int ModifiedLines { get; set; }
    public int UnchangedLines { get; set; }
    public long ProcessingTimeMs { get; set; }
    public long MemoryUsageMB { get; set; }
    public bool WasVirtualized { get; set; }
    
    public double ChangePercentage => TotalLines > 0 
        ? (AddedLines + DeletedLines + ModifiedLines) * 100.0 / TotalLines 
        : 0.0;
}

public class DiffProcessingOptions
{
    public int BufferSize { get; set; } = 8192;
    public int MaxLineLength { get; set; } = 10000;
    public bool EnableProgressReporting { get; set; } = true;
    public bool UseMemoryMapping { get; set; } = false; // 超大ファイル用
    public int ChunkSize { get; set; } = 1000; // 仮想化時のチャンクサイズ
}

public class DiffProgress
{
    public int ProcessedLines { get; set; }
    public int TotalLines { get; set; }
    public long ElapsedMs { get; set; }
    public string CurrentOperation { get; set; } = string.Empty;
    public double ProgressPercentage => TotalLines > 0 ? ProcessedLines * 100.0 / TotalLines : 0.0;
}