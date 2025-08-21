using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface IReleaseService
{
    Task<ReleaseResult> CreateReleaseAsync(ReleaseConfiguration config);
    Task<List<ReleaseHistory>> GetReleaseHistoryAsync();
    Task<bool> RestoreFromBackupAsync(string backupPath, string targetPath);
    Task<bool> DeleteReleaseAsync(int releaseId);
    Task<ReleaseStatistics> GetReleaseStatisticsAsync();
}