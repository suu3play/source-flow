using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using SourceFlow.UI.Commands;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Text.Json;
using NLog;

namespace SourceFlow.UI.ViewModels;

public class ScheduleManagerViewModel : INotifyPropertyChanged
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IScheduleService _scheduleService;
    private readonly IScheduleJobManager _jobManager;

    private ObservableCollection<ScheduledJob> _scheduledJobs = new();
    private ScheduledJob? _selectedJob;
    private bool _isLoading = false;
    private string _statusMessage = string.Empty;
    private ScheduleStatistics? _statistics;

    // 新規ジョブ作成用プロパティ
    private string _newJobName = string.Empty;
    private string _newJobDescription = string.Empty;
    private string _newCronExpression = string.Empty;
    private string _newSourcePath = string.Empty;
    private string _newTargetPath = string.Empty;
    private bool _newCreateBackup = true;
    private int _newMaxRetryCount = 3;
    private bool _isCreateJobDialogOpen = false;

    public ScheduleManagerViewModel(IScheduleService scheduleService, IScheduleJobManager jobManager)
    {
        _scheduleService = scheduleService;
        _jobManager = jobManager;

        // Commands
        LoadJobsCommand = new RelayCommand(async () => await LoadJobsAsync());
        CreateJobCommand = new RelayCommand(async () => await CreateJobAsync(), CanCreateJob);
        UpdateJobCommand = new RelayCommand(async () => await UpdateJobAsync(), () => SelectedJob != null);
        DeleteJobCommand = new RelayCommand(async () => await DeleteJobAsync(), () => SelectedJob != null);
        StartJobCommand = new RelayCommand(async () => await StartJobAsync(), () => SelectedJob != null && !SelectedJob.IsEnabled);
        StopJobCommand = new RelayCommand(async () => await StopJobAsync(), () => SelectedJob != null && SelectedJob.IsEnabled);
        PauseJobCommand = new RelayCommand(async () => await PauseJobAsync(), () => SelectedJob != null && SelectedJob.Status == ScheduleStatus.Active);
        ResumeJobCommand = new RelayCommand(async () => await ResumeJobAsync(), () => SelectedJob != null && SelectedJob.Status == ScheduleStatus.Paused);
        TriggerJobCommand = new RelayCommand(async () => await TriggerJobAsync(), () => SelectedJob != null);
        OpenCreateJobDialogCommand = new RelayCommand(() => OpenCreateJobDialog());
        CloseCreateJobDialogCommand = new RelayCommand(() => CloseCreateJobDialog());
        ValidateCronCommand = new RelayCommand(() => ValidateCronExpression());
        BrowseSourcePathCommand = new RelayCommand(() => BrowseSourcePath());
        BrowseTargetPathCommand = new RelayCommand(() => BrowseTargetPath());

        // 初期データロード
        _ = Task.Run(LoadJobsAsync);
    }

    #region Properties

    public ObservableCollection<ScheduledJob> ScheduledJobs
    {
        get => _scheduledJobs;
        set
        {
            _scheduledJobs = value;
            OnPropertyChanged();
        }
    }

    public ScheduledJob? SelectedJob
    {
        get => _selectedJob;
        set
        {
            _selectedJob = value;
            OnPropertyChanged();
            UpdateCommandStates();
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

    public ScheduleStatistics? Statistics
    {
        get => _statistics;
        set
        {
            _statistics = value;
            OnPropertyChanged();
        }
    }

    // 新規ジョブ作成用プロパティ
    public string NewJobName
    {
        get => _newJobName;
        set
        {
            _newJobName = value;
            OnPropertyChanged();
            UpdateCreateJobCommand();
        }
    }

    public string NewJobDescription
    {
        get => _newJobDescription;
        set
        {
            _newJobDescription = value;
            OnPropertyChanged();
        }
    }

    public string NewCronExpression
    {
        get => _newCronExpression;
        set
        {
            _newCronExpression = value;
            OnPropertyChanged();
            UpdateCreateJobCommand();
        }
    }

    public string NewSourcePath
    {
        get => _newSourcePath;
        set
        {
            _newSourcePath = value;
            OnPropertyChanged();
            UpdateCreateJobCommand();
        }
    }

    public string NewTargetPath
    {
        get => _newTargetPath;
        set
        {
            _newTargetPath = value;
            OnPropertyChanged();
            UpdateCreateJobCommand();
        }
    }

    public bool NewCreateBackup
    {
        get => _newCreateBackup;
        set
        {
            _newCreateBackup = value;
            OnPropertyChanged();
        }
    }

    public int NewMaxRetryCount
    {
        get => _newMaxRetryCount;
        set
        {
            _newMaxRetryCount = value;
            OnPropertyChanged();
        }
    }

    public bool IsCreateJobDialogOpen
    {
        get => _isCreateJobDialogOpen;
        set
        {
            _isCreateJobDialogOpen = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region Commands

    public ICommand LoadJobsCommand { get; }
    public ICommand CreateJobCommand { get; }
    public ICommand UpdateJobCommand { get; }
    public ICommand DeleteJobCommand { get; }
    public ICommand StartJobCommand { get; }
    public ICommand StopJobCommand { get; }
    public ICommand PauseJobCommand { get; }
    public ICommand ResumeJobCommand { get; }
    public ICommand TriggerJobCommand { get; }
    public ICommand OpenCreateJobDialogCommand { get; }
    public ICommand CloseCreateJobDialogCommand { get; }
    public ICommand ValidateCronCommand { get; }
    public ICommand BrowseSourcePathCommand { get; }
    public ICommand BrowseTargetPathCommand { get; }

    #endregion

    #region Methods

    private async Task LoadJobsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "スケジュールジョブを読み込み中...";

            var jobs = await _scheduleService.GetAllScheduledJobsAsync();
            ScheduledJobs.Clear();
            foreach (var job in jobs)
            {
                ScheduledJobs.Add(job);
            }

            Statistics = await _scheduleService.GetScheduleStatisticsAsync();
            StatusMessage = $"{jobs.Count}件のスケジュールジョブを読み込みました";

            _logger.Info("スケジュールジョブ一覧を読み込みました: {Count}件", jobs.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "スケジュールジョブの読み込みに失敗しました");
            StatusMessage = $"読み込みエラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CreateJobAsync()
    {
        try
        {
            if (!CanCreateJob()) return;

            IsLoading = true;
            StatusMessage = "新しいスケジュールジョブを作成中...";

            var releaseConfig = new ReleaseConfiguration
            {
                ReleaseName = NewJobName,
                SourcePath = NewSourcePath,
                TargetPath = NewTargetPath,
                CreateBackup = NewCreateBackup
            };

            var job = new ScheduledJob
            {
                JobName = NewJobName,
                Description = NewJobDescription,
                CronExpression = NewCronExpression,
                ReleaseConfigurationJson = JsonSerializer.Serialize(releaseConfig),
                MaxRetryCount = NewMaxRetryCount,
                IsEnabled = true,
                Status = ScheduleStatus.Active
            };

            var createdJob = await _scheduleService.CreateScheduledJobAsync(job);
            ScheduledJobs.Add(createdJob);

            ClearNewJobForm();
            CloseCreateJobDialog();
            StatusMessage = $"スケジュールジョブ '{createdJob.JobName}' を作成しました";

            _logger.Info("新しいスケジュールジョブを作成しました: {JobName}", createdJob.JobName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "スケジュールジョブの作成に失敗しました");
            StatusMessage = $"作成エラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateJobAsync()
    {
        if (SelectedJob == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "スケジュールジョブを更新中...";

            var result = await _scheduleService.UpdateScheduledJobAsync(SelectedJob);
            if (result)
            {
                StatusMessage = $"スケジュールジョブ '{SelectedJob.JobName}' を更新しました";
                _logger.Info("スケジュールジョブを更新しました: {JobName}", SelectedJob.JobName);
            }
            else
            {
                StatusMessage = "スケジュールジョブの更新に失敗しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "スケジュールジョブの更新に失敗しました");
            StatusMessage = $"更新エラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteJobAsync()
    {
        if (SelectedJob == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "スケジュールジョブを削除中...";

            var result = await _scheduleService.DeleteScheduledJobAsync(SelectedJob.Id);
            if (result)
            {
                ScheduledJobs.Remove(SelectedJob);
                StatusMessage = $"スケジュールジョブ '{SelectedJob.JobName}' を削除しました";
                _logger.Info("スケジュールジョブを削除しました: {JobName}", SelectedJob.JobName);
                SelectedJob = null;
            }
            else
            {
                StatusMessage = "スケジュールジョブの削除に失敗しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "スケジュールジョブの削除に失敗しました");
            StatusMessage = $"削除エラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task StartJobAsync()
    {
        if (SelectedJob == null) return;
        await ChangeJobStatusAsync(() => _scheduleService.StartScheduledJobAsync(SelectedJob.Id), "開始");
    }

    private async Task StopJobAsync()
    {
        if (SelectedJob == null) return;
        await ChangeJobStatusAsync(() => _scheduleService.StopScheduledJobAsync(SelectedJob.Id), "停止");
    }

    private async Task PauseJobAsync()
    {
        if (SelectedJob == null) return;
        await ChangeJobStatusAsync(() => _scheduleService.PauseScheduledJobAsync(SelectedJob.Id), "一時停止");
    }

    private async Task ResumeJobAsync()
    {
        if (SelectedJob == null) return;
        await ChangeJobStatusAsync(() => _scheduleService.ResumeScheduledJobAsync(SelectedJob.Id), "再開");
    }

    private async Task TriggerJobAsync()
    {
        if (SelectedJob == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "ジョブを手動実行中...";

            var result = await _scheduleService.TriggerJobAsync(SelectedJob.Id);
            if (result)
            {
                StatusMessage = $"ジョブ '{SelectedJob.JobName}' を手動実行しました";
                _logger.Info("ジョブを手動実行しました: {JobName}", SelectedJob.JobName);
            }
            else
            {
                StatusMessage = "ジョブの手動実行に失敗しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ジョブの手動実行に失敗しました");
            StatusMessage = $"実行エラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ChangeJobStatusAsync(Func<Task<bool>> action, string actionName)
    {
        if (SelectedJob == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = $"ジョブを{actionName}中...";

            var result = await action();
            if (result)
            {
                await LoadJobsAsync(); // 最新状態を再読み込み
                StatusMessage = $"ジョブ '{SelectedJob?.JobName}' を{actionName}しました";
            }
            else
            {
                StatusMessage = $"ジョブの{actionName}に失敗しました";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ジョブの{ActionName}に失敗しました", actionName);
            StatusMessage = $"{actionName}エラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private bool CanCreateJob()
    {
        return !string.IsNullOrWhiteSpace(NewJobName) &&
               !string.IsNullOrWhiteSpace(NewCronExpression) &&
               !string.IsNullOrWhiteSpace(NewSourcePath) &&
               !string.IsNullOrWhiteSpace(NewTargetPath) &&
               _scheduleService.ValidateCronExpression(NewCronExpression);
    }

    private void UpdateCommandStates()
    {
        // ViewModelの他のコマンドの有効性を更新
        OnPropertyChanged(nameof(UpdateJobCommand));
        OnPropertyChanged(nameof(DeleteJobCommand));
        OnPropertyChanged(nameof(StartJobCommand));
        OnPropertyChanged(nameof(StopJobCommand));
        OnPropertyChanged(nameof(PauseJobCommand));
        OnPropertyChanged(nameof(ResumeJobCommand));
        OnPropertyChanged(nameof(TriggerJobCommand));
    }

    private void UpdateCreateJobCommand()
    {
        OnPropertyChanged(nameof(CreateJobCommand));
    }

    private void OpenCreateJobDialog()
    {
        ClearNewJobForm();
        IsCreateJobDialogOpen = true;
    }

    private void CloseCreateJobDialog()
    {
        IsCreateJobDialogOpen = false;
    }

    private void ClearNewJobForm()
    {
        NewJobName = string.Empty;
        NewJobDescription = string.Empty;
        NewCronExpression = string.Empty;
        NewSourcePath = string.Empty;
        NewTargetPath = string.Empty;
        NewCreateBackup = true;
        NewMaxRetryCount = 3;
    }

    private void ValidateCronExpression()
    {
        if (string.IsNullOrWhiteSpace(NewCronExpression))
        {
            StatusMessage = "Cron式を入力してください";
            return;
        }

        var isValid = _scheduleService.ValidateCronExpression(NewCronExpression);
        if (isValid)
        {
            var nextExecution = _scheduleService.GetNextExecutionTime(NewCronExpression);
            StatusMessage = nextExecution.HasValue 
                ? $"Cron式は有効です。次回実行: {nextExecution:yyyy/MM/dd HH:mm:ss}"
                : "Cron式は有効です";
        }
        else
        {
            StatusMessage = "無効なCron式です";
        }
    }

    private void BrowseSourcePath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "ソースパスを選択",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "フォルダ選択"
        };

        if (dialog.ShowDialog() == true)
        {
            NewSourcePath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        }
    }

    private void BrowseTargetPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "ターゲットパスを選択",
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "フォルダ選択"
        };

        if (dialog.ShowDialog() == true)
        {
            NewTargetPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        }
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