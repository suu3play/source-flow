using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using spkl.Diffs;
using NLog;
using System.Diagnostics;
using System.Text;

namespace SourceFlow.Services.Diff;

public class AdvancedDiffEngine : TextDiffEngine, IAdvancedDiffEngine
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IPerformanceMonitorService _performanceMonitor;

    public AdvancedDiffEngine(IPerformanceMonitorService performanceMonitor)
    {
        _performanceMonitor = performanceMonitor;
    }

    public async Task<VirtualizedDiffResult> ProcessLargeFileDiffAsync(
        string leftFilePath, 
        string rightFilePath, 
        DiffProcessingOptions? options = null,
        IProgress<DiffProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DiffProcessingOptions();
        var metrics = await _performanceMonitor.StartMonitoringAsync($"ProcessLargeFileDiff: {Path.GetFileName(leftFilePath)} vs {Path.GetFileName(rightFilePath)}");

        try
        {
            _logger.Info("大ファイル差分処理開始: {LeftPath} vs {RightPath}", leftFilePath, rightFilePath);

            if (!await CanProcessLargeFileAsync(leftFilePath))
                throw new ArgumentException($"処理できないファイルです: {leftFilePath}");

            if (!await CanProcessLargeFileAsync(rightFilePath))
                throw new ArgumentException($"処理できないファイルです: {rightFilePath}");

            using var leftStream = File.OpenRead(leftFilePath);
            using var rightStream = File.OpenRead(rightFilePath);

            var result = await ProcessLargeContentDiffAsync(
                leftStream, rightStream, leftFilePath, rightFilePath, options, progress, cancellationToken);

            metrics.ProcessedItems = result.TotalLineCount;
            await _performanceMonitor.StopMonitoringAsync(metrics);

            _logger.Info("大ファイル差分処理完了: {TotalLines}行, 仮想化={IsVirtualized}", result.TotalLineCount, result.IsVirtualized);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "大ファイル差分処理に失敗しました");
            await _performanceMonitor.StopMonitoringAsync(metrics);
            throw;
        }
    }

    public async Task<VirtualizedDiffResult> ProcessLargeContentDiffAsync(
        Stream leftStream, 
        Stream rightStream, 
        string leftLabel = "Left",
        string rightLabel = "Right",
        DiffProcessingOptions? options = null,
        IProgress<DiffProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DiffProcessingOptions();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = new VirtualizedDiffResult
            {
                LeftFilePath = leftLabel,
                RightFilePath = rightLabel,
                LeftFileSize = leftStream.Length,
                RightFileSize = rightStream.Length,
                ViewportSize = options.ChunkSize,
                GeneratedAt = DateTime.Now
            };

            // ファイルサイズベースで仮想化を判定（10MB以上）
            var shouldVirtualize = leftStream.Length > 10 * 1024 * 1024 || rightStream.Length > 10 * 1024 * 1024;

            if (shouldVirtualize)
            {
                _logger.Info("仮想化モードで処理開始: LeftSize={LeftSize}MB, RightSize={RightSize}MB", 
                    leftStream.Length / (1024 * 1024), rightStream.Length / (1024 * 1024));
                
                await ProcessVirtualizedDiffAsync(leftStream, rightStream, result, options, progress, cancellationToken);
            }
            else
            {
                _logger.Info("標準モードで処理開始");
                await ProcessStandardDiffAsync(leftStream, rightStream, result, options, progress, cancellationToken);
            }

            result.Statistics.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.Statistics.WasVirtualized = shouldVirtualize;
            result.Statistics.MemoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "大ファイルコンテンツ差分処理に失敗しました");
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public async Task<List<LineDiff>> LoadChunkAsync(
        VirtualizedDiffResult diffResult, 
        int chunkIndex,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (chunkIndex < 0 || chunkIndex >= diffResult.Chunks.Count)
                throw new ArgumentOutOfRangeException(nameof(chunkIndex));

            var chunk = diffResult.Chunks[chunkIndex];
            if (chunk.IsLoaded)
                return chunk.Lines;

            _logger.Info("チャンク読み込み開始: Index={ChunkIndex}, StartLine={StartLine}, LineCount={LineCount}", 
                chunkIndex, chunk.StartLineNumber, chunk.LineCount);

            // 実際のファイルからチャンクを読み込み
            using var leftStream = File.OpenRead(diffResult.LeftFilePath);
            using var rightStream = File.OpenRead(diffResult.RightFilePath);

            var leftLines = await ReadLinesFromStreamAsync(leftStream, chunk.StartLineNumber, chunk.LineCount, cancellationToken);
            var rightLines = await ReadLinesFromStreamAsync(rightStream, chunk.StartLineNumber, chunk.LineCount, cancellationToken);

            chunk.Lines = await ComputeLineDiffAsync(leftLines, rightLines);
            chunk.IsLoaded = true;

            _logger.Info("チャンク読み込み完了: {LoadedLines}行", chunk.Lines.Count);
            return chunk.Lines;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "チャンク読み込みに失敗しました");
            throw;
        }
    }

    public async Task<bool> CanProcessLargeFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            var fileInfo = new FileInfo(filePath);
            
            // 1GB以下のファイルサイズ制限
            if (fileInfo.Length > 1024 * 1024 * 1024)
                return false;

            // テキストファイルかどうかを簡単にチェック
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var textExtensions = new[]
            {
                ".txt", ".cs", ".xml", ".json", ".html", ".css", ".js", ".ts",
                ".sql", ".py", ".java", ".cpp", ".c", ".h", ".php", ".rb",
                ".go", ".rs", ".md", ".yml", ".yaml", ".ini", ".bat", ".cmd", ".ps1"
            };

            if (textExtensions.Contains(extension))
                return true;

            // バイナリファイル検出（先頭4KBをチェック）
            try
            {
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[Math.Min(4096, (int)stream.Length)];
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                var actualBuffer = buffer.AsSpan(0, bytesRead).ToArray();
                
                return !actualBuffer.Contains((byte)0);
            }
            catch
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "大ファイル処理可能性チェックに失敗: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<DiffStatistics> AnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var statistics = new DiffStatistics();

            using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var lineCount = 0;
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                await reader.ReadLineAsync();
                lineCount++;
            }

            statistics.TotalLines = lineCount;
            statistics.UnchangedLines = lineCount; // 単一ファイル分析では全て未変更

            _logger.Info("ファイル分析完了: {FilePath}, Lines={LineCount}", filePath, lineCount);
            return statistics;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイル分析に失敗: {FilePath}", filePath);
            throw;
        }
    }

    private async Task ProcessVirtualizedDiffAsync(
        Stream leftStream, 
        Stream rightStream, 
        VirtualizedDiffResult result, 
        DiffProcessingOptions options,
        IProgress<DiffProgress>? progress,
        CancellationToken cancellationToken)
    {
        var leftLineCount = await CountLinesAsync(leftStream, cancellationToken);
        var rightLineCount = await CountLinesAsync(rightStream, cancellationToken);
        
        result.TotalLineCount = Math.Max(leftLineCount, rightLineCount);
        result.Statistics.TotalLines = result.TotalLineCount;

        // チャンクを作成（差分計算は遅延）
        var chunkCount = (result.TotalLineCount + options.ChunkSize - 1) / options.ChunkSize;
        for (int i = 0; i < chunkCount; i++)
        {
            var startLine = i * options.ChunkSize;
            var lineCount = Math.Min(options.ChunkSize, result.TotalLineCount - startLine);

            result.Chunks.Add(new DiffChunk
            {
                StartLineNumber = startLine,
                LineCount = lineCount,
                ChangeType = ChangeType.NoChange, // 初期値
                IsLoaded = false,
                PreviewText = $"チャンク {i + 1}/{chunkCount} ({startLine + 1}-{startLine + lineCount}行目)"
            });

            progress?.Report(new DiffProgress
            {
                ProcessedLines = (i + 1) * options.ChunkSize,
                TotalLines = result.TotalLineCount,
                CurrentOperation = "チャンク作成中"
            });
        }

        _logger.Info("仮想化処理完了: {ChunkCount}チャンク作成, 総行数={TotalLines}", chunkCount, result.TotalLineCount);
    }

    private async Task ProcessStandardDiffAsync(
        Stream leftStream, 
        Stream rightStream, 
        VirtualizedDiffResult result, 
        DiffProcessingOptions options,
        IProgress<DiffProgress>? progress,
        CancellationToken cancellationToken)
    {
        var leftContent = await new StreamReader(leftStream, Encoding.UTF8).ReadToEndAsync();
        var rightContent = await new StreamReader(rightStream, Encoding.UTF8).ReadToEndAsync();

        var fileDiffResult = await ProcessFileDiffAsync(leftContent, rightContent, result.LeftFilePath, result.RightFilePath);
        
        result.TotalLineCount = Math.Max(fileDiffResult.LeftLines.Count, fileDiffResult.RightLines.Count);
        result.Statistics.TotalLines = result.TotalLineCount;
        result.Statistics.AddedLines = fileDiffResult.LeftLines.Count(l => l.ChangeType == ChangeType.Add);
        result.Statistics.DeletedLines = fileDiffResult.LeftLines.Count(l => l.ChangeType == ChangeType.Delete);
        result.Statistics.ModifiedLines = fileDiffResult.LeftLines.Count(l => l.ChangeType == ChangeType.Modify);
        result.Statistics.UnchangedLines = fileDiffResult.LeftLines.Count(l => l.ChangeType == ChangeType.NoChange);

        // 標準処理では1つのチャンクとして格納
        result.Chunks.Add(new DiffChunk
        {
            StartLineNumber = 0,
            LineCount = result.TotalLineCount,
            ChangeType = fileDiffResult.HasChanges ? ChangeType.Modify : ChangeType.NoChange,
            IsLoaded = true,
            Lines = fileDiffResult.LeftLines,
            PreviewText = $"標準処理: {result.TotalLineCount}行"
        });

        progress?.Report(new DiffProgress
        {
            ProcessedLines = result.TotalLineCount,
            TotalLines = result.TotalLineCount,
            CurrentOperation = "完了"
        });
    }

    private static async Task<int> CountLinesAsync(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        
        var lineCount = 0;
        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            await reader.ReadLineAsync();
            lineCount++;
        }

        stream.Position = 0;
        return lineCount;
    }

    private static async Task<string[]> ReadLinesFromStreamAsync(
        Stream stream, 
        int startLine, 
        int lineCount, 
        CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        
        var lines = new List<string>();
        var currentLine = 0;
        
        // 開始行まで読み飛ばし
        while (currentLine < startLine && !reader.EndOfStream)
        {
            await reader.ReadLineAsync();
            currentLine++;
        }
        
        // 必要な行数だけ読み込み
        var readLines = 0;
        while (readLines < lineCount && !reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line != null)
            {
                lines.Add(line);
                readLines++;
            }
        }

        stream.Position = 0;
        return lines.ToArray();
    }
}