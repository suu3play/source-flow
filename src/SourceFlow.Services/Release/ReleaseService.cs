using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using SourceFlow.Data.Context;
using SourceFlow.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace SourceFlow.Services.Release;

public interface IReleaseService
{
    Task<ReleaseResult> CreateReleaseAsync(ReleaseConfiguration config);
    Task<List<ReleaseHistory>> GetReleaseHistoryAsync();
    Task<bool> RestoreFromBackupAsync(string backupPath, string targetPath);
    Task<bool> DeleteReleaseAsync(int releaseId);
    Task<ReleaseStatistics> GetReleaseStatisticsAsync();
}

public class ReleaseService : IReleaseService
{
    private readonly ILogger<ReleaseService> _logger;
    private readonly IComparisonService _comparisonService;
    private readonly SourceFlowDbContext _context;
    private readonly INotificationService _notificationService;

    public ReleaseService(
        ILogger<ReleaseService> logger,
        IComparisonService comparisonService,
        SourceFlowDbContext context,
        INotificationService notificationService)
    {
        _logger = logger;
        _comparisonService = comparisonService;
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<ReleaseResult> CreateReleaseAsync(ReleaseConfiguration config)
    {
        var result = new ReleaseResult
        {
            ReleaseName = config.ReleaseName,
            StartTime = DateTime.Now
        };

        try
        {
            _logger.LogInformation("リリース作成開始: {ReleaseName}", config.ReleaseName);

            // 1. ソースとターゲットの比較
            var comparisons = await _comparisonService.CompareDirectoriesAsync(config.SourcePath, config.TargetPath);
            var filesToRelease = comparisons.Where(c => c.Selected).ToList();

            if (!filesToRelease.Any())
            {
                result.Success = false;
                result.ErrorMessage = "リリース対象のファイルがありません";
                return result;
            }

            // 2. バックアップ作成（オプション）
            if (config.CreateBackup)
            {
                result.BackupPath = await CreateBackupAsync(config.TargetPath, config.ReleaseName);
                _logger.LogInformation("バックアップを作成しました: {BackupPath}", result.BackupPath);
            }

            // 3. ファイルのリリース処理
            var processedFiles = 0;
            var errors = new List<string>();

            foreach (var file in filesToRelease)
            {
                try
                {
                    await ProcessFileAsync(config, file);
                    processedFiles++;
                    
                    // 進捗報告
                    result.Progress = (processedFiles * 100) / filesToRelease.Count;
                }
                catch (Exception ex)
                {
                    var errorMsg = $"ファイル処理エラー [{file.FilePath}]: {ex.Message}";
                    errors.Add(errorMsg);
                    _logger.LogError(ex, errorMsg);
                }
            }

            // 4. 結果の記録
            result.FilesProcessed = processedFiles;
            result.ErrorsCount = errors.Count;
            result.EndTime = DateTime.Now;
            result.Success = errors.Count == 0;
            result.Errors = errors;

            // 5. データベースに履歴を保存
            await SaveReleaseHistoryAsync(result, config, filesToRelease.Count);

            _logger.LogInformation("リリース完了: {ReleaseName}, 処理ファイル数: {Count}, エラー数: {Errors}", 
                config.ReleaseName, processedFiles, errors.Count);

            // 通知を送信
            if (result.Success)
            {
                await _notificationService.ShowReleaseCompletedNotificationAsync(
                    config.ReleaseName, processedFiles, result.Duration);
            }
            else
            {
                await _notificationService.ShowReleaseErrorNotificationAsync(
                    config.ReleaseName, result.ErrorMessage, errors.Count);
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.EndTime = DateTime.Now;
            _logger.LogError(ex, "リリース作成に失敗しました: {ReleaseName}", config.ReleaseName);

            // エラー通知を送信
            await _notificationService.ShowReleaseErrorNotificationAsync(
                config.ReleaseName, ex.Message, 1);
        }

        return result;
    }

    public async Task<List<ReleaseHistory>> GetReleaseHistoryAsync()
    {
        try
        {
            var histories = await _context.ReleaseHistory
                .OrderByDescending(r => r.ReleaseDate)
                .ToListAsync();

            return histories.Select(h => new ReleaseHistory
            {
                Id = h.Id,
                ReleaseName = h.ReleaseName,
                SourcePath = h.SourcePath,
                TargetPath = h.TargetPath,
                ReleaseDate = h.ReleaseDate,
                FilesReleased = h.FilesReleased,
                BackupPath = h.BackupPath,
                Status = Enum.TryParse<SyncStatus>(h.Status, out var status) ? status : SyncStatus.Failed
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "リリース履歴の取得に失敗しました");
            return new List<ReleaseHistory>();
        }
    }

    public async Task<bool> RestoreFromBackupAsync(string backupPath, string targetPath)
    {
        try
        {
            _logger.LogInformation("バックアップから復元開始: {BackupPath} → {TargetPath}", backupPath, targetPath);

            if (!File.Exists(backupPath))
            {
                _logger.LogError("バックアップファイルが見つかりません: {BackupPath}", backupPath);
                return false;
            }

            // バックアップディレクトリを作成
            var restoreTemp = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid()}");
            Directory.CreateDirectory(restoreTemp);

            try
            {
                // ZIPファイルを展開
                ZipFile.ExtractToDirectory(backupPath, restoreTemp);

                // ターゲットディレクトリの内容を置換
                if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, true);
                }
                Directory.CreateDirectory(targetPath);

                // 復元ファイルをコピー
                await CopyDirectoryAsync(restoreTemp, targetPath);

                _logger.LogInformation("バックアップからの復元が完了しました");
                return true;
            }
            finally
            {
                // 一時ディレクトリを削除
                if (Directory.Exists(restoreTemp))
                {
                    Directory.Delete(restoreTemp, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "バックアップからの復元に失敗しました");
            return false;
        }
    }

    public async Task<bool> DeleteReleaseAsync(int releaseId)
    {
        try
        {
            var release = await _context.ReleaseHistory.FindAsync(releaseId);
            if (release != null)
            {
                _context.ReleaseHistory.Remove(release);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("リリース履歴を削除しました: ID={Id}", releaseId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "リリース履歴の削除に失敗しました: ID={Id}", releaseId);
            return false;
        }
    }

    public async Task<ReleaseStatistics> GetReleaseStatisticsAsync()
    {
        try
        {
            var totalReleases = await _context.ReleaseHistory.CountAsync();
            var successfulReleases = await _context.ReleaseHistory
                .Where(r => r.Status == SyncStatus.Completed.ToString())
                .CountAsync();
            
            var lastWeekReleases = await _context.ReleaseHistory
                .Where(r => r.ReleaseDate >= DateTime.Now.AddDays(-7))
                .CountAsync();

            var totalFiles = await _context.ReleaseHistory
                .SumAsync(r => r.FilesReleased);

            return new ReleaseStatistics
            {
                TotalReleases = totalReleases,
                SuccessfulReleases = successfulReleases,
                LastWeekReleases = lastWeekReleases,
                TotalFilesReleased = totalFiles,
                SuccessRate = totalReleases > 0 ? (successfulReleases * 100.0 / totalReleases) : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "リリース統計の取得に失敗しました");
            return new ReleaseStatistics();
        }
    }

    private async Task<string> CreateBackupAsync(string targetPath, string releaseName)
    {
        var backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"backup_{releaseName}_{timestamp}.zip";
        var backupPath = Path.Combine(backupDir, backupFileName);

        await Task.Run(() =>
        {
            ZipFile.CreateFromDirectory(targetPath, backupPath, CompressionLevel.Optimal, false);
        });

        return backupPath;
    }

    private async Task ProcessFileAsync(ReleaseConfiguration config, FileComparisonResult file)
    {
        var sourcePath = Path.Combine(config.SourcePath, file.FilePath);
        var targetPath = Path.Combine(config.TargetPath, file.FilePath);

        switch (file.ChangeType)
        {
            case ChangeType.Add:
            case ChangeType.Modify:
                // ディレクトリを作成
                var targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                
                // ファイルをコピー
                await Task.Run(() => File.Copy(sourcePath, targetPath, true));
                break;

            case ChangeType.Delete:
                // ファイルを削除
                if (File.Exists(targetPath))
                {
                    await Task.Run(() => File.Delete(targetPath));
                }
                break;
        }
    }

    private async Task CopyDirectoryAsync(string sourceDir, string targetDir)
    {
        await Task.Run(() =>
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var targetFile = Path.Combine(targetDir, relativePath);
                var targetFileDir = Path.GetDirectoryName(targetFile);
                
                if (!string.IsNullOrEmpty(targetFileDir) && !Directory.Exists(targetFileDir))
                {
                    Directory.CreateDirectory(targetFileDir);
                }
                
                File.Copy(file, targetFile, true);
            }
        });
    }

    private async Task SaveReleaseHistoryAsync(ReleaseResult result, ReleaseConfiguration config, int totalFiles)
    {
        var history = new ReleaseHistoryEntity
        {
            ReleaseName = config.ReleaseName,
            SourcePath = config.SourcePath,
            TargetPath = config.TargetPath,
            ReleaseDate = result.StartTime,
            FilesReleased = result.FilesProcessed,
            BackupPath = result.BackupPath,
            Status = result.Success ? SyncStatus.Completed.ToString() : SyncStatus.Failed.ToString()
        };

        _context.ReleaseHistory.Add(history);
        await _context.SaveChangesAsync();
    }
}

public class ReleaseConfiguration
{
    public string ReleaseName { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public bool CreateBackup { get; set; } = true;
    public List<FileComparisonResult> SelectedFiles { get; set; } = new();
}

public class ReleaseResult
{
    public string ReleaseName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
    public List<string> Errors { get; set; } = new();
    public int FilesProcessed { get; set; }
    public int ErrorsCount { get; set; }
    public int Progress { get; set; }
    public string? BackupPath { get; set; }
    
    public TimeSpan Duration => EndTime - StartTime;
}

public class ReleaseStatistics
{
    public int TotalReleases { get; set; }
    public int SuccessfulReleases { get; set; }
    public int LastWeekReleases { get; set; }
    public int TotalFilesReleased { get; set; }
    public double SuccessRate { get; set; }
}