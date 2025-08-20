using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using spkl.Diffs;
using NLog;

namespace SourceFlow.Services.Diff;

public class TextDiffEngine : ITextDiffEngine
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public async Task<List<LineDiff>> ComputeLineDiffAsync(string[] leftLines, string[] rightLines)
    {
        try
        {
            _logger.Info("行単位差分計算開始: {LeftCount}行 vs {RightCount}行", leftLines.Length, rightLines.Length);
            
            var diff = new MyersDiff<string>(leftLines, rightLines);
            var results = new List<LineDiff>();

            await Task.Run(() =>
            {
                var diffResults = diff.GetResult();
                var lineNumber = 1;

                foreach (var diffItem in diffResults)
                {
                    if (diffItem.ResultType == spkl.Diffs.ResultType.Both)
                    {
                        // 変更なし
                        results.Add(new LineDiff
                        {
                            LineNumber = lineNumber,
                            Content = diffItem.AItem ?? string.Empty,
                            ChangeType = ChangeType.NoChange,
                            CorrespondingLineNumber = lineNumber,
                            OriginalContent = diffItem.BItem ?? string.Empty
                        });
                    }
                    else if (diffItem.ResultType == spkl.Diffs.ResultType.A)
                    {
                        // 削除
                        results.Add(new LineDiff
                        {
                            LineNumber = lineNumber,
                            Content = diffItem.AItem ?? string.Empty,
                            ChangeType = ChangeType.Delete
                        });
                    }
                    else if (diffItem.ResultType == spkl.Diffs.ResultType.B)
                    {
                        // 追加
                        results.Add(new LineDiff
                        {
                            LineNumber = lineNumber,
                            Content = diffItem.BItem ?? string.Empty,
                            ChangeType = ChangeType.Add
                        });
                    }
                    
                    lineNumber++;
                }
            });

            _logger.Info("行単位差分計算完了: {ResultCount}行の差分を検出", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "行単位差分計算に失敗しました");
            throw;
        }
    }

    public async Task<List<CharacterDiff>> ComputeCharacterDiffAsync(string leftLine, string rightLine)
    {
        try
        {
            var leftChars = leftLine.ToCharArray().Select(c => c.ToString()).ToArray();
            var rightChars = rightLine.ToCharArray().Select(c => c.ToString()).ToArray();
            
            var diff = new MyersDiff<string>(leftChars, rightChars);
            var results = new List<CharacterDiff>();

            await Task.Run(() =>
            {
                var diffResults = diff.GetResult();
                var charIndex = 0;

                foreach (var diffItem in diffResults)
                {
                    if (diffItem.ResultType == spkl.Diffs.ResultType.A)
                    {
                        // 削除
                        var content = diffItem.AItem ?? string.Empty;
                        results.Add(new CharacterDiff
                        {
                            StartIndex = charIndex,
                            Length = content.Length,
                            ChangeType = ChangeType.Delete,
                            Content = content
                        });
                    }
                    else if (diffItem.ResultType == spkl.Diffs.ResultType.B)
                    {
                        // 挿入
                        var content = diffItem.BItem ?? string.Empty;
                        results.Add(new CharacterDiff
                        {
                            StartIndex = charIndex,
                            Length = content.Length,
                            ChangeType = ChangeType.Add,
                            Content = content
                        });
                    }
                    
                    charIndex++;
                }
            });

            return results;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "文字単位差分計算に失敗しました");
            throw;
        }
    }

    public async Task<FileDiffResult> ProcessFileDiffAsync(string leftContent, string rightContent, string leftPath, string rightPath)
    {
        try
        {
            _logger.Info("ファイル差分処理開始: {LeftPath} vs {RightPath}", leftPath, rightPath);
            
            var leftLines = leftContent.Split(['\r', '\n'], StringSplitOptions.None)
                                     .Where(line => !string.IsNullOrEmpty(line) || leftContent.Contains(line))
                                     .ToArray();
            var rightLines = rightContent.Split(['\r', '\n'], StringSplitOptions.None)
                                        .Where(line => !string.IsNullOrEmpty(line) || rightContent.Contains(line))
                                        .ToArray();

            var lineDiffs = await ComputeLineDiffAsync(leftLines, rightLines);
            
            var result = new FileDiffResult
            {
                LeftFilePath = leftPath,
                RightFilePath = rightPath,
                LeftFileContent = leftContent,
                RightFileContent = rightContent,
                GeneratedAt = DateTime.Now
            };

            // 左側の行を構築
            result.LeftLines = lineDiffs
                .Where(d => d.ChangeType != ChangeType.Add)
                .ToList();

            // 右側の行を構築
            result.RightLines = lineDiffs
                .Where(d => d.ChangeType != ChangeType.Delete)
                .Select(d => new LineDiff
                {
                    LineNumber = d.CorrespondingLineNumber ?? d.LineNumber,
                    Content = d.OriginalContent ?? d.Content,
                    ChangeType = d.ChangeType == ChangeType.NoChange ? ChangeType.NoChange : ChangeType.Add,
                    CorrespondingLineNumber = d.LineNumber,
                    OriginalContent = d.Content
                })
                .ToList();

            _logger.Info("ファイル差分処理完了: 変更あり={HasChanges}", result.HasChanges);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイル差分処理に失敗しました");
            throw;
        }
    }
}