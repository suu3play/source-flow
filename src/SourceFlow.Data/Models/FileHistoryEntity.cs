using System.ComponentModel.DataAnnotations;
using SourceFlow.Core.Enums;

namespace SourceFlow.Data.Models;

public class FileHistoryEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    public int Version { get; set; }
    
    [Required]
    [MaxLength(64)]
    public string HashValue { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    public DateTime ModifiedDate { get; set; }
    
    public DateTime CreatedDate { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string ChangeType { get; set; } = string.Empty;
}