using Microsoft.Extensions.Logging;
using Quartz;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;

namespace SourceFlow.Services.Schedule;

public class ScheduleJobManager : IScheduleJobManager
{
    private readonly IScheduler _scheduler;
    private readonly ILogger<ScheduleJobManager> _logger;

    public ScheduleJobManager(IScheduler scheduler, ILogger<ScheduleJobManager> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
    }

    public async Task<bool> ScheduleJobAsync(ScheduledJob job)
    {
        try
        {
            var jobKey = new JobKey($"job_{job.Id}", "scheduled_jobs");
            var triggerKey = new TriggerKey($"trigger_{job.Id}", "scheduled_jobs");

            // 既存のジョブを削除
            if (await _scheduler.CheckExists(jobKey))
            {
                await _scheduler.DeleteJob(jobKey);
            }

            // ジョブ詳細を作成
            var jobDetail = JobBuilder.Create<ScheduleJobExecutor>()
                .WithIdentity(jobKey)
                .UsingJobData("JobId", job.Id)
                .Build();

            // トリガーを作成
            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithCronSchedule(job.CronExpression)
                .Build();

            // スケジューラにジョブを追加
            await _scheduler.ScheduleJob(jobDetail, trigger);

            _logger.LogInformation("ジョブをスケジュールしました: {JobName} (ID: {JobId})", job.JobName, job.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブのスケジュール設定に失敗しました: {JobName} (ID: {JobId})", job.JobName, job.Id);
            return false;
        }
    }

    public async Task<bool> UnscheduleJobAsync(int jobId)
    {
        try
        {
            var jobKey = new JobKey($"job_{jobId}", "scheduled_jobs");
            var result = await _scheduler.DeleteJob(jobKey);
            
            if (result)
            {
                _logger.LogInformation("ジョブのスケジュールを削除しました: JobId={JobId}", jobId);
            }
            else
            {
                _logger.LogWarning("削除対象のジョブが見つかりませんでした: JobId={JobId}", jobId);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブのスケジュール削除に失敗しました: JobId={JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> PauseJobAsync(int jobId)
    {
        try
        {
            var jobKey = new JobKey($"job_{jobId}", "scheduled_jobs");
            await _scheduler.PauseJob(jobKey);
            
            _logger.LogInformation("ジョブを一時停止しました: JobId={JobId}", jobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブの一時停止に失敗しました: JobId={JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> ResumeJobAsync(int jobId)
    {
        try
        {
            var jobKey = new JobKey($"job_{jobId}", "scheduled_jobs");
            await _scheduler.ResumeJob(jobKey);
            
            _logger.LogInformation("ジョブを再開しました: JobId={JobId}", jobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブの再開に失敗しました: JobId={JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> TriggerJobAsync(int jobId)
    {
        try
        {
            var jobKey = new JobKey($"job_{jobId}", "scheduled_jobs");
            await _scheduler.TriggerJob(jobKey);
            
            _logger.LogInformation("ジョブを手動実行しました: JobId={JobId}", jobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブの手動実行に失敗しました: JobId={JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> CancelJobAsync(int jobId)
    {
        try
        {
            var jobKey = new JobKey($"job_{jobId}", "scheduled_jobs");
            
            // 実行中のジョブを取得
            var executingJobs = await _scheduler.GetCurrentlyExecutingJobs();
            var targetJob = executingJobs.FirstOrDefault(j => j.JobDetail.Key.Equals(jobKey));
            
            if (targetJob != null)
            {
                await _scheduler.Interrupt(jobKey);
                _logger.LogInformation("実行中のジョブをキャンセルしました: JobId={JobId}", jobId);
                return true;
            }
            else
            {
                _logger.LogWarning("実行中のジョブが見つかりませんでした: JobId={JobId}", jobId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ジョブのキャンセルに失敗しました: JobId={JobId}", jobId);
            return false;
        }
    }

    public async Task<bool> IsSchedulerRunningAsync()
    {
        try
        {
            return _scheduler.IsStarted && !_scheduler.IsShutdown;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジューラ状態の取得に失敗しました");
            return false;
        }
    }

    public async Task StartSchedulerAsync()
    {
        try
        {
            if (!_scheduler.IsStarted)
            {
                await _scheduler.Start();
                _logger.LogInformation("スケジューラを開始しました");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジューラの開始に失敗しました");
            throw;
        }
    }

    public async Task StopSchedulerAsync()
    {
        try
        {
            if (!_scheduler.IsShutdown)
            {
                await _scheduler.Shutdown(true);
                _logger.LogInformation("スケジューラを停止しました");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジューラの停止に失敗しました");
            throw;
        }
    }
}