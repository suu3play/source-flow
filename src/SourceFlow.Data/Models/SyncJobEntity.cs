using System.ComponentModel.DataAnnotations;

namespace SourceFlow.Data.Models;

public class SyncJobEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string JobName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string ServerHost { get; set; } = string.Empty;
    
    public DateTime SyncStart { get; set; }
    
    public DateTime? SyncEnd { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;
    
    public int FilesSynced { get; set; } = 0;
    
    public int ErrorsCount { get; set; } = 0;
    
    public string? LogMessage { get; set; }
}