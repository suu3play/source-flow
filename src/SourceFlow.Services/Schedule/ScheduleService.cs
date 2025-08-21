using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using SourceFlow.Data.Context;
using SourceFlow.Data.Models;

namespace SourceFlow.Services.Schedule;

public class ScheduleService : IScheduleService
{
    private readonly SourceFlowDbContext _context;
    private readonly IScheduleJobManager _jobManager;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(
        SourceFlowDbContext context,
        IScheduleJobManager jobManager,
        ILogger<ScheduleService> logger)
    {
        _context = context;
        _jobManager = jobManager;
        _logger = logger;
    }

    public async Task<ScheduledJob> CreateScheduledJobAsync(ScheduledJob job)
    {
        try
        {
            _logger.LogInformation("スケジュールジョブ作成開始: {JobName}", job.JobName);

            // Cron式の検証
            if (!ValidateCronExpression(job.CronExpression))
            {
                throw new ArgumentException($"無効なCron式です: {job.CronExpression}");
            }

            // 次回実行時間を計算
            job.NextExecutionTime = GetNextExecutionTime(job.CronExpression);

            var entity = new ScheduledJobEntity
            {
                JobName = job.JobName,
                Description = job.Description,
                CronExpression = job.CronExpression,
                Status = job.Status.ToString(),
                CreatedAt = job.CreatedAt,
                NextExecutionTime = job.NextExecutionTime,
                ReleaseConfigurationJson = job.ReleaseConfigurationJson,
                IsEnabled = job.IsEnabled,
                MaxRetryCount = job.MaxRetryCount
            };

            _context.ScheduledJobs.Add(entity);
            await _context.SaveChangesAsync();

            job.Id = entity.Id;

            // 有効な場合はスケジューラに登録
            if (job.IsEnabled && job.Status == ScheduleStatus.Active)
            {
                await _jobManager.ScheduleJobAsync(job);
            }

            _logger.LogInformation("スケジュールジョブが作成されました: {JobName} (ID: {JobId})", job.JobName, job.Id);
            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジュールジョブの作成に失敗しました: {JobName}", job.JobName);
            throw;
        }
    }

    public async Task<bool> UpdateScheduledJobAsync(ScheduledJob job)
    {
        try
        {
            var entity = await _context.ScheduledJobs.FindAsync(job.Id);
            if (entity == null)
            {
                _logger.LogWarning("更新対象のスケジュールジョブが見つかりません: ID={JobId}", job.Id);
                return false;
            }

            // Cron式が変更された場合は検証
            if (entity.CronExpression != job.CronExpression && !ValidateCronExpression(job.CronExpression))
            {
                throw new ArgumentException($"無効なCron式です: {job.CronExpression}");
            }

            // 次回実行時間を再計算
            job.NextExecutionTime = GetNextExecutionTime(job.CronExpression);

            // エンティティを更新
            entity.JobName = job.JobName;
            entity.Description = job.Description;
            entity.CronExpression = job.CronExpression;
            entity.Status = job.Status.ToString();
            entity.NextExecutionTime = job.NextExecutionTime;
            entity.ReleaseConfigurationJson = job.ReleaseConfigurationJson;
            entity.IsEnabled = job.IsEnabled;
            entity.MaxRetryCount = job.MaxRetryCount;
            entity.LastExecutionTime = job.LastExecutionTime;
            entity.LastErrorMessage = job.LastErrorMessage;

            await _context.SaveChangesAsync();

            // スケジューラでの登録状態を更新
            if (job.IsEnabled && job.Status == ScheduleStatus.Active)
            {
                await _jobManager.ScheduleJobAsync(job);
            }
            else
            {
                await _jobManager.UnscheduleJobAsync(job.Id);
            }

            _logger.LogInformation("スケジュールジョブが更新されました: {JobName} (ID: {JobId})", job.JobName, job.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジュールジョブの更新に失敗しました: ID={JobId}", job.Id);
            throw;
        }
    }

    public async Task<bool> DeleteScheduledJobAsync(int jobId)
    {
        try
        {
            var entity = await _context.ScheduledJobs
                .Include(j => j.Executions)
                .FirstOrDefaultAsync(j => j.Id == jobId);
                
            if (entity == null)
            {
                _logger.LogWarning("削除対象のスケジュールジョブが見つかりません: ID={JobId}", jobId);
                return false;
            }

            // スケジューラから削除
            await _jobManager.UnscheduleJobAsync(jobId);

            // データベースから削除（カスケード削除で実行履歴も削除される）
            _context.ScheduledJobs.Remove(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("スケジュールジョブが削除されました: {JobName} (ID: {JobId})", entity.JobName, jobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジュールジョブの削除に失敗しました: ID={JobId}", jobId);
            throw;
        }
    }

    public async Task<List<ScheduledJob>> GetAllScheduledJobsAsync()
    {
        try
        {
            var entities = await _context.ScheduledJobs
                .OrderBy(j => j.JobName)
                .ToListAsync();

            return entities.Select(ConvertToModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジュールジョブ一覧の取得に失敗しました");
            return new List<ScheduledJob>();
        }
    }

    public async Task<ScheduledJob?> GetScheduledJobByIdAsync(int jobId)
    {
        try
        {
            var entity = await _context.ScheduledJobs.FindAsync(jobId);
            return entity != null ? ConvertToModel(entity) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジュールジョブの取得に失敗しました: ID={JobId}", jobId);
            return null;
        }
    }

    public async Task<bool> StartScheduledJobAsync(int jobId)
    {
        var job = await GetScheduledJobByIdAsync(jobId);
        if (job == null) return false;

        job.Status = ScheduleStatus.Active;
        job.IsEnabled = true;
        
        return await UpdateScheduledJobAsync(job);
    }

    public async Task<bool> StopScheduledJobAsync(int jobId)
    {
        var job = await GetScheduledJobByIdAsync(jobId);
        if (job == null) return false;

        job.Status = ScheduleStatus.Inactive;
        job.IsEnabled = false;
        
        return await UpdateScheduledJobAsync(job);
    }

    public async Task<bool> PauseScheduledJobAsync(int jobId)
    {
        var job = await GetScheduledJobByIdAsync(jobId);
        if (job == null) return false;

        job.Status = ScheduleStatus.Paused;
        var result = await UpdateScheduledJobAsync(job);
        
        if (result)
        {
            await _jobManager.PauseJobAsync(jobId);
        }
        
        return result;
    }

    public async Task<bool> ResumeScheduledJobAsync(int jobId)
    {
        var job = await GetScheduledJobByIdAsync(jobId);
        if (job == null) return false;

        job.Status = ScheduleStatus.Active;
        var result = await UpdateScheduledJobAsync(job);
        
        if (result)
        {
            await _jobManager.ResumeJobAsync(jobId);
        }
        
        return result;
    }

    public async Task<bool> TriggerJobAsync(int jobId)
    {
        return await _jobManager.TriggerJobAsync(jobId);
    }

    public async Task<List<ScheduleExecution>> GetExecutionHistoryAsync(int jobId, int? limit = null)
    {
        try
        {
            var query = _context.ScheduleExecutions
                .Where(e => e.ScheduledJobId == jobId)
                .OrderByDescending(e => e.StartTime);

            if (limit.HasValue)
            {
                query = (IOrderedQueryable<ScheduleExecutionEntity>)query.Take(limit.Value);
            }

            var entities = await query.ToListAsync();
            return entities.Select(ConvertExecutionToModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "実行履歴の取得に失敗しました: JobId={JobId}", jobId);
            return new List<ScheduleExecution>();
        }
    }

    public async Task<ScheduleStatistics> GetScheduleStatisticsAsync()
    {
        try
        {
            var totalJobs = await _context.ScheduledJobs.CountAsync();
            var activeJobs = await _context.ScheduledJobs
                .Where(j => j.IsEnabled && j.Status == ScheduleStatus.Active.ToString())
                .CountAsync();

            var totalExecutions = await _context.ScheduleExecutions.CountAsync();
            var successfulExecutions = await _context.ScheduleExecutions
                .Where(e => e.Status == ScheduleExecutionStatus.Completed.ToString())
                .CountAsync();
            var failedExecutions = await _context.ScheduleExecutions
                .Where(e => e.Status == ScheduleExecutionStatus.Failed.ToString())
                .CountAsync();

            var lastExecution = await _context.ScheduleExecutions
                .OrderByDescending(e => e.StartTime)
                .Select(e => e.StartTime)
                .FirstOrDefaultAsync();

            return new ScheduleStatistics
            {
                TotalJobs = totalJobs,
                ActiveJobs = activeJobs,
                TotalExecutions = totalExecutions,
                SuccessfulExecutions = successfulExecutions,
                FailedExecutions = failedExecutions,
                LastExecutionTime = lastExecution == default ? null : lastExecution
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "スケジュール統計の取得に失敗しました");
            return new ScheduleStatistics();
        }
    }

    public bool ValidateCronExpression(string cronExpression)
    {
        try
        {
            CronExpression.ValidateExpression(cronExpression);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public DateTime? GetNextExecutionTime(string cronExpression)
    {
        try
        {
            var cron = new CronExpression(cronExpression);
            return cron.GetNextValidTimeAfter(DateTime.Now)?.DateTime;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "次回実行時間の計算に失敗しました: {CronExpression}", cronExpression);
            return null;
        }
    }

    private static ScheduledJob ConvertToModel(ScheduledJobEntity entity)
    {
        return new ScheduledJob
        {
            Id = entity.Id,
            JobName = entity.JobName,
            Description = entity.Description,
            CronExpression = entity.CronExpression,
            Status = Enum.TryParse<ScheduleStatus>(entity.Status, out var status) ? status : ScheduleStatus.Active,
            CreatedAt = entity.CreatedAt,
            LastExecutionTime = entity.LastExecutionTime,
            NextExecutionTime = entity.NextExecutionTime,
            ReleaseConfigurationJson = entity.ReleaseConfigurationJson,
            IsEnabled = entity.IsEnabled,
            MaxRetryCount = entity.MaxRetryCount,
            LastErrorMessage = entity.LastErrorMessage
        };
    }

    private static ScheduleExecution ConvertExecutionToModel(ScheduleExecutionEntity entity)
    {
        return new ScheduleExecution
        {
            Id = entity.Id,
            ScheduledJobId = entity.ScheduledJobId,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Status = Enum.TryParse<ScheduleExecutionStatus>(entity.Status, out var status) ? 
                status : ScheduleExecutionStatus.Running,
            ErrorMessage = entity.ErrorMessage,
            FilesProcessed = entity.FilesProcessed,
            BackupPath = entity.BackupPath,
            RetryCount = entity.RetryCount
        };
    }
}