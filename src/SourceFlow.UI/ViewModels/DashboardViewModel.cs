using SourceFlow.UI.Commands;
using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using NLog;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace SourceFlow.UI.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IConfigurationService _configurationService;
    private readonly ISourceSyncService _sourceSyncService;
    
    private string _systemStatus = "システム準備完了";
    private int _totalSources = 0;
    private int _enabledSources = 0;
    private DateTime _lastSyncTime = DateTime.MinValue;
    private string _lastSyncStatus = "未実行";
    private bool _isLoading = false;

    public DashboardViewModel(
        IConfigurationService configurationService, 
        ISourceSyncService sourceSyncService)
    {
        _configurationService = configurationService;
        _sourceSyncService = sourceSyncService;
        
        RecentJobs = new ObservableCollection<SyncJobSummary>();
        QuickActions = new ObservableCollection<QuickAction>();
        
        InitializeCommands();
        InitializeQuickActions();
        LoadDashboardDataAsync();
    }

    public string SystemStatus
    {
        get => _systemStatus;
        set => SetProperty(ref _systemStatus, value);
    }

    public int TotalSources
    {
        get => _totalSources;
        set => SetProperty(ref _totalSources, value);
    }

    public int EnabledSources
    {
        get => _enabledSources;
        set => SetProperty(ref _enabledSources, value);
    }

    public DateTime LastSyncTime
    {
        get => _lastSyncTime;
        set => SetProperty(ref _lastSyncTime, value);
    }

    public string LastSyncStatus
    {
        get => _lastSyncStatus;
        set => SetProperty(ref _lastSyncStatus, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<SyncJobSummary> RecentJobs { get; }
    public ObservableCollection<QuickAction> QuickActions { get; }

    public ICommand RefreshCommand { get; private set; } = null!;
    public ICommand ExecuteQuickActionCommand { get; private set; } = null!;
    public ICommand ViewAllJobsCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        RefreshCommand = new RelayCommand(() => _ = RefreshDashboardAsync());
        ExecuteQuickActionCommand = new RelayCommand<QuickAction>(ExecuteQuickAction);
        ViewAllJobsCommand = new RelayCommand(() => _logger.Info("すべてのジョブを表示（未実装）"));
    }

    private void InitializeQuickActions()
    {
        QuickActions.Add(new QuickAction 
        { 
            Title = "新規同期実行", 
            Description = "すべての有効なソースから同期を実行", 
            IconKind = "Sync",
            ActionType = "StartSync"
        });
        
        QuickActions.Add(new QuickAction 
        { 
            Title = "ソース設定", 
            Description = "同期ソースの設定を管理", 
            IconKind = "Settings",
            ActionType = "OpenSettings"
        });
        
        QuickActions.Add(new QuickAction 
        { 
            Title = "ファイル比較", 
            Description = "ディレクトリ間のファイル比較を実行", 
            IconKind = "Compare",
            ActionType = "OpenComparison"
        });
        
        QuickActions.Add(new QuickAction 
        { 
            Title = "バックアップ表示", 
            Description = "作成されたバックアップを確認", 
            IconKind = "Backup",
            ActionType = "OpenBackups"
        });
    }

    private async void LoadDashboardDataAsync()
    {
        try
        {
            IsLoading = true;
            SystemStatus = "データを読み込み中...";

            // ソース設定の読み込み
            var sources = await _configurationService.GetSourceConfigurationsAsync();
            TotalSources = sources.Count;
            EnabledSources = sources.Count(s => s.Enabled);

            // 最新の同期情報を模擬データで設定（後でデータベースから取得）
            if (sources.Any())
            {
                LastSyncTime = DateTime.Now.AddHours(-2); // 2時間前に実行したと仮定
                LastSyncStatus = "成功";
                SystemStatus = $"正常稼働中 - {EnabledSources}/{TotalSources} 個のソースが有効";
            }
            else
            {
                SystemStatus = "ソース設定がありません";
                LastSyncStatus = "未設定";
            }

            // 最近のジョブ履歴を模擬データで追加（後でデータベースから取得）
            LoadRecentJobs();

            _logger.Info("ダッシュボードデータを読み込みました: {TotalSources}個のソース設定", TotalSources);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ダッシュボードデータの読み込みに失敗しました");
            SystemStatus = "データの読み込みに失敗しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadRecentJobs()
    {
        RecentJobs.Clear();
        
        // 模擬データ（後でデータベースから取得）
        RecentJobs.Add(new SyncJobSummary
        {
            JobName = "開発サーバ同期",
            StartTime = DateTime.Now.AddHours(-2),
            Duration = TimeSpan.FromMinutes(5),
            Status = "成功",
            FilesCount = 125
        });
        
        RecentJobs.Add(new SyncJobSummary
        {
            JobName = "ローカル環境同期",
            StartTime = DateTime.Now.AddHours(-4),
            Duration = TimeSpan.FromMinutes(2),
            Status = "成功",
            FilesCount = 45
        });
        
        RecentJobs.Add(new SyncJobSummary
        {
            JobName = "共有フォルダ同期",
            StartTime = DateTime.Now.AddDays(-1),
            Duration = TimeSpan.FromMinutes(1),
            Status = "警告",
            FilesCount = 12
        });
    }

    private async Task RefreshDashboardAsync()
    {
        _logger.Info("ダッシュボードを更新しています...");
        LoadDashboardDataAsync();
        await Task.Delay(100); // UI更新のため
    }

    private void ExecuteQuickAction(QuickAction? action)
    {
        if (action == null) return;

        _logger.Info("クイックアクション実行: {ActionType}", action.ActionType);

        switch (action.ActionType)
        {
            case "StartSync":
                _ = StartAllSyncAsync();
                break;
            case "OpenSettings":
                // 設定タブに移動（親ViewModelに通知する仕組みが必要）
                _logger.Info("設定画面を開きます");
                break;
            case "OpenComparison":
                // 比較画面に移動
                _logger.Info("比較画面を開きます");
                break;
            case "OpenBackups":
                // バックアップ画面を開く
                _logger.Info("バックアップ画面を開きます");
                break;
        }
    }

    private async Task StartAllSyncAsync()
    {
        try
        {
            SystemStatus = "同期を実行中...";
            _logger.Info("全ソースの同期を開始します");

            var sources = await _configurationService.GetSourceConfigurationsAsync();
            var enabledSources = sources.Where(s => s.Enabled).ToList();

            if (!enabledSources.Any())
            {
                SystemStatus = "有効なソースがありません";
                return;
            }

            // 順次同期実行（並列実行も可能）
            foreach (var source in enabledSources)
            {
                try
                {
                    var result = await _sourceSyncService.SyncAsync(source);
                    _logger.Info("同期完了: {SourceName} - {Status}", source.Name, result.Status);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "同期に失敗しました: {SourceName}", source.Name);
                }
            }

            // 完了後にダッシュボードを更新
            LoadDashboardDataAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "同期処理中にエラーが発生しました");
            SystemStatus = "同期処理中にエラーが発生しました";
        }
    }
}

// データ用のクラス
public class SyncJobSummary
{
    public string JobName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public int FilesCount { get; set; }
    
    public string StatusColor => Status switch
    {
        "成功" => "Green",
        "警告" => "Orange", 
        "エラー" => "Red",
        _ => "Gray"
    };
}

public class QuickAction
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKind { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
}