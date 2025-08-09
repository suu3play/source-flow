using SourceFlow.Core.Enums;

namespace SourceFlow.Core.Models;

public class SourceConfiguration
{
    public string Name { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public string? Protocol { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? PasswordEncrypted { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public List<string> ExcludePatterns { get; set; } = new();
    public bool Enabled { get; set; } = true;
}