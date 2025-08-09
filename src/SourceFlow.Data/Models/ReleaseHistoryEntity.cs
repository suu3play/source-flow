using System.ComponentModel.DataAnnotations;

namespace SourceFlow.Data.Models;

public class ReleaseHistoryEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string ReleaseName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string SourcePath { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string TargetPath { get; set; } = string.Empty;
    
    public DateTime ReleaseDate { get; set; }
    
    public int FilesReleased { get; set; } = 0;
    
    [MaxLength(500)]
    public string? BackupPath { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;
}