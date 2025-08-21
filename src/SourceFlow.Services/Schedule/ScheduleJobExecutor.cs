using Microsoft.Extensions.Logging;
using Quartz;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using System.Text.Json;

namespace SourceFlow.Services.Schedule;

[DisallowConcurrentExecution]
public class ScheduleJobExecutor : IScheduleJobExecutor
{
    private readonly ILogger<ScheduleJobExecutor> _logger;
    private readonly IScheduleService _scheduleService;
    private readonly IReleaseService _releaseService;

    public ScheduleJobExecutor(
        ILogger<ScheduleJobExecutor> logger,
        IScheduleService scheduleService,
        IReleaseService releaseService)
    {
        _logger = logger;
        _scheduleService = scheduleService;
        _releaseService = releaseService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.JobDetail.JobDataMap.GetInt("JobId");
        
        try
        {
            _logger.LogInformation("スケジュールジョブ実行開始: JobId={JobId}", jobId);
            
            var job = await _scheduleService.GetScheduledJobByIdAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("スケジュールジョブが見つかりません: JobId={JobId}", jobId);
                return;
            }

            await ExecuteScheduledJobAsync(job, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジュールジョブ実行エラー: JobId={JobId}", jobId);
            throw new JobExecutionException(ex);
        }
    }

    public async Task ExecuteScheduledJobAsync(ScheduledJob job, CancellationToken cancellationToken = default)
    {
        var execution = new ScheduleExecution
        {
            ScheduledJobId = job.Id,
            StartTime = DateTime.Now,
            Status = ScheduleExecutionStatus.Running
        };

        try
        {
            _logger.LogInformation("スケジュールジョブ実行開始: {JobName}", job.JobName);

            // 実行履歴記録を開始
            var executionId = await RecordExecutionStartAsync(execution);
            execution.Id = executionId;

            // リリース設定をデシリアライズ
            var releaseConfig = JsonSerializer.Deserialize<ReleaseConfiguration>(job.ReleaseConfigurationJson);
            if (releaseConfig == null)
            {
                throw new InvalidOperationException("リリース設定が無効です");
            }

            releaseConfig.ReleaseName = $"{job.JobName}_{DateTime.Now:yyyyMMdd_HHmmss}";

            // リリース実行
            var releaseResult = await _releaseService.CreateReleaseAsync(releaseConfig);
            
            // 実行結果を記録
            execution.EndTime = DateTime.Now;
            execution.FilesProcessed = releaseResult.FilesProcessed;
            execution.BackupPath = releaseResult.BackupPath;
            
            if (releaseResult.Success)
            {
                execution.Status = ScheduleExecutionStatus.Completed;
                _logger.LogInformation("スケジュールジョブ実行完了: {JobName}, 処理ファイル数: {Count}", 
                    job.JobName, releaseResult.FilesProcessed);
                
                // ジョブの成功情報を更新
                await UpdateJobLastExecutionAsync(job.Id, DateTime.Now, null);
            }
            else
            {
                execution.Status = ScheduleExecutionStatus.Failed;
                execution.ErrorMessage = releaseResult.ErrorMessage;
                _logger.LogError("スケジュールジョブ実行失敗: {JobName}, エラー: {Error}", 
                    job.JobName, releaseResult.ErrorMessage);
                
                // エラー情報を更新
                await UpdateJobLastExecutionAsync(job.Id, DateTime.Now, releaseResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            execution.EndTime = DateTime.Now;
            execution.Status = ScheduleExecutionStatus.Failed;
            execution.ErrorMessage = ex.Message;
            
            _logger.LogError(ex, "スケジュールジョブ実行中に例外が発生: {JobName}", job.JobName);
            
            // エラー情報を更新
            await UpdateJobLastExecutionAsync(job.Id, DateTime.Now, ex.Message);
            
            // リトライ判定
            if (execution.RetryCount < job.MaxRetryCount)
            {
                execution.RetryCount++;
                execution.Status = ScheduleExecutionStatus.Retrying;
                _logger.LogInformation("スケジュールジョブをリトライします: {JobName}, リトライ回数: {RetryCount}/{MaxRetry}", 
                    job.JobName, execution.RetryCount, job.MaxRetryCount);
                
                // 遅延後にリトライ（指数バックオフ）
                var delayMs = (int)Math.Pow(2, execution.RetryCount) * 1000;
                await Task.Delay(delayMs, cancellationToken);
                
                await ExecuteScheduledJobAsync(job, cancellationToken);
                return;
            }
        }
        finally
        {
            // 実行履歴を更新
            await RecordExecutionEndAsync(execution);
        }
    }

    private async Task<int> RecordExecutionStartAsync(ScheduleExecution execution)
    {
        // 実際の実装では ScheduleService または Database Service を使用
        // ここでは簡易的な実装
        _logger.LogDebug("実行履歴記録開始: ScheduledJobId={ScheduledJobId}", execution.ScheduledJobId);
        return 1; // 仮のID
    }

    private Task RecordExecutionEndAsync(ScheduleExecution execution)
    {
        // 実際の実装では ScheduleService または Database Service を使用
        _logger.LogDebug("実行履歴記録終了: ExecutionId={ExecutionId}, Status={Status}", 
            execution.Id, execution.Status);
        return Task.CompletedTask;
    }

    private async Task UpdateJobLastExecutionAsync(int jobId, DateTime lastExecution, string? errorMessage)
    {
        var job = await _scheduleService.GetScheduledJobByIdAsync(jobId);
        if (job != null)
        {
            job.LastExecutionTime = lastExecution;
            job.LastErrorMessage = errorMessage;
            
            // 次回実行時間を計算
            job.NextExecutionTime = _scheduleService.GetNextExecutionTime(job.CronExpression);
            
            await _scheduleService.UpdateScheduledJobAsync(job);
        }
    }
}