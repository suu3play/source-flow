using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using SourceFlow.UI.Commands;
using SourceFlow.Core.Models;
using SourceFlow.Core.Interfaces;
using SourceFlow.Services.Release;
using NLog;

namespace SourceFlow.UI.ViewModels;

public class ReleaseManagerViewModel : ViewModelBase
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IComparisonService _comparisonService;
    private readonly IReleaseService _releaseService;
    
    private string _sourcePath = "";
    private string _targetPath = "";
    private string _releaseName = "";
    private bool _createBackup = true;
    private bool _isComparing = false;
    private bool _isReleasing = false;
    private int _releaseProgress = 0;
    private string _statusMessage = "準備完了";
    private ReleaseStatistics? _statistics;
    private CancellationTokenSource? _cancellationTokenSource;

    public ReleaseManagerViewModel(IComparisonService comparisonService, IReleaseService releaseService)
    {
        _comparisonService = comparisonService;
        _releaseService = releaseService;
        
        ComparisonResults = new ObservableCollection<FileComparisonResult>();
        ReleaseHistory = new ObservableCollection<ReleaseHistory>();
        
        InitializeCommands();
        LoadDataAsync();
    }

    // プロパティ
    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value);
    }

    public string ReleaseName
    {
        get => _releaseName;
        set => SetProperty(ref _releaseName, value);
    }

    public bool CreateBackup
    {
        get => _createBackup;
        set => SetProperty(ref _createBackup, value);
    }

    public bool IsComparing
    {
        get => _isComparing;
        set => SetProperty(ref _isComparing, value);
    }

    public bool IsReleasing
    {
        get => _isReleasing;
        set => SetProperty(ref _isReleasing, value);
    }

    public int ReleaseProgress
    {
        get => _releaseProgress;
        set => SetProperty(ref _releaseProgress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ReleaseStatistics? Statistics
    {
        get => _statistics;
        set => SetProperty(ref _statistics, value);
    }

    public ObservableCollection<FileComparisonResult> ComparisonResults { get; }
    public ObservableCollection<ReleaseHistory> ReleaseHistory { get; }

    // コマンド
    public ICommand BrowseSourceCommand { get; private set; } = null!;
    public ICommand BrowseTargetCommand { get; private set; } = null!;
    public ICommand CompareCommand { get; private set; } = null!;
    public ICommand SelectAllCommand { get; private set; } = null!;
    public ICommand UnselectAllCommand { get; private set; } = null!;
    public ICommand CreateReleaseCommand { get; private set; } = null!;
    public ICommand RefreshHistoryCommand { get; private set; } = null!;
    public ICommand DeleteReleaseCommand { get; private set; } = null!;
    public ICommand RestoreFromBackupCommand { get; private set; } = null!;
    public ICommand OpenWinMergeCommand { get; private set; } = null!;
    public ICommand CancelOperationCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        BrowseSourceCommand = new RelayCommand(BrowseSource);
        BrowseTargetCommand = new RelayCommand(BrowseTarget);
        CompareCommand = new RelayCommand(() => _ = CompareDirectoriesAsync(), CanCompare);
        SelectAllCommand = new RelayCommand(SelectAll);
        UnselectAllCommand = new RelayCommand(UnselectAll);
        CreateReleaseCommand = new RelayCommand(() => _ = CreateReleaseAsync(), CanCreateRelease);
        RefreshHistoryCommand = new RelayCommand(() => _ = LoadHistoryAsync());
        DeleteReleaseCommand = new RelayCommand<ReleaseHistory>(history => _ = DeleteReleaseAsync(history));
        RestoreFromBackupCommand = new RelayCommand<ReleaseHistory>(history => _ = RestoreFromBackupAsync(history));
        OpenWinMergeCommand = new RelayCommand<FileComparisonResult>(result => _ = OpenWinMergeAsync(result));
        CancelOperationCommand = new RelayCommand(CancelOperation, () => IsComparing || IsReleasing);
    }

    private async void LoadDataAsync()
    {
        try
        {
            StatusMessage = "データを読み込み中...";
            
            await LoadHistoryAsync();
            await LoadStatisticsAsync();
            
            // デフォルトのリリース名を生成
            ReleaseName = $"Release_{DateTime.Now:yyyyMMdd_HHmmss}";
            
            StatusMessage = "準備完了";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "データの読み込みに失敗しました");
            StatusMessage = "データの読み込みに失敗しました";
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var histories = await _releaseService.GetReleaseHistoryAsync();
            
            ReleaseHistory.Clear();
            foreach (var history in histories)
            {
                ReleaseHistory.Add(history);
            }
            
            _logger.Info("リリース履歴を読み込みました: {Count}件", histories.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "リリース履歴の読み込みに失敗しました");
        }
    }

    private async Task LoadStatisticsAsync()
    {
        try
        {
            Statistics = await _releaseService.GetReleaseStatisticsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "統計情報の読み込みに失敗しました");
        }
    }

    private void BrowseSource()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "ソースディレクトリを選択",
            InitialDirectory = SourcePath
        };

        if (dialog.ShowDialog() == true)
        {
            SourcePath = dialog.FolderName;
        }
    }

    private void BrowseTarget()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "ターゲットディレクトリを選択",
            InitialDirectory = TargetPath
        };

        if (dialog.ShowDialog() == true)
        {
            TargetPath = dialog.FolderName;
        }
    }

    private bool CanCompare()
    {
        return !IsComparing && !string.IsNullOrEmpty(SourcePath) && !string.IsNullOrEmpty(TargetPath) &&
               Directory.Exists(SourcePath) && Directory.Exists(TargetPath);
    }

    private async Task CompareDirectoriesAsync()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            IsComparing = true;
            StatusMessage = "ディレクトリを比較中...";
            
            ComparisonResults.Clear();
            
            var results = await _comparisonService.CompareDirectoriesAsync(SourcePath, TargetPath);
            
            if (!token.IsCancellationRequested)
            {
                foreach (var result in results)
                {
                    ComparisonResults.Add(result);
                    
                    if (token.IsCancellationRequested)
                        break;
                }
                
                StatusMessage = $"比較完了: {results.Count}件の差分を検出しました";
                _logger.Info("ディレクトリ比較完了: {Count}件の差分", results.Count);
            }
            else
            {
                StatusMessage = "比較がキャンセルされました";
                _logger.Info("ディレクトリ比較がキャンセルされました");
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "比較がキャンセルされました";
            _logger.Info("ディレクトリ比較がキャンセルされました");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ディレクトリ比較に失敗しました");
            StatusMessage = "ディレクトリ比較に失敗しました";
        }
        finally
        {
            IsComparing = false;
        }
    }

    private void SelectAll()
    {
        foreach (var result in ComparisonResults)
        {
            result.Selected = true;
        }
    }

    private void UnselectAll()
    {
        foreach (var result in ComparisonResults)
        {
            result.Selected = false;
        }
    }

    private bool CanCreateRelease()
    {
        return !IsReleasing && !string.IsNullOrEmpty(ReleaseName) && 
               ComparisonResults.Any(r => r.Selected);
    }

    private async Task CreateReleaseAsync()
    {
        try
        {
            IsReleasing = true;
            ReleaseProgress = 0;
            StatusMessage = "リリースを作成中...";
            
            var config = new ReleaseConfiguration
            {
                ReleaseName = ReleaseName,
                SourcePath = SourcePath,
                TargetPath = TargetPath,
                CreateBackup = CreateBackup,
                SelectedFiles = ComparisonResults.Where(r => r.Selected).ToList()
            };
            
            var result = await _releaseService.CreateReleaseAsync(config);
            
            if (result.Success)
            {
                StatusMessage = $"リリース完了: {result.FilesProcessed}件のファイルを処理しました";
                
                // 履歴を更新
                await LoadHistoryAsync();
                await LoadStatisticsAsync();
                
                // 次のリリース名を生成
                ReleaseName = $"Release_{DateTime.Now:yyyyMMdd_HHmmss}";
            }
            else
            {
                StatusMessage = $"リリース失敗: {result.ErrorMessage}";
            }
            
            ReleaseProgress = 100;
            _logger.Info("リリース処理完了: {Success}, エラー数: {Errors}", result.Success, result.ErrorsCount);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "リリース作成に失敗しました");
            StatusMessage = "リリース作成に失敗しました";
        }
        finally
        {
            IsReleasing = false;
        }
    }

    private async Task DeleteReleaseAsync(ReleaseHistory? history)
    {
        if (history == null) return;

        try
        {
            var success = await _releaseService.DeleteReleaseAsync(history.Id);
            if (success)
            {
                ReleaseHistory.Remove(history);
                await LoadStatisticsAsync();
                StatusMessage = "リリース履歴を削除しました";
            }
            else
            {
                StatusMessage = "リリース履歴の削除に失敗しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "リリース履歴の削除に失敗しました");
            StatusMessage = "リリース履歴の削除に失敗しました";
        }
    }

    private async Task RestoreFromBackupAsync(ReleaseHistory? history)
    {
        if (history == null || string.IsNullOrEmpty(history.BackupPath)) return;

        try
        {
            StatusMessage = "バックアップから復元中...";
            
            var success = await _releaseService.RestoreFromBackupAsync(history.BackupPath, history.TargetPath);
            if (success)
            {
                StatusMessage = "バックアップからの復元が完了しました";
            }
            else
            {
                StatusMessage = "バックアップからの復元に失敗しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "バックアップからの復元に失敗しました");
            StatusMessage = "バックアップからの復元に失敗しました";
        }
    }

    private async Task OpenWinMergeAsync(FileComparisonResult? result)
    {
        if (result == null) return;

        try
        {
            var sourceFile = Path.Combine(SourcePath, result.FilePath);
            var targetFile = Path.Combine(TargetPath, result.FilePath);
            
            if (File.Exists(sourceFile) && File.Exists(targetFile))
            {
                await _comparisonService.LaunchWinMergeAsync(sourceFile, targetFile);
                StatusMessage = "WinMergeを起動しました";
            }
            else
            {
                StatusMessage = "比較対象ファイルが見つかりません";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "WinMergeの起動に失敗しました");
            StatusMessage = "WinMergeの起動に失敗しました";
        }
    }

    private void CancelOperation()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = "操作をキャンセル中...";
            _logger.Info("ユーザーが操作をキャンセルしました");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "キャンセル処理中にエラーが発生しました");
        }
    }
}