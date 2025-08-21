using System.ComponentModel.DataAnnotations;
using SourceFlow.Core.Enums;

namespace SourceFlow.Data.Models;

public class ScheduledJobEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string JobName { get; set; } = "";

    [MaxLength(1000)]
    public string Description { get; set; } = "";

    [Required]
    [MaxLength(100)]
    public string CronExpression { get; set; } = "";

    [Required]
    public string Status { get; set; } = ScheduleStatus.Active.ToString();

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? LastExecutionTime { get; set; }

    public DateTime? NextExecutionTime { get; set; }

    [Required]
    public string ReleaseConfigurationJson { get; set; } = "";

    [Required]
    public bool IsEnabled { get; set; } = true;

    [Required]
    public int MaxRetryCount { get; set; } = 3;

    [MaxLength(2000)]
    public string? LastErrorMessage { get; set; }

    // Navigation property
    public virtual ICollection<ScheduleExecutionEntity> Executions { get; set; } = new List<ScheduleExecutionEntity>();
}