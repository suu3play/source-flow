using System.Text.Json.Serialization;

namespace SourceFlow.Core.Models;

public class ApplicationSettings
{
    public string ApplicationVersion { get; set; } = "1.0.0";
    public string LastBackupLocation { get; set; } = "";
    
    // 一般設定
    public GeneralSettings General { get; set; } = new();
    
    // 外部ツール設定
    public ExternalToolSettings ExternalTools { get; set; } = new();
    
    // ログ設定
    public LoggingSettings Logging { get; set; } = new();
    
    // UI設定
    public UiSettings UI { get; set; } = new();
}

public class GeneralSettings
{
    public bool AutoSaveEnabled { get; set; } = true;
    public int AutoSaveIntervalMinutes { get; set; } = 5;
    public bool CreateBackupOnSync { get; set; } = true;
    public string DefaultWorkingDirectory { get; set; } = "";
    public bool ConfirmDeleteOperations { get; set; } = true;
    public bool ShowHiddenFiles { get; set; } = false;
    public int MaxConcurrentOperations { get; set; } = 3;
}

public class ExternalToolSettings
{
    public string TextEditorPath { get; set; } = "";
    public string DiffToolPath { get; set; } = "";
    public string ArchiveToolPath { get; set; } = "";
    public string GitExecutablePath { get; set; } = "";
    public bool UseSystemDefaultTextEditor { get; set; } = true;
    public bool UseSystemDefaultDiffTool { get; set; } = true;
}

public class LoggingSettings
{
    public string LogLevel { get; set; } = "Info"; // Trace, Debug, Info, Warn, Error, Fatal
    public bool EnableFileLogging { get; set; } = true;
    public string LogFilePath { get; set; } = "logs/application.log";
    public int MaxLogFileSizeMB { get; set; } = 10;
    public int MaxLogFiles { get; set; } = 5;
    public bool EnableConsoleLogging { get; set; } = true;
    public bool LogSyncOperations { get; set; } = true;
    public bool LogFileOperations { get; set; } = true;
}

public class UiSettings
{
    public string Theme { get; set; } = "Light"; // Light, Dark, Auto
    public string Language { get; set; } = "ja-JP";
    public bool RestoreWindowState { get; set; } = true;
    public WindowState LastWindowState { get; set; } = new();
    public bool ShowStatusBar { get; set; } = true;
    public bool ShowToolbar { get; set; } = true;
    public int DefaultTabIndex { get; set; } = 0;
    public bool EnableAnimations { get; set; } = true;
}

public class WindowState
{
    public double Width { get; set; } = 1000;
    public double Height { get; set; } = 600;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public bool IsMaximized { get; set; } = false;
}