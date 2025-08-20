using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using NLog;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;

namespace SourceFlow.Services.Diff;

public class PerformanceMonitorService : IPerformanceMonitorService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly ConcurrentBag<PerformanceMetrics> _performanceHistory = [];
    private readonly int _maxHistoryCount = 1000;

    public async Task<PerformanceMetrics> StartMonitoringAsync(string operationName)
    {
        await Task.CompletedTask; // 非同期形式維持
        
        var metrics = new PerformanceMetrics
        {
            OperationName = operationName,
            StartTime = DateTime.Now,
            StartMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024)
        };

        _logger.Debug("パフォーマンス監視開始: {OperationName}", operationName);
        return metrics;
    }

    public async Task StopMonitoringAsync(PerformanceMetrics metrics)
    {
        await Task.CompletedTask; // 非同期形式維持

        metrics.EndTime = DateTime.Now;
        metrics.ElapsedMs = (long)(metrics.EndTime.Value - metrics.StartTime).TotalMilliseconds;
        metrics.EndMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        
        // ピークメモリ使用量を測定（簡易版）
        metrics.PeakMemoryMB = Math.Max(metrics.StartMemoryMB, metrics.EndMemoryMB);

        _performanceHistory.Add(metrics);

        // 履歴サイズ制限
        if (_performanceHistory.Count > _maxHistoryCount)
        {
            await TrimHistoryAsync();
        }

        _logger.Info("パフォーマンス監視完了: {OperationName}, 経過時間={ElapsedMs}ms, " +
                    "メモリ使用量={StartMB}->{EndMB}MB, 処理項目={ProcessedItems}, " +
                    "処理速度={ItemsPerSecond:F2}items/sec",
            metrics.OperationName, metrics.ElapsedMs, metrics.StartMemoryMB, metrics.EndMemoryMB,
            metrics.ProcessedItems, metrics.ItemsPerSecond);
    }

    public async Task<List<PerformanceMetrics>> GetPerformanceHistoryAsync(TimeSpan? timeRange = null)
    {
        await Task.CompletedTask; // 非同期形式維持

        var cutoffTime = timeRange.HasValue 
            ? DateTime.Now - timeRange.Value 
            : DateTime.MinValue;

        return _performanceHistory
            .Where(m => m.StartTime >= cutoffTime)
            .OrderByDescending(m => m.StartTime)
            .ToList();
    }

    public async Task ClearPerformanceHistoryAsync()
    {
        await Task.CompletedTask; // 非同期形式維持
        
        _performanceHistory.Clear();
        _logger.Info("パフォーマンス履歴をクリアしました");
    }

    public async Task<PerformanceStatistics> GetPerformanceStatisticsAsync(TimeSpan? timeRange = null)
    {
        var history = await GetPerformanceHistoryAsync(timeRange);

        if (!history.Any())
        {
            return new PerformanceStatistics();
        }

        var completedOperations = history.Where(h => h.IsCompleted).ToList();

        return new PerformanceStatistics
        {
            TotalOperations = history.Count,
            CompletedOperations = completedOperations.Count,
            FailedOperations = history.Count - completedOperations.Count,
            AverageElapsedMs = completedOperations.Any() ? completedOperations.Average(h => h.ElapsedMs) : 0,
            MinElapsedMs = completedOperations.Any() ? completedOperations.Min(h => h.ElapsedMs) : 0,
            MaxElapsedMs = completedOperations.Any() ? completedOperations.Max(h => h.ElapsedMs) : 0,
            AverageMemoryUsageMB = completedOperations.Any() ? completedOperations.Average(h => h.EndMemoryMB - h.StartMemoryMB) : 0,
            TotalProcessedItems = completedOperations.Sum(h => h.ProcessedItems),
            AverageItemsPerSecond = completedOperations.Any() ? completedOperations.Average(h => h.ItemsPerSecond) : 0,
            MostFrequentOperations = history
                .GroupBy(h => h.OperationName)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count()),
            TimeRange = timeRange ?? TimeSpan.FromDays(30)
        };
    }

    public async Task<string> GeneratePerformanceReportAsync(TimeSpan? timeRange = null)
    {
        var statistics = await GetPerformanceStatisticsAsync(timeRange);
        var history = await GetPerformanceHistoryAsync(timeRange);

        var report = new StringBuilder();
        report.AppendLine("# パフォーマンスレポート");
        report.AppendLine($"生成日時: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"対象期間: {statistics.TimeRange}");
        report.AppendLine();

        report.AppendLine("## 概要統計");
        report.AppendLine($"- 総操作数: {statistics.TotalOperations}");
        report.AppendLine($"- 完了操作数: {statistics.CompletedOperations}");
        report.AppendLine($"- 失敗操作数: {statistics.FailedOperations}");
        report.AppendLine($"- 平均実行時間: {statistics.AverageElapsedMs:F2}ms");
        report.AppendLine($"- 最小実行時間: {statistics.MinElapsedMs}ms");
        report.AppendLine($"- 最大実行時間: {statistics.MaxElapsedMs}ms");
        report.AppendLine($"- 平均メモリ使用量: {statistics.AverageMemoryUsageMB:F2}MB");
        report.AppendLine($"- 総処理項目数: {statistics.TotalProcessedItems}");
        report.AppendLine($"- 平均処理速度: {statistics.AverageItemsPerSecond:F2}items/sec");
        report.AppendLine();

        if (statistics.MostFrequentOperations.Any())
        {
            report.AppendLine("## 操作別頻度");
            foreach (var kvp in statistics.MostFrequentOperations)
            {
                report.AppendLine($"- {kvp.Key}: {kvp.Value}回");
            }
            report.AppendLine();
        }

        if (history.Any())
        {
            report.AppendLine("## 最近の操作 (最新10件)");
            var recentOperations = history.Take(10);
            foreach (var op in recentOperations)
            {
                var status = op.IsCompleted ? "完了" : "実行中";
                report.AppendLine($"- [{op.StartTime:HH:mm:ss}] {op.OperationName} ({status}, {op.ElapsedMs}ms, {op.ProcessedItems}項目)");
            }
        }

        return report.ToString();
    }

    private async Task TrimHistoryAsync()
    {
        await Task.Run(() =>
        {
            var historyList = _performanceHistory.ToList();
            var toKeep = historyList
                .OrderByDescending(m => m.StartTime)
                .Take(_maxHistoryCount * 3 / 4) // 75%を保持
                .ToList();

            _performanceHistory.Clear();
            foreach (var item in toKeep)
            {
                _performanceHistory.Add(item);
            }
        });

        _logger.Debug("パフォーマンス履歴をトリムしました: {CurrentCount}件", _performanceHistory.Count);
    }
}

public class PerformanceStatistics
{
    public int TotalOperations { get; set; }
    public int CompletedOperations { get; set; }
    public int FailedOperations { get; set; }
    public double AverageElapsedMs { get; set; }
    public long MinElapsedMs { get; set; }
    public long MaxElapsedMs { get; set; }
    public double AverageMemoryUsageMB { get; set; }
    public int TotalProcessedItems { get; set; }
    public double AverageItemsPerSecond { get; set; }
    public Dictionary<string, int> MostFrequentOperations { get; set; } = [];
    public TimeSpan TimeRange { get; set; }
}