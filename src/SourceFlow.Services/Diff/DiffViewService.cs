using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using NLog;
using System.Text;
using System.Text.Json;

namespace SourceFlow.Services.Diff;

public class DiffViewService : IDiffViewService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly ITextDiffEngine _diffEngine;
    private readonly ISyntaxHighlightingService _syntaxHighlightingService;

    public DiffViewService(ITextDiffEngine diffEngine, ISyntaxHighlightingService syntaxHighlightingService)
    {
        _diffEngine = diffEngine;
        _syntaxHighlightingService = syntaxHighlightingService;
    }

    public async Task<FileDiffResult> GenerateFileDiffAsync(string leftFilePath, string rightFilePath)
    {
        try
        {
            _logger.Info("ファイル差分生成開始: {LeftPath} vs {RightPath}", leftFilePath, rightFilePath);

            if (!await CanProcessFileAsync(leftFilePath))
                throw new ArgumentException($"処理できないファイル形式です: {leftFilePath}");

            if (!await CanProcessFileAsync(rightFilePath))
                throw new ArgumentException($"処理できないファイル形式です: {rightFilePath}");

            var leftContent = await File.ReadAllTextAsync(leftFilePath, Encoding.UTF8);
            var rightContent = await File.ReadAllTextAsync(rightFilePath, Encoding.UTF8);

            var result = await _diffEngine.ProcessFileDiffAsync(leftContent, rightContent, leftFilePath, rightFilePath);
            
            _logger.Info("ファイル差分生成完了: 変更={HasChanges}", result.HasChanges);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイル差分生成に失敗しました");
            throw;
        }
    }

    public async Task<FileDiffResult> GenerateContentDiffAsync(string leftContent, string rightContent, string leftLabel = "Left", string rightLabel = "Right")
    {
        try
        {
            _logger.Info("コンテンツ差分生成開始: {LeftLabel} vs {RightLabel}", leftLabel, rightLabel);
            
            var result = await _diffEngine.ProcessFileDiffAsync(leftContent, rightContent, leftLabel, rightLabel);
            
            _logger.Info("コンテンツ差分生成完了: 変更={HasChanges}", result.HasChanges);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "コンテンツ差分生成に失敗しました");
            throw;
        }
    }

    public async Task<List<SearchResult>> SearchInDiffAsync(FileDiffResult diffResult, string searchText, bool caseSensitive = false)
    {
        try
        {
            if (string.IsNullOrEmpty(searchText))
                return [];

            _logger.Info("差分内検索開始: {SearchText}, CaseSensitive={CaseSensitive}", searchText, caseSensitive);

            var results = new List<SearchResult>();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            await Task.Run(() =>
            {
                // 左側の検索
                foreach (var line in diffResult.LeftLines)
                {
                    var index = 0;
                    while ((index = line.Content.IndexOf(searchText, index, comparison)) != -1)
                    {
                        results.Add(new SearchResult
                        {
                            LineNumber = line.LineNumber,
                            CharIndex = index,
                            Length = searchText.Length,
                            IsLeftSide = true
                        });
                        index += searchText.Length;
                    }
                }

                // 右側の検索
                foreach (var line in diffResult.RightLines)
                {
                    var index = 0;
                    while ((index = line.Content.IndexOf(searchText, index, comparison)) != -1)
                    {
                        results.Add(new SearchResult
                        {
                            LineNumber = line.LineNumber,
                            CharIndex = index,
                            Length = searchText.Length,
                            IsLeftSide = false
                        });
                        index += searchText.Length;
                    }
                }
            });

            _logger.Info("差分内検索完了: {ResultCount}件の結果", results.Count);
            return results.OrderBy(r => r.IsLeftSide ? 0 : 1)
                         .ThenBy(r => r.LineNumber)
                         .ThenBy(r => r.CharIndex)
                         .ToList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "差分内検索に失敗しました");
            throw;
        }
    }

    public async Task SaveDiffResultAsync(FileDiffResult diffResult, string outputPath)
    {
        try
        {
            _logger.Info("差分結果保存開始: {OutputPath}", outputPath);

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
                default:
                    throw new ArgumentException($"サポートされていない出力形式です: {extension}");
            }

            _logger.Info("差分結果保存完了: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "差分結果保存に失敗しました");
            throw;
        }
    }

    public async Task<bool> CanProcessFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            var fileInfo = new FileInfo(filePath);
            
            // ファイルサイズチェック（100MB以下）
            if (fileInfo.Length > 100 * 1024 * 1024)
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

            // 拡張子で判定できない場合、先頭バイトをチェック
            try
            {
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[Math.Min(1024, (int)stream.Length)];
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                
                // 実際に読み取れた部分のみを使用
                var actualBuffer = buffer.AsSpan(0, bytesRead).ToArray();
                
                // null バイトが含まれていればバイナリファイルと判定
                return !actualBuffer.Contains((byte)0);
            }
            catch
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイル処理可能性チェックに失敗: {FilePath}", filePath);
            return false;
        }
    }

    private static async Task SaveAsJsonAsync(FileDiffResult diffResult, string outputPath)
    {
        var json = JsonSerializer.Serialize(diffResult, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        
        await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8);
    }

    private static async Task SaveAsTextAsync(FileDiffResult diffResult, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Diff Result ===");
        sb.AppendLine($"Left:  {diffResult.LeftFilePath}");
        sb.AppendLine($"Right: {diffResult.RightFilePath}");
        sb.AppendLine($"Generated: {diffResult.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        var maxLineNumber = Math.Max(
            diffResult.LeftLines.DefaultIfEmpty().Max(l => l?.LineNumber ?? 0),
            diffResult.RightLines.DefaultIfEmpty().Max(l => l?.LineNumber ?? 0)
        );
        var lineNumberWidth = maxLineNumber.ToString().Length;

        foreach (var line in diffResult.LeftLines.Where(l => l.ChangeType != ChangeType.NoChange))
        {
            var prefix = line.ChangeType switch
            {
                ChangeType.Add => "+",
                ChangeType.Delete => "-",
                ChangeType.Modify => "~",
                _ => " "
            };
            sb.AppendLine($"{prefix} {line.LineNumber.ToString().PadLeft(lineNumberWidth)}: {line.Content}");
        }

        await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
    }

    private static async Task SaveAsHtmlAsync(FileDiffResult diffResult, string outputPath)
    {
        var html = $$"""
<!DOCTYPE html>
<html>
<head>
    <title>Diff Result</title>
    <style>
        body { font-family: 'Consolas', monospace; margin: 20px; }
        .diff-header { margin-bottom: 20px; padding: 10px; background: #f5f5f5; }
        .diff-line { white-space: pre-wrap; padding: 2px 5px; }
        .line-add { background-color: #d4edda; }
        .line-delete { background-color: #f8d7da; }
        .line-modify { background-color: #fff3cd; }
        .line-number { color: #666; margin-right: 10px; }
    </style>
</head>
<body>
    <div class="diff-header">
        <h1>Diff Result</h1>
        <p><strong>Left:</strong> {{diffResult.LeftFilePath}}</p>
        <p><strong>Right:</strong> {{diffResult.RightFilePath}}</p>
        <p><strong>Generated:</strong> {{diffResult.GeneratedAt:yyyy-MM-dd HH:mm:ss}}</p>
    </div>
    <div class="diff-content">
""";

        foreach (var line in diffResult.LeftLines.Where(l => l.ChangeType != ChangeType.NoChange))
        {
            var cssClass = line.ChangeType switch
            {
                ChangeType.Add => "line-add",
                ChangeType.Delete => "line-delete",
                ChangeType.Modify => "line-modify",
                _ => ""
            };
            var encodedContent = System.Net.WebUtility.HtmlEncode(line.Content);
            html += $"        <div class=\"diff-line {cssClass}\"><span class=\"line-number\">{line.LineNumber}</span>{encodedContent}</div>{Environment.NewLine}";
        }

        html += """
    </div>
</body>
</html>
""";

        await File.WriteAllTextAsync(outputPath, html, Encoding.UTF8);
    }
}