using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using SourceFlow.UI.Commands;
using SourceFlow.Core.Models;
using SourceFlow.Services.Settings;
using NLog;

namespace SourceFlow.UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IApplicationSettingsService _settingsService;
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    
    private ApplicationSettings _settings = new();
    private string _statusMessage = "設定を読み込み中...";
    private bool _isDirty = false;
    private bool _isLoading = true;

    public SettingsViewModel(IApplicationSettingsService settingsService)
    {
        _settingsService = settingsService;
        
        AvailableLogLevels = new ObservableCollection<string>
        {
            "Trace", "Debug", "Info", "Warn", "Error", "Fatal"
        };
        
        AvailableThemes = new ObservableCollection<string>
        {
            "Light", "Dark", "Auto"
        };
        
        InitializeCommands();
        LoadSettingsAsync();
    }

    // 設定プロパティ
    public ApplicationSettings Settings
    {
        get => _settings;
        set
        {
            if (SetProperty(ref _settings, value))
            {
                OnPropertyChanged(nameof(AutoSaveEnabled));
                OnPropertyChanged(nameof(AutoSaveIntervalMinutes));
                OnPropertyChanged(nameof(CreateBackupOnSync));
                OnPropertyChanged(nameof(DefaultWorkingDirectory));
                OnPropertyChanged(nameof(ConfirmDeleteOperations));
                OnPropertyChanged(nameof(ShowHiddenFiles));
                OnPropertyChanged(nameof(MaxConcurrentOperations));
                OnPropertyChanged(nameof(TextEditorPath));
                OnPropertyChanged(nameof(DiffToolPath));
                OnPropertyChanged(nameof(UseSystemDefaultTextEditor));
                OnPropertyChanged(nameof(UseSystemDefaultDiffTool));
                OnPropertyChanged(nameof(LogLevel));
                OnPropertyChanged(nameof(EnableFileLogging));
                OnPropertyChanged(nameof(LogFilePath));
                OnPropertyChanged(nameof(MaxLogFileSizeMB));
                OnPropertyChanged(nameof(MaxLogFiles));
                OnPropertyChanged(nameof(Theme));
                OnPropertyChanged(nameof(RestoreWindowState));
                OnPropertyChanged(nameof(ShowStatusBar));
                OnPropertyChanged(nameof(EnableAnimations));
            }
        }
    }

    // 一般設定
    public bool AutoSaveEnabled
    {
        get => Settings.General.AutoSaveEnabled;
        set { Settings.General.AutoSaveEnabled = value; MarkDirty(); OnPropertyChanged(); }
    }

    public int AutoSaveIntervalMinutes
    {
        get => Settings.General.AutoSaveIntervalMinutes;
        set { Settings.General.AutoSaveIntervalMinutes = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool CreateBackupOnSync
    {
        get => Settings.General.CreateBackupOnSync;
        set { Settings.General.CreateBackupOnSync = value; MarkDirty(); OnPropertyChanged(); }
    }

    public string DefaultWorkingDirectory
    {
        get => Settings.General.DefaultWorkingDirectory;
        set { Settings.General.DefaultWorkingDirectory = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool ConfirmDeleteOperations
    {
        get => Settings.General.ConfirmDeleteOperations;
        set { Settings.General.ConfirmDeleteOperations = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool ShowHiddenFiles
    {
        get => Settings.General.ShowHiddenFiles;
        set { Settings.General.ShowHiddenFiles = value; MarkDirty(); OnPropertyChanged(); }
    }

    public int MaxConcurrentOperations
    {
        get => Settings.General.MaxConcurrentOperations;
        set { Settings.General.MaxConcurrentOperations = value; MarkDirty(); OnPropertyChanged(); }
    }

    // 外部ツール設定
    public string TextEditorPath
    {
        get => Settings.ExternalTools.TextEditorPath;
        set { Settings.ExternalTools.TextEditorPath = value; MarkDirty(); OnPropertyChanged(); }
    }

    public string DiffToolPath
    {
        get => Settings.ExternalTools.DiffToolPath;
        set { Settings.ExternalTools.DiffToolPath = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool UseSystemDefaultTextEditor
    {
        get => Settings.ExternalTools.UseSystemDefaultTextEditor;
        set { Settings.ExternalTools.UseSystemDefaultTextEditor = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool UseSystemDefaultDiffTool
    {
        get => Settings.ExternalTools.UseSystemDefaultDiffTool;
        set { Settings.ExternalTools.UseSystemDefaultDiffTool = value; MarkDirty(); OnPropertyChanged(); }
    }

    // ログ設定
    public string LogLevel
    {
        get => Settings.Logging.LogLevel;
        set { Settings.Logging.LogLevel = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool EnableFileLogging
    {
        get => Settings.Logging.EnableFileLogging;
        set { Settings.Logging.EnableFileLogging = value; MarkDirty(); OnPropertyChanged(); }
    }

    public string LogFilePath
    {
        get => Settings.Logging.LogFilePath;
        set { Settings.Logging.LogFilePath = value; MarkDirty(); OnPropertyChanged(); }
    }

    public int MaxLogFileSizeMB
    {
        get => Settings.Logging.MaxLogFileSizeMB;
        set { Settings.Logging.MaxLogFileSizeMB = value; MarkDirty(); OnPropertyChanged(); }
    }

    public int MaxLogFiles
    {
        get => Settings.Logging.MaxLogFiles;
        set { Settings.Logging.MaxLogFiles = value; MarkDirty(); OnPropertyChanged(); }
    }

    // UI設定
    public string Theme
    {
        get => Settings.UI.Theme;
        set { Settings.UI.Theme = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool RestoreWindowState
    {
        get => Settings.UI.RestoreWindowState;
        set { Settings.UI.RestoreWindowState = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool ShowStatusBar
    {
        get => Settings.UI.ShowStatusBar;
        set { Settings.UI.ShowStatusBar = value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool EnableAnimations
    {
        get => Settings.UI.EnableAnimations;
        set { Settings.UI.EnableAnimations = value; MarkDirty(); OnPropertyChanged(); }
    }

    // その他のプロパティ
    public ObservableCollection<string> AvailableLogLevels { get; }
    public ObservableCollection<string> AvailableThemes { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    // コマンド
    public ICommand SaveCommand { get; private set; } = null!;
    public ICommand ResetCommand { get; private set; } = null!;
    public ICommand BrowseWorkingDirectoryCommand { get; private set; } = null!;
    public ICommand BrowseTextEditorCommand { get; private set; } = null!;
    public ICommand BrowseDiffToolCommand { get; private set; } = null!;
    public ICommand BrowseLogFileCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        SaveCommand = new RelayCommand(() => _ = SaveSettingsAsync(), () => IsDirty);
        ResetCommand = new RelayCommand(() => _ = ResetSettingsAsync());
        BrowseWorkingDirectoryCommand = new RelayCommand(BrowseWorkingDirectory);
        BrowseTextEditorCommand = new RelayCommand(BrowseTextEditor);
        BrowseDiffToolCommand = new RelayCommand(BrowseDiffTool);
        BrowseLogFileCommand = new RelayCommand(BrowseLogFile);
    }

    private async void LoadSettingsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "設定を読み込み中...";
            
            var settings = await _settingsService.LoadSettingsAsync();
            Settings = settings;
            IsDirty = false;
            
            StatusMessage = "設定を読み込みました";
            _logger.Info("設定を読み込みました");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "設定の読み込みに失敗しました");
            StatusMessage = "設定の読み込みに失敗しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            StatusMessage = "設定を保存中...";
            
            var success = await _settingsService.SaveSettingsAsync(Settings);
            if (success)
            {
                IsDirty = false;
                StatusMessage = "設定を保存しました";
                _logger.Info("設定を保存しました");
            }
            else
            {
                StatusMessage = "設定の保存に失敗しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "設定の保存に失敗しました");
            StatusMessage = "設定の保存に失敗しました";
        }
    }

    private async Task ResetSettingsAsync()
    {
        try
        {
            StatusMessage = "設定をリセット中...";
            
            var success = await _settingsService.ResetToDefaultAsync();
            if (success)
            {
                // 設定を再読み込み
                var settings = await _settingsService.LoadSettingsAsync();
                Settings = settings;
                IsDirty = false;
                StatusMessage = "設定をデフォルトにリセットしました";
                _logger.Info("設定をデフォルトにリセットしました");
            }
            else
            {
                StatusMessage = "設定のリセットに失敗しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "設定のリセットに失敗しました");
            StatusMessage = "設定のリセットに失敗しました";
        }
    }

    private void BrowseWorkingDirectory()
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "デフォルト作業ディレクトリを選択",
                InitialDirectory = Directory.Exists(DefaultWorkingDirectory) ? DefaultWorkingDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == true)
            {
                DefaultWorkingDirectory = dialog.FolderName;
                StatusMessage = "作業ディレクトリを設定しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "作業ディレクトリの選択に失敗しました");
            StatusMessage = "作業ディレクトリの選択に失敗しました";
        }
    }

    private void BrowseTextEditor()
    {
        try
        {
            var initialDir = !string.IsNullOrEmpty(TextEditorPath) && Directory.Exists(Path.GetDirectoryName(TextEditorPath))
                ? Path.GetDirectoryName(TextEditorPath)
                : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            var dialog = new OpenFileDialog
            {
                Title = "テキストエディターを選択",
                Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
                InitialDirectory = initialDir
            };

            if (dialog.ShowDialog() == true)
            {
                TextEditorPath = dialog.FileName;
                StatusMessage = "テキストエディターを設定しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "テキストエディターの選択に失敗しました");
            StatusMessage = "テキストエディターの選択に失敗しました";
        }
    }

    private void BrowseDiffTool()
    {
        var dialog = new OpenFileDialog
        {
            Title = "差分ツールを選択",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(DiffToolPath)
        };

        if (dialog.ShowDialog() == true)
        {
            DiffToolPath = dialog.FileName;
        }
    }

    private void BrowseLogFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "ログファイルパスを選択",
            Filter = "ログファイル (*.log)|*.log|テキストファイル (*.txt)|*.txt|すべてのファイル (*.*)|*.*",
            FileName = Path.GetFileName(LogFilePath),
            InitialDirectory = Path.GetDirectoryName(LogFilePath)
        };

        if (dialog.ShowDialog() == true)
        {
            LogFilePath = dialog.FileName;
        }
    }

    private void MarkDirty()
    {
        if (!IsLoading)
        {
            IsDirty = true;
        }
    }
}