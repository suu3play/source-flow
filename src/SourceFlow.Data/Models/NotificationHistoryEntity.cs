using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SourceFlow.Data.Models;

[Table("NotificationHistory")]
public class NotificationHistoryEntity
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";
    
    [StringLength(1000)]
    public string Message { get; set; } = "";
    
    [Required]
    [StringLength(20)]
    public string Type { get; set; } = ""; // Info, Success, Warning, Error
    
    [Required]
    [StringLength(20)]
    public string Priority { get; set; } = ""; // Low, Normal, High, Critical
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    public bool IsRead { get; set; } = false;
    
    [Column(TypeName = "TEXT")]
    public string DataJson { get; set; } = "{}";
    
    // データの取得・設定用プロパティ
    [NotMapped]
    public Dictionary<string, object> Data
    {
        get
        {
            try
            {
                return string.IsNullOrEmpty(DataJson) 
                    ? new Dictionary<string, object>()
                    : JsonSerializer.Deserialize<Dictionary<string, object>>(DataJson) ?? new Dictionary<string, object>();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }
        set
        {
            try
            {
                DataJson = JsonSerializer.Serialize(value ?? new Dictionary<string, object>());
            }
            catch
            {
                DataJson = "{}";
            }
        }
    }
}