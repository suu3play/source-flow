using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using SourceFlow.UI.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.IO;
using System.Text;
using NLog;

namespace SourceFlow.UI.ViewModels;

public class ScheduleExecutionHistoryViewModel : INotifyPropertyChanged
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IScheduleService _scheduleService;

    private ObservableCollection<ScheduleExecution> _executions = new();
    private ScheduleExecution? _selectedExecution;
    private bool _isLoading = false;
    private string _statusMessage = string.Empty;
    private int? _selectedJobId;
    private string _selectedJobName = "全て";
    private int _historyLimit = 100;

    // フィルター用プロパティ
    private DateTime? _filterStartDate;
    private DateTime? _filterEndDate;
    private ScheduleExecutionStatus? _filterStatus;

    public ScheduleExecutionHistoryViewModel(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;

        // Commands
        LoadExecutionsCommand = new RelayCommand(async () => await LoadExecutionsAsync());
        RefreshCommand = new RelayCommand(async () => await RefreshAsync());
        ApplyFilterCommand = new RelayCommand(async () => await ApplyFilterAsync());
        ClearFilterCommand = new RelayCommand(() => ClearFilter());
        ExportExecutionsCommand = new RelayCommand(async () => await ExportExecutionsAsync(), () => Executions.Any());

        // 初期データロード
        _ = Task.Run(LoadExecutionsAsync);
    }

    #region Properties

    public ObservableCollection<ScheduleExecution> Executions
    {
        get => _executions;
        set
        {
            _executions = value;
            OnPropertyChanged();
        }
    }

    public ScheduleExecution? SelectedExecution
    {
        get => _selectedExecution;
        set
        {
            _selectedExecution = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public int? SelectedJobId
    {
        get => _selectedJobId;
        set
        {
            _selectedJobId = value;
            OnPropertyChanged();
        }
    }

    public string SelectedJobName
    {
        get => _selectedJobName;
        set
        {
            _selectedJobName = value;
            OnPropertyChanged();
        }
    }

    public int HistoryLimit
    {
        get => _historyLimit;
        set
        {
            _historyLimit = value;
            OnPropertyChanged();
        }
    }

    // フィルター用プロパティ
    public DateTime? FilterStartDate
    {
        get => _filterStartDate;
        set
        {
            _filterStartDate = value;
            OnPropertyChanged();
        }
    }

    public DateTime? FilterEndDate
    {
        get => _filterEndDate;
        set
        {
            _filterEndDate = value;
            OnPropertyChanged();
        }
    }

    public ScheduleExecutionStatus? FilterStatus
    {
        get => _filterStatus;
        set
        {
            _filterStatus = value;
            OnPropertyChanged();
        }
    }

    // 統計情報
    public int TotalExecutions => Executions.Count;
    public int CompletedExecutions => Executions.Count(e => e.Status == ScheduleExecutionStatus.Completed);
    public int FailedExecutions => Executions.Count(e => e.Status == ScheduleExecutionStatus.Failed);
    public double SuccessRate => TotalExecutions > 0 ? (CompletedExecutions * 100.0 / TotalExecutions) : 0;
    public TimeSpan? AverageExecutionTime
    {
        get
        {
            var completedExecutions = Executions
                .Where(e => e.Status == ScheduleExecutionStatus.Completed && e.Duration.HasValue)
                .ToList();

            if (!completedExecutions.Any()) return null;

            var totalTicks = completedExecutions.Sum(e => e.Duration!.Value.Ticks);
            return new TimeSpan(totalTicks / completedExecutions.Count);
        }
    }

    // 利用可能なステータス一覧
    public IEnumerable<ScheduleExecutionStatus> AvailableStatuses => 
        Enum.GetValues<ScheduleExecutionStatus>();

    #endregion

    #region Commands

    public ICommand LoadExecutionsCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ApplyFilterCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand ExportExecutionsCommand { get; }

    #endregion

    #region Methods

    public async Task LoadExecutionsForJobAsync(int jobId, string jobName)
    {
        SelectedJobId = jobId;
        SelectedJobName = jobName;
        await LoadExecutionsAsync();
    }

    public async Task LoadAllExecutionsAsync()
    {
        SelectedJobId = null;
        SelectedJobName = "全て";
        await LoadExecutionsAsync();
    }

    private async Task LoadExecutionsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "実行履歴を読み込み中...";

            List<ScheduleExecution> executions;

            if (SelectedJobId.HasValue)
            {
                executions = await _scheduleService.GetExecutionHistoryAsync(SelectedJobId.Value, HistoryLimit);
                StatusMessage = $"ジョブ '{SelectedJobName}' の実行履歴を読み込みました";
            }
            else
            {
                // 全ジョブの実行履歴を取得（実際の実装では専用メソッドが必要）
                var allJobs = await _scheduleService.GetAllScheduledJobsAsync();
                executions = new List<ScheduleExecution>();
                
                foreach (var job in allJobs)
                {
                    var jobExecutions = await _scheduleService.GetExecutionHistoryAsync(job.Id, HistoryLimit / allJobs.Count);
                    executions.AddRange(jobExecutions);
                }
                
                executions = executions.OrderByDescending(e => e.StartTime).Take(HistoryLimit).ToList();
                StatusMessage = $"全ジョブの実行履歴を読み込みました";
            }

            Executions.Clear();
            foreach (var execution in executions)
            {
                Executions.Add(execution);
            }

            UpdateStatistics();

            _logger.Info("実行履歴を読み込みました: {Count}件 (ジョブ: {JobName})", 
                executions.Count, SelectedJobName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "実行履歴の読み込みに失敗しました");
            StatusMessage = $"読み込みエラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        await LoadExecutionsAsync();
    }

    private async Task ApplyFilterAsync()
    {
        await LoadExecutionsAsync();
        
        var filteredExecutions = Executions.ToList();

        // 日付フィルター
        if (FilterStartDate.HasValue)
        {
            filteredExecutions = filteredExecutions
                .Where(e => e.StartTime >= FilterStartDate.Value)
                .ToList();
        }

        if (FilterEndDate.HasValue)
        {
            filteredExecutions = filteredExecutions
                .Where(e => e.StartTime <= FilterEndDate.Value.AddDays(1))
                .ToList();
        }

        // ステータスフィルター
        if (FilterStatus.HasValue)
        {
            filteredExecutions = filteredExecutions
                .Where(e => e.Status == FilterStatus.Value)
                .ToList();
        }

        Executions.Clear();
        foreach (var execution in filteredExecutions)
        {
            Executions.Add(execution);
        }

        UpdateStatistics();
        StatusMessage = $"フィルター適用: {filteredExecutions.Count}件表示";
    }

    private void ClearFilter()
    {
        FilterStartDate = null;
        FilterEndDate = null;
        FilterStatus = null;
        
        _ = Task.Run(LoadExecutionsAsync);
    }

    private async Task ExportExecutionsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "実行履歴をエクスポート",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"schedule_execution_history_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                IsLoading = true;
                StatusMessage = "実行履歴をエクスポート中...";

                await ExportToCsvAsync(dialog.FileName);

                StatusMessage = $"実行履歴をエクスポートしました: {dialog.FileName}";
                _logger.Info("実行履歴をエクスポートしました: {FilePath}", dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "実行履歴のエクスポートに失敗しました");
            StatusMessage = $"エクスポートエラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportToCsvAsync(string filePath)
    {
        var csv = new StringBuilder();
        
        // ヘッダー
        csv.AppendLine("実行ID,ジョブID,開始時間,終了時間,ステータス,処理ファイル数,エラーメッセージ,リトライ回数,継続時間(秒),バックアップパス");
        
        // データ
        foreach (var execution in Executions)
        {
            csv.AppendLine($"{execution.Id}," +
                          $"{execution.ScheduledJobId}," +
                          $"\"{execution.StartTime:yyyy/MM/dd HH:mm:ss}\"," +
                          $"\"{execution.EndTime?.ToString("yyyy/MM/dd HH:mm:ss") ?? ""}\"," +
                          $"{execution.Status}," +
                          $"{execution.FilesProcessed}," +
                          $"\"{execution.ErrorMessage ?? ""}\"," +
                          $"{execution.RetryCount}," +
                          $"{execution.Duration?.TotalSeconds ?? 0}," +
                          $"\"{execution.BackupPath ?? ""}\"");
        }
        
        await File.WriteAllTextAsync(filePath, csv.ToString(), Encoding.UTF8);
    }

    private void UpdateStatistics()
    {
        OnPropertyChanged(nameof(TotalExecutions));
        OnPropertyChanged(nameof(CompletedExecutions));
        OnPropertyChanged(nameof(FailedExecutions));
        OnPropertyChanged(nameof(SuccessRate));
        OnPropertyChanged(nameof(AverageExecutionTime));
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}