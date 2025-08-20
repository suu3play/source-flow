using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface IAdvancedDiffEngine : ITextDiffEngine
{
    Task<VirtualizedDiffResult> ProcessLargeFileDiffAsync(
        string leftFilePath, 
        string rightFilePath, 
        DiffProcessingOptions? options = null,
        IProgress<DiffProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<List<LineDiff>> LoadChunkAsync(
        VirtualizedDiffResult diffResult, 
        int chunkIndex,
        CancellationToken cancellationToken = default);

    Task<VirtualizedDiffResult> ProcessLargeContentDiffAsync(
        Stream leftStream, 
        Stream rightStream, 
        string leftLabel = "Left",
        string rightLabel = "Right",
        DiffProcessingOptions? options = null,
        IProgress<DiffProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> CanProcessLargeFileAsync(string filePath);
    Task<DiffStatistics> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default);
}

public interface IDiffCacheService
{
    Task<FileDiffResult?> GetCachedDiffAsync(string leftPath, string rightPath, DateTime leftModified, DateTime rightModified);
    Task CacheDiffResultAsync(FileDiffResult diffResult, DateTime leftModified, DateTime rightModified);
    Task<VirtualizedDiffResult?> GetCachedVirtualizedDiffAsync(string leftPath, string rightPath, DateTime leftModified, DateTime rightModified);
    Task CacheVirtualizedDiffAsync(VirtualizedDiffResult diffResult, DateTime leftModified, DateTime rightModified);
    Task ClearCacheAsync();
    Task<long> GetCacheSizeAsync();
    Task<int> GetCacheCountAsync();
}

public interface IPerformanceMonitorService
{
    Task<PerformanceMetrics> StartMonitoringAsync(string operationName);
    Task StopMonitoringAsync(PerformanceMetrics metrics);
    Task<List<PerformanceMetrics>> GetPerformanceHistoryAsync(TimeSpan? timeRange = null);
    Task ClearPerformanceHistoryAsync();
}

public class PerformanceMetrics
{
    public string OperationName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long ElapsedMs { get; set; }
    public long PeakMemoryMB { get; set; }
    public long StartMemoryMB { get; set; }
    public long EndMemoryMB { get; set; }
    public int ProcessedItems { get; set; }
    public string? AdditionalInfo { get; set; }
    public bool IsCompleted => EndTime.HasValue;
    public double ItemsPerSecond => ElapsedMs > 0 ? ProcessedItems * 1000.0 / ElapsedMs : 0.0;
}