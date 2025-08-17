using SourceFlow.UI.Commands;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using NLog;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Win32;

namespace SourceFlow.UI.ViewModels;

public class SourceSyncViewModel : ViewModelBase
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IConfigurationService _configurationService;
    private readonly ISourceSyncService _sourceSyncService;
    
    private bool _isLoading = false;
    private bool _isSyncing = false;
    private string _statusMessage = "準備完了";
    private int _syncProgress = 0;
    private string _syncProgressText = "";
    
    // 選択中のソース設定
    private SourceConfiguration? _selectedSource;
    private bool _isEditMode = false;
    private string _editingName = "";
    private SourceType _editingSourceType = SourceType.Local;
    private string _editingSourcePath = "";
    private string _editingLocalPath = "";
    private string _editingSchedule = "0 0 * * *"; // デフォルト: 毎日午前0時
    private bool _editingEnabled = true;
    
    // リモート接続設定
    private string _editingProtocol = "SFTP";
    private string _editingHost = "";
    private int _editingPort = 22;
    private string _editingUsername = "";
    private string _editingPassword = "";
    
    public SourceSyncViewModel(
        IConfigurationService configurationService,
        ISourceSyncService sourceSyncService)
    {
        _configurationService = configurationService;
        _sourceSyncService = sourceSyncService;
        
        Sources = new ObservableCollection<SourceConfiguration>();
        SyncJobs = new ObservableCollection<SyncJob>();
        ExcludePatterns = new ObservableCollection<string>();
        ProtocolOptions = new ObservableCollection<string> { "SFTP", "FTP", "SMB", "HTTP", "HTTPS" };
        
        InitializeCommands();
        _ = Task.Run(LoadDataAsync);
    }

    #region Properties

    public ObservableCollection<SourceConfiguration> Sources { get; }
    public ObservableCollection<SyncJob> SyncJobs { get; }
    public ObservableCollection<string> ExcludePatterns { get; }
    public ObservableCollection<string> ProtocolOptions { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsSyncing
    {
        get => _isSyncing;
        set => SetProperty(ref _isSyncing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int SyncProgress
    {
        get => _syncProgress;
        set => SetProperty(ref _syncProgress, value);
    }

    public string SyncProgressText
    {
        get => _syncProgressText;
        set => SetProperty(ref _syncProgressText, value);
    }

    public SourceConfiguration? SelectedSource
    {
        get => _selectedSource;
        set => SetProperty(ref _selectedSource, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    public string EditingName
    {
        get => _editingName;
        set => SetProperty(ref _editingName, value);
    }

    public SourceType EditingSourceType
    {
        get => _editingSourceType;
        set 
        { 
            SetProperty(ref _editingSourceType, value);
            OnPropertyChanged(nameof(IsRemoteSource));
        }
    }

    public bool IsRemoteSource => EditingSourceType == SourceType.Remote;

    public string EditingSourcePath
    {
        get => _editingSourcePath;
        set => SetProperty(ref _editingSourcePath, value);
    }

    public string EditingLocalPath
    {
        get => _editingLocalPath;
        set => SetProperty(ref _editingLocalPath, value);
    }

    public string EditingSchedule
    {
        get => _editingSchedule;
        set => SetProperty(ref _editingSchedule, value);
    }

    public bool EditingEnabled
    {
        get => _editingEnabled;
        set => SetProperty(ref _editingEnabled, value);
    }

    public string EditingProtocol
    {
        get => _editingProtocol;
        set 
        { 
            SetProperty(ref _editingProtocol, value);
            // プロトコル変更時のデフォルトポート設定
            EditingPort = _editingProtocol switch
            {
                "SFTP" => 22,
                "FTP" => 21,
                "SMB" => 445,
                "HTTP" => 80,
                "HTTPS" => 443,
                _ => 22
            };
        }
    }

    public string EditingHost
    {
        get => _editingHost;
        set => SetProperty(ref _editingHost, value);
    }

    public int EditingPort
    {
        get => _editingPort;
        set => SetProperty(ref _editingPort, value);
    }

    public string EditingUsername
    {
        get => _editingUsername;
        set => SetProperty(ref _editingUsername, value);
    }

    public string EditingPassword
    {
        get => _editingPassword;
        set => SetProperty(ref _editingPassword, value);
    }

    #endregion

    #region Commands

    public ICommand LoadCommand { get; private set; } = null!;
    public ICommand AddSourceCommand { get; private set; } = null!;
    public ICommand EditSourceCommand { get; private set; } = null!;
    public ICommand DeleteSourceCommand { get; private set; } = null!;
    public ICommand SaveSourceCommand { get; private set; } = null!;
    public ICommand CancelEditCommand { get; private set; } = null!;
    public ICommand TestConnectionCommand { get; private set; } = null!;
    public ICommand StartSyncCommand { get; private set; } = null!;
    public ICommand StartAllSyncCommand { get; private set; } = null!;
    public ICommand StopSyncCommand { get; private set; } = null!;
    public ICommand AddExcludePatternCommand { get; private set; } = null!;
    public ICommand RemoveExcludePatternCommand { get; private set; } = null!;
    public ICommand BrowseFolderCommand { get; private set; } = null!;

    #endregion

    private void InitializeCommands()
    {
        LoadCommand = new RelayCommand(() => _ = Task.Run(LoadDataAsync));
        AddSourceCommand = new RelayCommand(AddNewSource);
        EditSourceCommand = new RelayCommand<SourceConfiguration>(EditSource);
        DeleteSourceCommand = new RelayCommand<SourceConfiguration>(source => _ = DeleteSourceAsync(source!));
        SaveSourceCommand = new RelayCommand(() => _ = SaveSourceAsync(), CanSaveSource);
        CancelEditCommand = new RelayCommand(CancelEdit);
        TestConnectionCommand = new RelayCommand(() => _ = TestConnectionAsync());
        StartSyncCommand = new RelayCommand<SourceConfiguration>(source => _ = StartSyncAsync(source!));
        StartAllSyncCommand = new RelayCommand(() => _ = StartAllSyncAsync(), () => Sources.Any(s => s.Enabled));
        StopSyncCommand = new RelayCommand(() => _logger.Info("同期停止（未実装）"));
        AddExcludePatternCommand = new RelayCommand(AddExcludePattern);
        RemoveExcludePatternCommand = new RelayCommand<string>(RemoveExcludePattern);
        BrowseFolderCommand = new RelayCommand<string>(BrowseFolder);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "ソース設定を読み込み中...";

            var sources = await _configurationService.GetSourceConfigurationsAsync();
            
            Sources.Clear();
            foreach (var source in sources)
            {
                Sources.Add(source);
            }

            StatusMessage = $"{sources.Count}件のソース設定を読み込みました";
            _logger.Info("ソース設定を読み込みました: {Count}件", sources.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ソース設定の読み込みに失敗しました");
            StatusMessage = "ソース設定の読み込みに失敗しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AddNewSource()
    {
        ClearEditingFields();
        IsEditMode = true;
        SelectedSource = null;
        _logger.Info("新規ソース追加モードに入りました");
    }

    private void EditSource(SourceConfiguration? source)
    {
        if (source == null) return;

        SelectedSource = source;
        
        // 編集フィールドに値を設定
        EditingName = source.Name;
        EditingSourceType = source.SourceType;
        EditingSourcePath = source.SourcePath;
        EditingLocalPath = source.LocalPath;
        EditingSchedule = source.Schedule;
        EditingEnabled = source.Enabled;

        if (source.SourceType == SourceType.Remote)
        {
            EditingProtocol = source.Protocol ?? "SFTP";
            EditingHost = source.Host ?? "";
            EditingPort = source.Port ?? 22;
            EditingUsername = source.Username ?? "";
            EditingPassword = ""; // セキュリティのため既存パスワードは非表示
        }

        // 除外パターンを設定
        ExcludePatterns.Clear();
        foreach (var pattern in source.ExcludePatterns)
        {
            ExcludePatterns.Add(pattern);
        }

        IsEditMode = true;
        _logger.Info("ソース編集モードに入りました: {SourceName}", source.Name);
    }

    private async Task DeleteSourceAsync(SourceConfiguration source)
    {
        try
        {
            Sources.Remove(source);
            await SaveAllSourcesAsync();
            StatusMessage = $"ソース設定「{source.Name}」を削除しました";
            _logger.Info("ソース設定を削除しました: {SourceName}", source.Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ソース設定の削除に失敗しました: {SourceName}", source.Name);
            StatusMessage = "ソース設定の削除に失敗しました";
        }
    }

    private async Task SaveSourceAsync()
    {
        try
        {
            var source = SelectedSource ?? new SourceConfiguration();
            
            // 基本情報の設定
            source.Name = EditingName;
            source.SourceType = EditingSourceType;
            source.SourcePath = EditingSourcePath;
            source.LocalPath = EditingLocalPath;
            source.Schedule = EditingSchedule;
            source.Enabled = EditingEnabled;

            // リモート設定
            if (EditingSourceType == SourceType.Remote)
            {
                source.Protocol = EditingProtocol;
                source.Host = EditingHost;
                source.Port = EditingPort;
                source.Username = EditingUsername;
                
                // パスワードが入力された場合のみ更新（セキュリティ向上済み）
                if (!string.IsNullOrWhiteSpace(EditingPassword))
                {
                    source.PasswordEncrypted = EditingPassword; // TODO: 将来的に暗号化強化
                }
            }

            // 除外パターン
            source.ExcludePatterns = ExcludePatterns.ToList();

            // 新規追加の場合
            if (SelectedSource == null)
            {
                Sources.Add(source);
            }

            await SaveAllSourcesAsync();
            
            IsEditMode = false;
            StatusMessage = $"ソース設定「{source.Name}」を保存しました";
            _logger.Info("ソース設定を保存しました: {SourceName}", source.Name);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ソース設定の保存に失敗しました");
            StatusMessage = "ソース設定の保存に失敗しました";
        }
    }

    private void CancelEdit()
    {
        IsEditMode = false;
        SelectedSource = null;
        ClearEditingFields();
        _logger.Info("編集をキャンセルしました");
    }

    private async Task TestConnectionAsync()
    {
        if (EditingSourceType == SourceType.Local)
        {
            var exists = System.IO.Directory.Exists(EditingSourcePath);
            StatusMessage = exists ? "パスへのアクセスに成功しました" : "指定されたパスが見つかりません";
            return;
        }

        try
        {
            StatusMessage = "接続テスト中...";
            
            var tempSource = new SourceConfiguration
            {
                SourceType = EditingSourceType,
                Protocol = EditingProtocol,
                Host = EditingHost,
                Port = EditingPort,
                Username = EditingUsername,
                PasswordEncrypted = EditingPassword,
                SourcePath = EditingSourcePath
            };

            var result = await _sourceSyncService.TestConnectionAsync(tempSource);
            StatusMessage = result ? "接続テストに成功しました" : "接続テストに失敗しました";
            
            _logger.Info("接続テスト実行: {Result}, ホスト: {Host}", result, EditingHost);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "接続テストでエラーが発生しました");
            StatusMessage = "接続テストでエラーが発生しました";
        }
    }

    private async Task StartSyncAsync(SourceConfiguration source)
    {
        try
        {
            IsSyncing = true;
            SyncProgress = 0;
            SyncProgressText = $"{source.Name} を同期中...";
            StatusMessage = "同期を開始しました";

            var result = await _sourceSyncService.SyncAsync(source);
            
            // 結果をジョブリストに追加
            SyncJobs.Insert(0, result);
            
            StatusMessage = result.Status == SyncStatus.Completed 
                ? $"同期が完了しました ({result.FilesSynced}ファイル)"
                : $"同期に失敗しました: {result.LogMessage}";

            _logger.Info("同期完了: {SourceName}, 状態: {Status}, ファイル数: {FileCount}", 
                source.Name, result.Status, result.FilesSynced);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "同期処理でエラーが発生しました: {SourceName}", source.Name);
            StatusMessage = "同期処理でエラーが発生しました";
        }
        finally
        {
            IsSyncing = false;
            SyncProgress = 0;
            SyncProgressText = "";
        }
    }

    private async Task StartAllSyncAsync()
    {
        var enabledSources = Sources.Where(s => s.Enabled).ToList();
        if (!enabledSources.Any())
        {
            StatusMessage = "有効なソースがありません";
            return;
        }

        try
        {
            IsSyncing = true;
            StatusMessage = $"{enabledSources.Count}件のソースを同期中...";

            for (int i = 0; i < enabledSources.Count; i++)
            {
                var source = enabledSources[i];
                SyncProgress = (int)((double)(i + 1) / enabledSources.Count * 100);
                SyncProgressText = $"{source.Name} を同期中... ({i + 1}/{enabledSources.Count})";

                await StartSyncAsync(source);
            }

            StatusMessage = "すべての同期が完了しました";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "一括同期でエラーが発生しました");
            StatusMessage = "一括同期でエラーが発生しました";
        }
        finally
        {
            IsSyncing = false;
            SyncProgress = 0;
            SyncProgressText = "";
        }
    }

    private void AddExcludePattern()
    {
        ExcludePatterns.Add("*.tmp");
    }

    private void RemoveExcludePattern(string? pattern)
    {
        if (pattern != null)
        {
            ExcludePatterns.Remove(pattern);
        }
    }

    private void BrowseFolder(string? target)
    {
        var dialog = new OpenFolderDialog();
        dialog.Title = target == "source" ? "ソースフォルダーを選択" : "ローカルフォルダーを選択";
        
        if (dialog.ShowDialog() == true)
        {
            if (target == "source")
                EditingSourcePath = dialog.FolderName;
            else
                EditingLocalPath = dialog.FolderName;
        }
    }

    private bool CanSaveSource()
    {
        return !string.IsNullOrWhiteSpace(EditingName) &&
               !string.IsNullOrWhiteSpace(EditingSourcePath) &&
               !string.IsNullOrWhiteSpace(EditingLocalPath) &&
               (EditingSourceType == SourceType.Local || 
                (!string.IsNullOrWhiteSpace(EditingHost) && !string.IsNullOrWhiteSpace(EditingUsername)));
    }

    private async Task SaveAllSourcesAsync()
    {
        await _configurationService.SaveSourceConfigurationsAsync(Sources.ToList());
    }

    private void ClearEditingFields()
    {
        EditingName = "";
        EditingSourceType = SourceType.Local;
        EditingSourcePath = "";
        EditingLocalPath = "";
        EditingSchedule = "0 0 * * *";
        EditingEnabled = true;
        EditingProtocol = "SFTP";
        EditingHost = "";
        EditingPort = 22;
        EditingUsername = "";
        EditingPassword = "";
        ExcludePatterns.Clear();
    }
}