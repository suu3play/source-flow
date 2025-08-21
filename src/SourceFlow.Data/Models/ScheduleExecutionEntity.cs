using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SourceFlow.Core.Enums;

namespace SourceFlow.Data.Models;

public class ScheduleExecutionEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ScheduledJobId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Required]
    public string Status { get; set; } = ScheduleExecutionStatus.Running.ToString();

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [Required]
    public int FilesProcessed { get; set; } = 0;

    [MaxLength(500)]
    public string? BackupPath { get; set; }

    [Required]
    public int RetryCount { get; set; } = 0;

    // Foreign key navigation
    [ForeignKey(nameof(ScheduledJobId))]
    public virtual ScheduledJobEntity ScheduledJob { get; set; } = null!;
}