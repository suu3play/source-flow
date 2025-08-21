using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface IScheduleService
{
    /// <summary>
    /// 新しいスケジュールジョブを作成
    /// </summary>
    Task<ScheduledJob> CreateScheduledJobAsync(ScheduledJob job);

    /// <summary>
    /// スケジュールジョブを更新
    /// </summary>
    Task<bool> UpdateScheduledJobAsync(ScheduledJob job);

    /// <summary>
    /// スケジュールジョブを削除
    /// </summary>
    Task<bool> DeleteScheduledJobAsync(int jobId);

    /// <summary>
    /// すべてのスケジュールジョブを取得
    /// </summary>
    Task<List<ScheduledJob>> GetAllScheduledJobsAsync();

    /// <summary>
    /// IDでスケジュールジョブを取得
    /// </summary>
    Task<ScheduledJob?> GetScheduledJobByIdAsync(int jobId);

    /// <summary>
    /// スケジュールジョブを開始
    /// </summary>
    Task<bool> StartScheduledJobAsync(int jobId);

    /// <summary>
    /// スケジュールジョブを停止
    /// </summary>
    Task<bool> StopScheduledJobAsync(int jobId);

    /// <summary>
    /// スケジュールジョブを一時停止
    /// </summary>
    Task<bool> PauseScheduledJobAsync(int jobId);

    /// <summary>
    /// スケジュールジョブを再開
    /// </summary>
    Task<bool> ResumeScheduledJobAsync(int jobId);

    /// <summary>
    /// スケジュールジョブを即座に実行
    /// </summary>
    Task<bool> TriggerJobAsync(int jobId);

    /// <summary>
    /// ジョブの実行履歴を取得
    /// </summary>
    Task<List<ScheduleExecution>> GetExecutionHistoryAsync(int jobId, int? limit = null);

    /// <summary>
    /// スケジュール統計を取得
    /// </summary>
    Task<ScheduleStatistics> GetScheduleStatisticsAsync();

    /// <summary>
    /// Cron式が有効かどうかを検証
    /// </summary>
    bool ValidateCronExpression(string cronExpression);

    /// <summary>
    /// Cron式から次の実行時間を計算
    /// </summary>
    DateTime? GetNextExecutionTime(string cronExpression);
}