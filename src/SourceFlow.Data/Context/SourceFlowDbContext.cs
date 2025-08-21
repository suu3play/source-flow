using Microsoft.EntityFrameworkCore;
using SourceFlow.Data.Models;

namespace SourceFlow.Data.Context;

public class SourceFlowDbContext : DbContext
{
    public SourceFlowDbContext(DbContextOptions<SourceFlowDbContext> options) : base(options)
    {
    }
    
    public DbSet<FileHistoryEntity> FileHistory { get; set; }
    public DbSet<SyncJobEntity> SyncJobs { get; set; }
    public DbSet<ReleaseHistoryEntity> ReleaseHistory { get; set; }
    public DbSet<NotificationHistoryEntity> NotificationHistory { get; set; }
    public DbSet<ScheduledJobEntity> ScheduledJobs { get; set; }
    public DbSet<ScheduleExecutionEntity> ScheduleExecutions { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // FileHistory インデックス
        modelBuilder.Entity<FileHistoryEntity>()
            .HasIndex(e => e.FilePath)
            .HasDatabaseName("IX_FileHistory_FilePath");
            
        modelBuilder.Entity<FileHistoryEntity>()
            .HasIndex(e => new { e.FilePath, e.Version })
            .IsUnique()
            .HasDatabaseName("IX_FileHistory_FilePath_Version");
            
        // SyncJobs インデックス
        modelBuilder.Entity<SyncJobEntity>()
            .HasIndex(e => e.SyncStart)
            .HasDatabaseName("IX_SyncJobs_SyncStart");
            
        // ReleaseHistory インデックス
        modelBuilder.Entity<ReleaseHistoryEntity>()
            .HasIndex(e => e.ReleaseDate)
            .HasDatabaseName("IX_ReleaseHistory_ReleaseDate");
            
        // NotificationHistory インデックス
        modelBuilder.Entity<NotificationHistoryEntity>()
            .HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_NotificationHistory_CreatedAt");
            
        modelBuilder.Entity<NotificationHistoryEntity>()
            .HasIndex(e => e.IsRead)
            .HasDatabaseName("IX_NotificationHistory_IsRead");
            
        modelBuilder.Entity<NotificationHistoryEntity>()
            .HasIndex(e => e.Type)
            .HasDatabaseName("IX_NotificationHistory_Type");
            
        // ScheduledJobs インデックス
        modelBuilder.Entity<ScheduledJobEntity>()
            .HasIndex(e => e.JobName)
            .IsUnique()
            .HasDatabaseName("IX_ScheduledJobs_JobName");
            
        modelBuilder.Entity<ScheduledJobEntity>()
            .HasIndex(e => e.IsEnabled)
            .HasDatabaseName("IX_ScheduledJobs_IsEnabled");
            
        modelBuilder.Entity<ScheduledJobEntity>()
            .HasIndex(e => e.NextExecutionTime)
            .HasDatabaseName("IX_ScheduledJobs_NextExecutionTime");
            
        // ScheduleExecutions インデックス
        modelBuilder.Entity<ScheduleExecutionEntity>()
            .HasIndex(e => e.ScheduledJobId)
            .HasDatabaseName("IX_ScheduleExecutions_ScheduledJobId");
            
        modelBuilder.Entity<ScheduleExecutionEntity>()
            .HasIndex(e => e.StartTime)
            .HasDatabaseName("IX_ScheduleExecutions_StartTime");
            
        modelBuilder.Entity<ScheduleExecutionEntity>()
            .HasIndex(e => e.Status)
            .HasDatabaseName("IX_ScheduleExecutions_Status");
            
        // ScheduledJobs と ScheduleExecutions の関係
        modelBuilder.Entity<ScheduleExecutionEntity>()
            .HasOne(e => e.ScheduledJob)
            .WithMany(j => j.Executions)
            .HasForeignKey(e => e.ScheduledJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}