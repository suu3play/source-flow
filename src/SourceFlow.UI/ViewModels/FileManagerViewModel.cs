using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using SourceFlow.UI.Commands;
using SourceFlow.Core.Interfaces;
using NLog;

namespace SourceFlow.UI.ViewModels;

public class FileManagerViewModel : ViewModelBase
{
    private const int LARGE_DIRECTORY_THRESHOLD = 1000;
    private const int UI_BATCH_SIZE = 100;
    private const int UI_REFRESH_DELAY = 1;

    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IConfigurationService _configurationService;
    private readonly IComparisonService _comparisonService;
    
    private string _currentPath = "";
    private DirectoryItem? _selectedDirectory;
    private FileItem? _selectedFile;
    private string _statusMessage = "準備完了";
    private bool _isLoading = false;

    public FileManagerViewModel(IConfigurationService configurationService, IComparisonService comparisonService)
    {
        _configurationService = configurationService;
        _comparisonService = comparisonService;
        
        DirectoryTree = new ObservableCollection<DirectoryItem>();
        FileList = new ObservableCollection<FileItem>();
        
        InitializeCommands();
        InitializeAsync();
    }

    public ObservableCollection<DirectoryItem> DirectoryTree { get; }
    public ObservableCollection<FileItem> FileList { get; }

    public string CurrentPath
    {
        get => _currentPath;
        set => SetProperty(ref _currentPath, value);
    }

    public DirectoryItem? SelectedDirectory
    {
        get => _selectedDirectory;
        set
        {
            if (SetProperty(ref _selectedDirectory, value))
            {
                _ = LoadFilesAsync(value?.FullPath ?? "");
            }
        }
    }

    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ICommand RefreshCommand { get; private set; } = null!;
    public ICommand NavigateUpCommand { get; private set; } = null!;
    public ICommand OpenFolderCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        RefreshCommand = new RelayCommand(() => _ = RefreshAsync());
        NavigateUpCommand = new RelayCommand(NavigateUp, CanNavigateUp);
        OpenFolderCommand = new RelayCommand(() => _ = OpenFolderAsync());
    }

    private async void InitializeAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "初期化中...";
            
            await LoadDirectoryTreeAsync();
            
            // デフォルトでルートドライブを設定
            var drives = Directory.GetLogicalDrives();
            if (drives.Length > 0)
            {
                CurrentPath = drives[0];
                await LoadFilesAsync(CurrentPath);
            }
            
            StatusMessage = "準備完了";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイルマネージャーの初期化に失敗しました");
            StatusMessage = "初期化に失敗しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadDirectoryTreeAsync()
    {
        try
        {
            DirectoryTree.Clear();
            
            // 非同期でドライブ情報を取得
            var drives = await Task.Run(() => Directory.GetLogicalDrives());
            
            foreach (var drive in drives)
            {
                try
                {
                    // ドライブの準備状態をチェック
                    var driveInfo = new DriveInfo(drive);
                    if (!driveInfo.IsReady)
                    {
                        _logger.Debug("ドライブが準備できていないためスキップしました: {Drive}", drive);
                        continue;
                    }

                    var driveItem = new DirectoryItem
                    {
                        Name = $"{drive} ({driveInfo.DriveType})",
                        FullPath = drive,
                        IsExpanded = false,
                        Parent = null
                    };
                    
                    // サブディレクトリがあるかチェック（遅延読み込み用）
                    try
                    {
                        var hasDirectories = await Task.Run(() => Directory.EnumerateDirectories(drive).Any());
                        if (hasDirectories)
                        {
                            driveItem.Children.Add(new DirectoryItem { Name = "読み込み中..." });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug(ex, "ドライブのサブディレクトリチェックに失敗: {Drive}", drive);
                        // アクセス権限がない場合やその他のエラーは無視
                    }
                    
                    DirectoryTree.Add(driveItem);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "ドライブの処理に失敗しました: {Drive}", drive);
                    // 準備ができていないドライブやその他のエラーは無視して続行
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ディレクトリツリーの読み込みに失敗しました");
        }
    }

    private async Task LoadFilesAsync(string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            IsLoading = true;
            StatusMessage = $"ファイルを読み込み中: {path}";
            
            FileList.Clear();
            CurrentPath = path;
            
            // 非同期でディレクトリとファイル一覧を取得
            var items = await Task.Run(() =>
            {
                var directories = Directory.EnumerateDirectories(path)
                    .Select(dir => new FileItem
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsDirectory = true,
                        Size = 0,
                        ModifiedDate = Directory.GetLastWriteTime(dir)
                    });

                var files = Directory.EnumerateFiles(path)
                    .Select(file => new FileItem
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        IsDirectory = false,
                        Size = new FileInfo(file).Length,
                        ModifiedDate = File.GetLastWriteTime(file)
                    });

                return directories.Concat(files).ToList();
            });

            // UIスレッドでコレクションに追加（バッチ処理で高速化）
            if (items.Count > 0)
            {
                // 大量のファイルがある場合はプログレスを表示
                if (items.Count > LARGE_DIRECTORY_THRESHOLD)
                {
                    StatusMessage = $"ファイル表示中: {items.Count}個のアイテム";
                    
                    // バッチ処理で追加（UIの応答性向上）
                    for (int i = 0; i < items.Count; i += UI_BATCH_SIZE)
                    {
                        var batch = items.Skip(i).Take(UI_BATCH_SIZE);
                        foreach (var item in batch)
                        {
                            FileList.Add(item);
                        }
                        
                        // UIスレッドに制御を戻す
                        await Task.Delay(UI_REFRESH_DELAY);
                    }
                }
                else
                {
                    foreach (var item in items)
                    {
                        FileList.Add(item);
                    }
                }
            }
            
            StatusMessage = $"{FileList.Count}個のアイテムを表示中: {path}";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "アクセス権限がありません";
            _logger.Warn("アクセス権限なし: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイル一覧の読み込みに失敗しました: {Path}", path);
            StatusMessage = "ファイル一覧の読み込みに失敗しました";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            StatusMessage = "更新中...";
            await LoadDirectoryTreeAsync();
            await LoadFilesAsync(CurrentPath);
            StatusMessage = "更新完了";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "更新に失敗しました");
            StatusMessage = "更新に失敗しました";
        }
    }

    private void NavigateUp()
    {
        try
        {
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                var parentPath = Directory.GetParent(CurrentPath)?.FullName;
                if (!string.IsNullOrEmpty(parentPath))
                {
                    _ = LoadFilesAsync(parentPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "上位ディレクトリへの移動に失敗しました");
            StatusMessage = "上位ディレクトリへの移動に失敗しました";
        }
    }

    private bool CanNavigateUp()
    {
        return !string.IsNullOrEmpty(CurrentPath) && Directory.GetParent(CurrentPath) != null;
    }

    private async Task OpenFolderAsync()
    {
        try
        {
            var selectedItem = SelectedFile;
            if (selectedItem != null && selectedItem.IsDirectory)
            {
                await LoadFilesAsync(selectedItem.FullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "フォルダを開くことができませんでした");
            StatusMessage = "フォルダを開くことができませんでした";
        }
    }
}

public class DirectoryItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsExpanded { get; set; }
    public DirectoryItem? Parent { get; set; }
    public ObservableCollection<DirectoryItem> Children { get; set; } = new();
}

public class FileItem
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long Size { get; set; }
    public DateTime ModifiedDate { get; set; }
    
    public string DisplaySize => IsDirectory ? "フォルダ" : FormatFileSize(Size);
    
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.#} {sizes[order]}";
    }
}