using SourceFlow.UI.Commands;
using SourceFlow.Core.Interfaces;
using SourceFlow.Services.Settings;
using SourceFlow.Services.Release;
using NLog;
using System.Windows.Input;
using System.Collections.ObjectModel;

namespace SourceFlow.UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IConfigurationService _configurationService;
    private readonly ISourceSyncService _sourceSyncService;
    private readonly IComparisonService _comparisonService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IReleaseService _releaseService;
    
    private string _statusMessage = "準備完了";
    private int _selectedTabIndex = 0;
    
    public DashboardViewModel DashboardViewModel { get; }
    public SourceSyncViewModel SourceSyncViewModel { get; }
    public FileManagerViewModel FileManagerViewModel { get; }
    public ReleaseManagerViewModel ReleaseManagerViewModel { get; }
    public ScheduleManagerViewModel ScheduleManagerViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public FileDiffViewModel FileDiffViewModel { get; }

    public MainWindowViewModel(
        IConfigurationService configurationService,
        ISourceSyncService sourceSyncService,
        IComparisonService comparisonService,
        IApplicationSettingsService settingsService,
        IReleaseService releaseService,
        IScheduleService scheduleService,
        IScheduleJobManager scheduleJobManager,
        IDiffViewService diffViewService,
        ISyntaxHighlightingService syntaxHighlightingService)
    {
        _configurationService = configurationService;
        _sourceSyncService = sourceSyncService;
        _comparisonService = comparisonService;
        _settingsService = settingsService;
        _releaseService = releaseService;
        
        // 子ViewModelの初期化
        DashboardViewModel = new DashboardViewModel(_configurationService, _sourceSyncService);
        SourceSyncViewModel = new SourceSyncViewModel(_configurationService, _sourceSyncService);
        FileManagerViewModel = new FileManagerViewModel(_configurationService, _comparisonService);
        ReleaseManagerViewModel = new ReleaseManagerViewModel(_comparisonService, _releaseService);
        ScheduleManagerViewModel = new ScheduleManagerViewModel(scheduleService, scheduleJobManager);
        SettingsViewModel = new SettingsViewModel(_settingsService);
        FileDiffViewModel = new FileDiffViewModel(diffViewService, syntaxHighlightingService);
        
        InitializeCommands();
        LoadDataAsync();
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public ICommand RefreshCommand { get; private set; } = null!;
    public ICommand ExitCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        RefreshCommand = new RelayCommand(() => _ = RefreshAsync());
        ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());
    }

    private async void LoadDataAsync()
    {
        try
        {
            StatusMessage = "データを読み込み中...";
            
            // 設定の読み込みテスト
            var sources = await _configurationService.GetSourceConfigurationsAsync();
            _logger.Info("設定を読み込みました: {Count}件のソース設定", sources.Count);
            
            StatusMessage = $"準備完了 - {sources.Count}件のソース設定を読み込みました";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "データの読み込みに失敗しました");
            StatusMessage = "データの読み込みに失敗しました";
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            StatusMessage = "データを更新中...";
            
            // 設定の読み込みテスト
            var sources = await _configurationService.GetSourceConfigurationsAsync();
            _logger.Info("設定を更新しました: {Count}件のソース設定", sources.Count);
            
            StatusMessage = $"準備完了 - {sources.Count}件のソース設定を読み込みました";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "データの更新に失敗しました");
            StatusMessage = "データの更新に失敗しました";
        }
    }
}