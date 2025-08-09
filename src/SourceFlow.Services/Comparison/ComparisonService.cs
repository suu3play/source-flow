using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using SourceFlow.Services.FileOperations;
using System.Diagnostics;
using NLog;

namespace SourceFlow.Services.Comparison;

public class ComparisonService : IComparisonService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IConfigurationService _configurationService;
    
    public ComparisonService(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }
    
    public async Task<List<FileComparisonResult>> CompareDirectoriesAsync(string sourcePath, string targetPath)
    {
        var results = new List<FileComparisonResult>();
        
        try
        {
            _logger.Info("ディレクトリ比較開始: {Source} vs {Target}", sourcePath, targetPath);
            
            var sourceFiles = new Dictionary<string, FileInfo>();
            var targetFiles = new Dictionary<string, FileInfo>();
            
            // ソースファイル収集
            if (Directory.Exists(sourcePath))
            {
                await Task.Run(() =>
                {
                    foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(sourcePath, file);
                        sourceFiles[relativePath] = new FileInfo(file);
                    }
                });
            }
            
            // ターゲットファイル収集
            if (Directory.Exists(targetPath))
            {
                await Task.Run(() =>
                {
                    foreach (var file in Directory.GetFiles(targetPath, "*", SearchOption.AllDirectories))
                    {
                        var relativePath = Path.GetRelativePath(targetPath, file);
                        targetFiles[relativePath] = new FileInfo(file);
                    }
                });
            }
            
            // 全ファイルパスを取得
            var allFilePaths = sourceFiles.Keys.Union(targetFiles.Keys).ToHashSet();
            
            foreach (var filePath in allFilePaths)
            {
                var sourceExists = sourceFiles.TryGetValue(filePath, out var sourceInfo);
                var targetExists = targetFiles.TryGetValue(filePath, out var targetInfo);
                
                var result = new FileComparisonResult
                {
                    FilePath = filePath
                };
                
                if (sourceExists && targetExists)
                {
                    result.SourceSize = sourceInfo!.Length;
                    result.TargetSize = targetInfo!.Length;
                    result.SourceModified = sourceInfo.LastWriteTime;
                    result.TargetModified = targetInfo.LastWriteTime;
                    
                    // ファイルが変更されているかチェック
                    var isModified = sourceInfo.Length != targetInfo.Length ||
                                   sourceInfo.LastWriteTime != targetInfo.LastWriteTime;
                    
                    if (isModified)
                    {
                        // より詳細な比較（ハッシュ）
                        var sourceFullPath = Path.Combine(sourcePath, filePath);
                        var targetFullPath = Path.Combine(targetPath, filePath);
                        
                        if (!await FileHashService.CompareFileHashesAsync(sourceFullPath, targetFullPath))
                        {
                            result.ChangeType = ChangeType.Modify;
                        }
                        else
                        {
                            continue; // 実際には変更なし
                        }
                    }
                    else
                    {
                        continue; // 変更なし
                    }
                }
                else if (sourceExists && !targetExists)
                {
                    result.ChangeType = ChangeType.Add;
                    result.SourceSize = sourceInfo!.Length;
                    result.SourceModified = sourceInfo.LastWriteTime;
                }
                else if (!sourceExists && targetExists)
                {
                    result.ChangeType = ChangeType.Delete;
                    result.TargetSize = targetInfo!.Length;
                    result.TargetModified = targetInfo.LastWriteTime;
                }
                
                results.Add(result);
            }
            
            _logger.Info("ディレクトリ比較完了: {Count}件の差分を検出しました", results.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ディレクトリ比較に失敗しました");
            throw;
        }
        
        return results.OrderBy(r => r.FilePath).ToList();
    }
    
    public async Task LaunchWinMergeAsync(string leftFile, string rightFile)
    {
        try
        {
            var winMergePath = await _configurationService.GetSettingAsync("external_tools:winmerge_path", 
                @"C:\Program Files (x86)\WinMerge\WinMergeU.exe");
            
            if (!File.Exists(winMergePath))
            {
                throw new FileNotFoundException($"WinMergeが見つかりません: {winMergePath}");
            }
            
            if (!File.Exists(leftFile))
            {
                throw new FileNotFoundException($"比較対象ファイルが見つかりません: {leftFile}");
            }
            
            if (!File.Exists(rightFile))
            {
                throw new FileNotFoundException($"比較対象ファイルが見つかりません: {rightFile}");
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = winMergePath,
                Arguments = $"\"{leftFile}\" \"{rightFile}\"",
                UseShellExecute = true
            };
            
            _logger.Info("WinMergeを起動します: {Left} vs {Right}", leftFile, rightFile);
            
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await Task.Delay(1000); // プロセス起動の確認
                _logger.Info("WinMergeを起動しました (PID: {ProcessId})", process.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "WinMergeの起動に失敗しました");
            throw;
        }
    }
    
    public async Task<bool> IsWinMergeAvailableAsync()
    {
        try
        {
            var winMergePath = await _configurationService.GetSettingAsync("external_tools:winmerge_path", 
                @"C:\Program Files (x86)\WinMerge\WinMergeU.exe");
            
            return File.Exists(winMergePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "WinMergeの可用性チェックに失敗しました");
            return false;
        }
    }
}