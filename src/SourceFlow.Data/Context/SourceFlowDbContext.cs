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
    }
}