using Quartz;
using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface IScheduleJobExecutor : IJob
{
    /// <summary>
    /// スケジュールジョブを実行
    /// </summary>
    Task ExecuteScheduledJobAsync(ScheduledJob job, CancellationToken cancellationToken = default);
}

public interface IScheduleJobManager
{
    /// <summary>
    /// Quartzスケジューラにジョブを追加
    /// </summary>
    Task<bool> ScheduleJobAsync(ScheduledJob job);

    /// <summary>
    /// Quartzスケジューラからジョブを削除
    /// </summary>
    Task<bool> UnscheduleJobAsync(int jobId);

    /// <summary>
    /// ジョブを一時停止
    /// </summary>
    Task<bool> PauseJobAsync(int jobId);

    /// <summary>
    /// ジョブを再開
    /// </summary>
    Task<bool> ResumeJobAsync(int jobId);

    /// <summary>
    /// ジョブを即座に実行
    /// </summary>
    Task<bool> TriggerJobAsync(int jobId);

    /// <summary>
    /// 実行中のジョブをキャンセル
    /// </summary>
    Task<bool> CancelJobAsync(int jobId);

    /// <summary>
    /// スケジューラの状態を取得
    /// </summary>
    Task<bool> IsSchedulerRunningAsync();

    /// <summary>
    /// スケジューラを開始
    /// </summary>
    Task StartSchedulerAsync();

    /// <summary>
    /// スケジューラを停止
    /// </summary>
    Task StopSchedulerAsync();
}