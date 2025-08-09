using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using SourceFlow.Services.FileOperations;
using NLog;

namespace SourceFlow.Services.Sync;

public class LocalSyncService : ISourceSyncService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    
    public async Task<SyncJob> SyncAsync(SourceConfiguration source, CancellationToken cancellationToken = default)
    {
        var syncJob = new SyncJob
        {
            JobName = source.Name,
            ServerHost = source.SourcePath,
            SyncStart = DateTime.Now,
            Status = SyncStatus.InProgress
        };
        
        try
        {
            _logger.Info("ローカル同期開始: {Source} -> {Target}", source.SourcePath, source.LocalPath);
            
            if (!Directory.Exists(source.SourcePath))
            {
                throw new DirectoryNotFoundException($"ソースディレクトリが見つかりません: {source.SourcePath}");
            }
            
            if (!Directory.Exists(source.LocalPath))
            {
                Directory.CreateDirectory(source.LocalPath);
                _logger.Info("ターゲットディレクトリを作成しました: {Target}", source.LocalPath);
            }
            
            var filesSynced = await SyncDirectoryAsync(source.SourcePath, source.LocalPath, source.ExcludePatterns, cancellationToken);
            
            syncJob.FilesSynced = filesSynced;
            syncJob.Status = SyncStatus.Completed;
            syncJob.SyncEnd = DateTime.Now;
            
            _logger.Info("ローカル同期完了: {FileCount}件のファイルを同期しました", filesSynced);
        }
        catch (OperationCanceledException)
        {
            syncJob.Status = SyncStatus.Cancelled;
            syncJob.SyncEnd = DateTime.Now;
            _logger.Warn("ローカル同期がキャンセルされました");
        }
        catch (Exception ex)
        {
            syncJob.Status = SyncStatus.Failed;
            syncJob.SyncEnd = DateTime.Now;
            syncJob.ErrorsCount = 1;
            syncJob.LogMessage = ex.Message;
            _logger.Error(ex, "ローカル同期に失敗しました");
        }
        
        return syncJob;
    }
    
    public async Task<bool> TestConnectionAsync(SourceConfiguration source, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() => Directory.Exists(source.SourcePath), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ローカルパスの接続テストに失敗しました: {Path}", source.SourcePath);
            return false;
        }
    }
    
    public async Task<List<string>> ListFilesAsync(SourceConfiguration source, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(source.SourcePath))
            {
                return new List<string>();
            }
            
            var files = await Task.Run(() => 
                Directory.GetFiles(source.SourcePath, "*", SearchOption.AllDirectories)
                         .Select(f => Path.GetRelativePath(source.SourcePath, f))
                         .Where(f => !IsExcluded(f, source.ExcludePatterns))
                         .ToList(), 
                cancellationToken);
                
            return files;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイル一覧の取得に失敗しました: {Path}", source.SourcePath);
            return new List<string>();
        }
    }
    
    private async Task<int> SyncDirectoryAsync(string sourcePath, string targetPath, List<string> excludePatterns, CancellationToken cancellationToken)
    {
        var filesSynced = 0;
        var sourceFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
        
        foreach (var sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var relativePath = Path.GetRelativePath(sourcePath, sourceFile);
            
            if (IsExcluded(relativePath, excludePatterns))
            {
                continue;
            }
            
            var targetFile = Path.Combine(targetPath, relativePath);
            var targetDir = Path.GetDirectoryName(targetFile);
            
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir!);
            }
            
            var needsCopy = false;
            
            if (!File.Exists(targetFile))
            {
                needsCopy = true;
            }
            else
            {
                var sourceInfo = new FileInfo(sourceFile);
                var targetInfo = new FileInfo(targetFile);
                
                if (sourceInfo.LastWriteTime != targetInfo.LastWriteTime || 
                    sourceInfo.Length != targetInfo.Length)
                {
                    needsCopy = true;
                }
                else
                {
                    needsCopy = !await FileHashService.CompareFileHashesAsync(sourceFile, targetFile);
                }
            }
            
            if (needsCopy)
            {
                File.Copy(sourceFile, targetFile, true);
                filesSynced++;
                _logger.Debug("ファイルをコピーしました: {File}", relativePath);
            }
        }
        
        return filesSynced;
    }
    
    private static bool IsExcluded(string filePath, List<string> excludePatterns)
    {
        foreach (var pattern in excludePatterns)
        {
            if (MatchesPattern(filePath, pattern))
            {
                return true;
            }
        }
        return false;
    }
    
    private static bool MatchesPattern(string input, string pattern)
    {
        if (pattern.EndsWith("/"))
        {
            return input.StartsWith(pattern[..^1] + Path.DirectorySeparatorChar);
        }
        
        if (pattern.Contains("*"))
        {
            var regex = "^" + pattern.Replace("*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(input, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        
        return input.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}