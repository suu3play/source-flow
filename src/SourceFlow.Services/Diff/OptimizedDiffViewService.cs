using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using NLog;
using System.Text;

namespace SourceFlow.Services.Diff;

public class OptimizedDiffViewService : IDiffViewService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IAdvancedDiffEngine _advancedDiffEngine;
    private readonly IDiffCacheService _cacheService;
    private readonly IPerformanceMonitorService _performanceMonitor;
    private readonly ISyntaxHighlightingService _syntaxHighlightingService;

    public OptimizedDiffViewService(
        IAdvancedDiffEngine advancedDiffEngine,
        IDiffCacheService cacheService,
        IPerformanceMonitorService performanceMonitor,
        ISyntaxHighlightingService syntaxHighlightingService)
    {
        _advancedDiffEngine = advancedDiffEngine;
        _cacheService = cacheService;
        _performanceMonitor = performanceMonitor;
        _syntaxHighlightingService = syntaxHighlightingService;
    }

    public async Task<FileDiffResult> GenerateFileDiffAsync(string leftFilePath, string rightFilePath)
    {
        var metrics = await _performanceMonitor.StartMonitoringAsync($"GenerateFileDiff: {Path.GetFileName(leftFilePath)} vs {Path.GetFileName(rightFilePath)}");

        try
        {
            _logger.Info("最適化ファイル差分生成開始: {LeftPath} vs {RightPath}", leftFilePath, rightFilePath);

            // ファイル情報取得
            var leftInfo = new FileInfo(leftFilePath);
            var rightInfo = new FileInfo(rightFilePath);

            if (!leftInfo.Exists)
                throw new FileNotFoundException($"左側ファイルが見つかりません: {leftFilePath}");
            if (!rightInfo.Exists)
                throw new FileNotFoundException($"右側ファイルが見つかりません: {rightFilePath}");

            // キャッシュチェック
            var cachedResult = await _cacheService.GetCachedDiffAsync(
                leftFilePath, rightFilePath, leftInfo.LastWriteTime, rightInfo.LastWriteTime);

            if (cachedResult != null)
            {
                _logger.Info("キャッシュから差分結果を取得: {LeftPath} vs {RightPath}", leftFilePath, rightFilePath);
                metrics.ProcessedItems = Math.Max(cachedResult.LeftLines.Count, cachedResult.RightLines.Count);
                metrics.AdditionalInfo = "キャッシュヒット";
                await _performanceMonitor.StopMonitoringAsync(metrics);
                return cachedResult;
            }

            // 大ファイルかどうかチェック
            var isLargeFile = leftInfo.Length > 10 * 1024 * 1024 || rightInfo.Length > 10 * 1024 * 1024;

            FileDiffResult result;
            if (isLargeFile)
            {
                _logger.Info("大ファイルを検出。仮想化処理にフォールバック");
                var virtualizedResult = await _advancedDiffEngine.ProcessLargeFileDiffAsync(
                    leftFilePath, rightFilePath);
                
                // 仮想化結果を標準結果に変換（最初のチャンクのみ）
                result = await ConvertVirtualizedToStandardAsync(virtualizedResult);
                metrics.AdditionalInfo = "仮想化処理";
            }
            else
            {
                // 標準処理
                var leftContent = await File.ReadAllTextAsync(leftFilePath, Encoding.UTF8);
                var rightContent = await File.ReadAllTextAsync(rightFilePath, Encoding.UTF8);

                result = await _advancedDiffEngine.ProcessFileDiffAsync(
                    leftContent, rightContent, leftFilePath, rightFilePath);
                metrics.AdditionalInfo = "標準処理";
            }

            // キャッシュに保存
            await _cacheService.CacheDiffResultAsync(result, leftInfo.LastWriteTime, rightInfo.LastWriteTime);

            metrics.ProcessedItems = Math.Max(result.LeftLines.Count, result.RightLines.Count);
            await _performanceMonitor.StopMonitoringAsync(metrics);

            _logger.Info("最適化ファイル差分生成完了: 変更={HasChanges}, 処理={ProcessingInfo}", 
                result.HasChanges, metrics.AdditionalInfo);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "最適化ファイル差分生成に失敗しました");
            await _performanceMonitor.StopMonitoringAsync(metrics);
            throw;
        }
    }

    public async Task<FileDiffResult> GenerateContentDiffAsync(string leftContent, string rightContent, string leftLabel = "Left", string rightLabel = "Right")
    {
        var metrics = await _performanceMonitor.StartMonitoringAsync($"GenerateContentDiff: {leftLabel} vs {rightLabel}");

        try
        {
            _logger.Info("最適化コンテンツ差分生成開始: {LeftLabel} vs {RightLabel}", leftLabel, rightLabel);

            // コンテンツサイズベースで処理方法を判定
            var isLargeContent = leftContent.Length > 1024 * 1024 || rightContent.Length > 1024 * 1024; // 1MB

            FileDiffResult result;
            if (isLargeContent)
            {
                _logger.Info("大きなコンテンツを検出。ストリーム処理を使用");
                
                using var leftStream = new MemoryStream(Encoding.UTF8.GetBytes(leftContent));
                using var rightStream = new MemoryStream(Encoding.UTF8.GetBytes(rightContent));

                var virtualizedResult = await _advancedDiffEngine.ProcessLargeContentDiffAsync(
                    leftStream, rightStream, leftLabel, rightLabel);
                
                result = await ConvertVirtualizedToStandardAsync(virtualizedResult);
                metrics.AdditionalInfo = "大コンテンツ処理";
            }
            else
            {
                result = await _advancedDiffEngine.ProcessFileDiffAsync(
                    leftContent, rightContent, leftLabel, rightLabel);
                metrics.AdditionalInfo = "標準コンテンツ処理";
            }

            metrics.ProcessedItems = Math.Max(result.LeftLines.Count, result.RightLines.Count);
            await _performanceMonitor.StopMonitoringAsync(metrics);

            _logger.Info("最適化コンテンツ差分生成完了: 変更={HasChanges}", result.HasChanges);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "最適化コンテンツ差分生成に失敗しました");
            await _performanceMonitor.StopMonitoringAsync(metrics);
            throw;
        }
    }

    public async Task<List<SearchResult>> SearchInDiffAsync(FileDiffResult diffResult, string searchText, bool caseSensitive = false)
    {
        var metrics = await _performanceMonitor.StartMonitoringAsync($"SearchInDiff: {searchText}");

        try
        {
            if (string.IsNullOrEmpty(searchText))
                return [];

            _logger.Info("最適化差分検索開始: {SearchText}, CaseSensitive={CaseSensitive}", searchText, caseSensitive);

            var results = new List<SearchResult>();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            // 並列検索で高速化
            var leftSearchTask = Task.Run(() => SearchInLinesAsync(diffResult.LeftLines, searchText, comparison, true));
            var rightSearchTask = Task.Run(() => SearchInLinesAsync(diffResult.RightLines, searchText, comparison, false));

            var allResults = await Task.WhenAll(leftSearchTask, rightSearchTask);
            results.AddRange(allResults.SelectMany(r => r));

            // 結果をソート
            var sortedResults = results
                .OrderBy(r => r.FileType == FileType.Left ? 0 : 1)
                .ThenBy(r => r.LineNumber)
                .ThenBy(r => r.CharIndex)
                .ToList();

            metrics.ProcessedItems = diffResult.LeftLines.Count + diffResult.RightLines.Count;
            await _performanceMonitor.StopMonitoringAsync(metrics);

            _logger.Info("最適化差分検索完了: {ResultCount}件の結果", sortedResults.Count);
            return sortedResults;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "最適化差分検索に失敗しました");
            await _performanceMonitor.StopMonitoringAsync(metrics);
            throw;
        }
    }

    public async Task<ReplaceResult> ReplaceInDiffAsync(FileDiffResult diffResult, string searchText, string replaceText, bool caseSensitive = false, bool replaceAll = false)
    {
        var metrics = await _performanceMonitor.StartMonitoringAsync($"ReplaceInDiff: {searchText} -> {replaceText}");

        try
        {
            if (string.IsNullOrEmpty(searchText))
                return new ReplaceResult { IsSuccess = false, ErrorMessage = "検索テキストが空です" };

            _logger.Info("最適化差分置換開始: '{SearchText}' -> '{ReplaceText}', CaseSensitive={CaseSensitive}, ReplaceAll={ReplaceAll}", 
                searchText, replaceText, caseSensitive, replaceAll);

            var result = new ReplaceResult { IsSuccess = true };
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            // 並列置換で高速化
            var leftReplaceTask = Task.Run(() => ReplaceInLinesAsync(diffResult.LeftLines, searchText, replaceText, comparison, FileType.Left, replaceAll));
            var rightReplaceTask = Task.Run(() => ReplaceInLinesAsync(diffResult.RightLines, searchText, replaceText, comparison, FileType.Right, replaceAll));

            var replaceResults = await Task.WhenAll(leftReplaceTask, rightReplaceTask);
            
            foreach (var replaceResultItem in replaceResults)
            {
                result.ReplacedCount += replaceResultItem.ReplacedCount;
                result.Matches.AddRange(replaceResultItem.Matches);
            }

            metrics.ProcessedItems = result.ReplacedCount;
            await _performanceMonitor.StopMonitoringAsync(metrics);

            _logger.Info("最適化差分置換完了: {ReplacedCount}件の置換", result.ReplacedCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "最適化差分置換に失敗しました");
            await _performanceMonitor.StopMonitoringAsync(metrics);
            return new ReplaceResult 
            { 
                IsSuccess = false, 
                ErrorMessage = ex.Message 
            };
        }
    }

    public async Task SaveDiffResultAsync(FileDiffResult diffResult, string outputPath)
    {
        var metrics = await _performanceMonitor.StartMonitoringAsync($"SaveDiffResult: {Path.GetFileName(outputPath)}");

        try
        {
            _logger.Info("最適化差分結果保存開始: {OutputPath}", outputPath);

            var extension = Path.GetExtension(outputPath).ToLowerInvariant();
            
            switch (extension)
            {
                case ".json":
                    await SaveAsJsonAsync(diffResult, outputPath);
                    break;
                case ".html":
                    await SaveAsHtmlAsync(diffResult, outputPath);
                    break;
                case ".txt":
                    await SaveAsTextAsync(diffResult, outputPath);
                    break;
                case ".csv":
                    await SaveAsCsvAsync(diffResult, outputPath);
                    break;
                default:
                    throw new ArgumentException($"サポートされていない出力形式です: {extension}");
            }

            metrics.ProcessedItems = Math.Max(diffResult.LeftLines.Count, diffResult.RightLines.Count);
            await _performanceMonitor.StopMonitoringAsync(metrics);

            _logger.Info("最適化差分結果保存完了: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "最適化差分結果保存に失敗しました");
            await _performanceMonitor.StopMonitoringAsync(metrics);
            throw;
        }
    }

    public async Task<bool> CanProcessFileAsync(string filePath)
    {
        return await _advancedDiffEngine.CanProcessLargeFileAsync(filePath);
    }

    private async Task<FileDiffResult> ConvertVirtualizedToStandardAsync(VirtualizedDiffResult virtualizedResult)
    {
        // 仮想化結果から標準結果への変換（簡易版）
        var result = new FileDiffResult
        {
            LeftFilePath = virtualizedResult.LeftFilePath,
            RightFilePath = virtualizedResult.RightFilePath,
            GeneratedAt = virtualizedResult.GeneratedAt
        };

        if (virtualizedResult.Chunks.Any())
        {
            // 最初のチャンクをロードして変換
            var firstChunk = virtualizedResult.Chunks.First();
            if (firstChunk.IsLoaded)
            {
                result.LeftLines = firstChunk.Lines.Where(l => l.ChangeType != ChangeType.Add).ToList();
                result.RightLines = firstChunk.Lines.Where(l => l.ChangeType != ChangeType.Delete).ToList();
            }
        }

        await Task.CompletedTask; // 非同期形式維持
        return result;
    }

    private static async Task<List<SearchResult>> SearchInLinesAsync(
        List<LineDiff> lines, 
        string searchText, 
        StringComparison comparison, 
        bool isLeftSide)
    {
        return await Task.Run(() =>
        {
            var results = new List<SearchResult>();
            
            foreach (var line in lines)
            {
                var index = 0;
                while ((index = line.Content.IndexOf(searchText, index, comparison)) != -1)
                {
                    results.Add(new SearchResult
                    {
                        LineNumber = line.LineNumber,
                        CharIndex = index,
                        StartIndex = index,
                        Length = searchText.Length,
                        MatchedText = searchText,
                        ContextLine = line.Content,
                        FileType = isLeftSide ? FileType.Left : FileType.Right,
                        ChangeType = line.ChangeType,
                        CharacterPosition = index,
                        FilePath = ""
                    });
                    index += searchText.Length;
                }
            }
            
            return results;
        });
    }

    // 各種出力形式の実装（元のDiffViewServiceから移植・最適化）
    private static async Task SaveAsJsonAsync(FileDiffResult diffResult, string outputPath)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(diffResult, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        
        await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8);
    }

    private static async Task SaveAsTextAsync(FileDiffResult diffResult, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== 差分結果 ===");
        sb.AppendLine($"左側:  {diffResult.LeftFilePath}");
        sb.AppendLine($"右側: {diffResult.RightFilePath}");
        sb.AppendLine($"生成日時: {diffResult.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        var changedLines = diffResult.LeftLines.Where(l => l.ChangeType != ChangeType.NoChange).ToList();
        foreach (var line in changedLines)
        {
            var prefix = line.ChangeType switch
            {
                ChangeType.Add => "+",
                ChangeType.Delete => "-",
                ChangeType.Modify => "~",
                _ => " "
            };
            sb.AppendLine($"{prefix} {line.LineNumber:000000}: {line.Content}");
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static async Task SaveAsHtmlAsync(FileDiffResult diffResult, string outputPath)
    {
        var html = $$"""
<!DOCTYPE html>
<html>
<head>
    <title>差分結果</title>
    <meta charset="utf-8">
    <style>
        body { font-family: 'Consolas', 'Courier New', monospace; margin: 20px; }
        .diff-header { margin-bottom: 20px; padding: 15px; background: #f8f9fa; border-radius: 5px; }
        .diff-line { white-space: pre-wrap; padding: 2px 8px; border-left: 3px solid transparent; }
        .line-add { background-color: #d4edda; border-left-color: #28a745; }
        .line-delete { background-color: #f8d7da; border-left-color: #dc3545; }
        .line-modify { background-color: #fff3cd; border-left-color: #ffc107; }
        .line-number { color: #6c757d; margin-right: 15px; min-width: 60px; display: inline-block; }
    </style>
</head>
<body>
    <div class="diff-header">
        <h1>差分結果</h1>
        <p><strong>左側:</strong> {{diffResult.LeftFilePath}}</p>
        <p><strong>右側:</strong> {{diffResult.RightFilePath}}</p>
        <p><strong>生成日時:</strong> {{diffResult.GeneratedAt:yyyy-MM-dd HH:mm:ss}}</p>
    </div>
    <div class="diff-content">
""";

        var changedLines = diffResult.LeftLines.Where(l => l.ChangeType != ChangeType.NoChange).ToList();
        foreach (var line in changedLines)
        {
            var cssClass = line.ChangeType switch
            {
                ChangeType.Add => "line-add",
                ChangeType.Delete => "line-delete",
                ChangeType.Modify => "line-modify",
                _ => ""
            };
            var encodedContent = System.Net.WebUtility.HtmlEncode(line.Content);
            html = html + $"        <div class=\"diff-line {cssClass}\"><span class=\"line-number\">{line.LineNumber}</span>{encodedContent}</div>{Environment.NewLine}";
        }

        html += """
    </div>
</body>
</html>
""";

        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8);
    }

    private static async Task SaveAsCsvAsync(FileDiffResult diffResult, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"行番号\",\"変更タイプ\",\"内容\"");

        var changedLines = diffResult.LeftLines.Where(l => l.ChangeType != ChangeType.NoChange).ToList();
        foreach (var line in changedLines)
        {
            var changeType = line.ChangeType switch
            {
                ChangeType.Add => "追加",
                ChangeType.Delete => "削除",
                ChangeType.Modify => "変更",
                _ => "不明"
            };
            var escapedContent = line.Content.Replace("\"", "\"\"");
            sb.AppendLine($"{line.LineNumber},\"{changeType}\",\"{escapedContent}\"");
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
    }


    private static Task<ReplaceResult> ReplaceInLinesAsync(List<LineDiff> lines, string searchText, string replaceText, StringComparison comparison, FileType fileType, bool replaceAll)
    {
        return Task.Run(() =>
        {
            var result = new ReplaceResult { IsSuccess = true };
            
            foreach (var line in lines)
            {
                var originalContent = line.Content;
                var newContent = originalContent;
                var currentIndex = 0;
                var replacedInLine = 0;

                while ((currentIndex = newContent.IndexOf(searchText, currentIndex, comparison)) != -1)
                {
                    result.Matches.Add(new ReplaceMatch
                    {
                        LineNumber = line.LineNumber,
                        StartIndex = currentIndex,
                        Length = searchText.Length,
                        OriginalText = searchText,
                        ReplacedText = replaceText,
                        FileType = fileType
                    });

                    newContent = newContent.Remove(currentIndex, searchText.Length).Insert(currentIndex, replaceText);
                    currentIndex += replaceText.Length;
                    replacedInLine++;
                    result.ReplacedCount++;

                    if (!replaceAll) break;
                }

                if (replacedInLine > 0)
                {
                    line.Content = newContent;
                    if (line.ChangeType == ChangeType.NoChange)
                    {
                        line.ChangeType = ChangeType.Modify;
                    }
                }
            }
            
            return result;
        });
    }
}