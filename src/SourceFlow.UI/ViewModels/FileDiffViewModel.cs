using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using SourceFlow.UI.Commands;
using SourceFlow.UI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using NLog;

namespace SourceFlow.UI.ViewModels;

public class FileDiffViewModel : INotifyPropertyChanged
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly IDiffViewService _diffViewService;
    private readonly ISyntaxHighlightingService _syntaxHighlightingService;

    private FileDiffResult? _diffResult;
    private DiffViewMode _viewMode = DiffViewMode.SideBySide;
    private bool _showLineNumbers = true;
    private bool _showWhitespace = false;
    private string _searchText = string.Empty;
    private string _replaceText = string.Empty;
    private bool _isCaseSensitive = false;
    private bool _isLoading = false;
    private string _statusMessage = string.Empty;
    private string _leftSyntaxHighlighting = "Text";
    private string _rightSyntaxHighlighting = "Text";
    private int _currentSearchIndex = -1;
    private string _currentTheme = "Default";

    public FileDiffViewModel(IDiffViewService diffViewService, ISyntaxHighlightingService syntaxHighlightingService)
    {
        _diffViewService = diffViewService;
        _syntaxHighlightingService = syntaxHighlightingService;

        // Commands
        LoadFilesCommand = new RelayCommand<(string Left, string Right)>(async param => await LoadFilesAsync(param.Left, param.Right));
        CompareContentCommand = new RelayCommand<(string Left, string Right)>(async param => await CompareContentAsync(param.Left, param.Right));
        SearchCommand = new RelayCommand(async () => await SearchAsync());
        NextSearchResultCommand = new RelayCommand(NextSearchResult, () => SearchResults?.Any() == true);
        PreviousSearchResultCommand = new RelayCommand(PreviousSearchResult, () => SearchResults?.Any() == true);
        ReplaceCommand = new RelayCommand(async () => await ReplaceAsync(), () => DiffResult != null && !string.IsNullOrWhiteSpace(SearchText) && !string.IsNullOrWhiteSpace(ReplaceText));
        ReplaceAllCommand = new RelayCommand(async () => await ReplaceAllAsync(), () => DiffResult != null && !string.IsNullOrWhiteSpace(SearchText) && !string.IsNullOrWhiteSpace(ReplaceText));
        ExportCommand = new RelayCommand<string?>(async path => await ExportAsync(path ?? string.Empty), path => DiffResult != null && !string.IsNullOrEmpty(path));
        RefreshCommand = new RelayCommand(async () => await RefreshAsync(), () => !string.IsNullOrEmpty(LeftFilePath) && !string.IsNullOrEmpty(RightFilePath));
        ChangeThemeCommand = new RelayCommand<string>(theme => ChangeTheme(theme ?? "Default"));

        SearchResults = new ObservableCollection<SearchResult>();
        AvailableViewModes = new ObservableCollection<DiffViewMode>
        {
            DiffViewMode.SideBySide,
            DiffViewMode.Inline,
            DiffViewMode.Unified
        };

        AvailableThemes = new ObservableCollection<string>
        {
            "Default",
            "Dark"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FileDiffResult? DiffResult
    {
        get => _diffResult;
        private set
        {
            _diffResult = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasDifferences));
            OnPropertyChanged(nameof(DiffStatistics));
        }
    }

    public DiffViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            _viewMode = value;
            OnPropertyChanged();
        }
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            _showLineNumbers = value;
            OnPropertyChanged();
        }
    }

    public bool ShowWhitespace
    {
        get => _showWhitespace;
        set
        {
            _showWhitespace = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
        }
    }

    public string ReplaceText
    {
        get => _replaceText;
        set
        {
            _replaceText = value;
            OnPropertyChanged();
        }
    }

    public bool IsCaseSensitive
    {
        get => _isCaseSensitive;
        set
        {
            _isCaseSensitive = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string LeftSyntaxHighlighting
    {
        get => _leftSyntaxHighlighting;
        private set
        {
            _leftSyntaxHighlighting = value;
            OnPropertyChanged();
        }
    }

    public string RightSyntaxHighlighting
    {
        get => _rightSyntaxHighlighting;
        private set
        {
            _rightSyntaxHighlighting = value;
            OnPropertyChanged();
        }
    }

    public int CurrentSearchIndex
    {
        get => _currentSearchIndex;
        private set
        {
            _currentSearchIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SearchResultsText));
        }
    }

    public string CurrentTheme
    {
        get => _currentTheme;
        private set
        {
            _currentTheme = value;
            OnPropertyChanged();
        }
    }

    public string LeftFilePath { get; private set; } = string.Empty;
    public string RightFilePath { get; private set; } = string.Empty;

    public bool HasDifferences => DiffResult?.HasChanges ?? false;

    public string DiffStatistics
    {
        get
        {
            if (DiffResult == null) return "差分なし";
            
            var leftLines = DiffResult.LeftLines;
            var addedCount = leftLines.Count(l => l.ChangeType == ChangeType.Add);
            var deletedCount = leftLines.Count(l => l.ChangeType == ChangeType.Delete);
            var modifiedCount = leftLines.Count(l => l.ChangeType == ChangeType.Modify);
            
            return $"追加: {addedCount}, 削除: {deletedCount}, 変更: {modifiedCount}";
        }
    }

    public string SearchResultsText
    {
        get
        {
            if (SearchResults?.Any() != true) return "検索結果なし";
            return $"{CurrentSearchIndex + 1} / {SearchResults.Count}";
        }
    }

    public ObservableCollection<SearchResult> SearchResults { get; }
    public ObservableCollection<DiffViewMode> AvailableViewModes { get; }
    public ObservableCollection<string> AvailableThemes { get; }

    // Commands
    public ICommand LoadFilesCommand { get; }
    public ICommand CompareContentCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand NextSearchResultCommand { get; }
    public ICommand PreviousSearchResultCommand { get; }
    public ICommand ReplaceCommand { get; }
    public ICommand ReplaceAllCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ChangeThemeCommand { get; }

    public async Task LoadFilesAsync(string leftPath, string rightPath)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "ファイル差分を計算中...";

            LeftFilePath = leftPath;
            RightFilePath = rightPath;

            _logger.Info("ファイル差分比較開始: {LeftPath} vs {RightPath}", leftPath, rightPath);

            // シンタックスハイライト検出
            LeftSyntaxHighlighting = await _syntaxHighlightingService.DetectSyntaxFromFileAsync(leftPath);
            RightSyntaxHighlighting = await _syntaxHighlightingService.DetectSyntaxFromFileAsync(rightPath);

            // 差分計算
            DiffResult = await _diffViewService.GenerateFileDiffAsync(leftPath, rightPath);

            StatusMessage = HasDifferences 
                ? $"差分を検出しました。{DiffStatistics}" 
                : "ファイルに差分はありません。";

            _logger.Info("ファイル差分比較完了: HasChanges={HasChanges}", HasDifferences);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "ファイル差分比較に失敗しました");
            StatusMessage = $"エラー: {ex.Message}";
            MessageBox.Show($"ファイル差分の計算に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task CompareContentAsync(string leftContent, string rightContent)
    {
        try
        {
            IsLoading = true;
            StatusMessage = "コンテンツ差分を計算中...";

            LeftFilePath = "左側コンテンツ";
            RightFilePath = "右側コンテンツ";

            // シンタックスハイライト検出
            LeftSyntaxHighlighting = await _syntaxHighlightingService.DetectSyntaxFromContentAsync(leftContent);
            RightSyntaxHighlighting = await _syntaxHighlightingService.DetectSyntaxFromContentAsync(rightContent);

            // 差分計算
            DiffResult = await _diffViewService.GenerateContentDiffAsync(leftContent, rightContent, "左側", "右側");

            StatusMessage = HasDifferences 
                ? $"差分を検出しました。{DiffStatistics}" 
                : "コンテンツに差分はありません。";

            _logger.Info("コンテンツ差分比較完了: HasChanges={HasChanges}", HasDifferences);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "コンテンツ差分比較に失敗しました");
            StatusMessage = $"エラー: {ex.Message}";
            MessageBox.Show($"コンテンツ差分の計算に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SearchAsync()
    {
        if (DiffResult == null || string.IsNullOrWhiteSpace(SearchText))
        {
            SearchResults.Clear();
            CurrentSearchIndex = -1;
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "検索中...";

            var results = await _diffViewService.SearchInDiffAsync(DiffResult, SearchText, IsCaseSensitive);
            
            SearchResults.Clear();
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }

            CurrentSearchIndex = SearchResults?.Any() == true ? 0 : -1;
            StatusMessage = SearchResults?.Any() == true 
                ? $"検索完了: {SearchResults.Count}件の結果" 
                : "検索結果が見つかりませんでした。";

            _logger.Info("差分内検索完了: SearchText={SearchText}, Results={ResultCount}", SearchText, SearchResults?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "差分内検索に失敗しました");
            StatusMessage = $"検索エラー: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void NextSearchResult()
    {
        if (SearchResults?.Any() == true && CurrentSearchIndex < SearchResults.Count - 1)
        {
            CurrentSearchIndex++;
        }
    }

    public void PreviousSearchResult()
    {
        if (SearchResults?.Any() == true && CurrentSearchIndex > 0)
        {
            CurrentSearchIndex--;
        }
    }

    public async Task ReplaceAsync()
    {
        if (DiffResult == null || string.IsNullOrWhiteSpace(SearchText) || string.IsNullOrWhiteSpace(ReplaceText))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "置換中...";

            var result = await _diffViewService.ReplaceInDiffAsync(DiffResult, SearchText, ReplaceText, IsCaseSensitive, false);
            
            if (result.IsSuccess)
            {
                StatusMessage = $"置換完了: {result.ReplacedCount}件の置換";
                
                // 差分結果を更新してビューに反映
                OnPropertyChanged(nameof(DiffResult));
                
                // 検索結果をクリア（置換によって位置が変わるため）
                SearchResults.Clear();
                CurrentSearchIndex = -1;
                
                _logger.Info("置換完了: SearchText={SearchText}, ReplaceText={ReplaceText}, Count={Count}", 
                    SearchText, ReplaceText, result.ReplacedCount);
            }
            else
            {
                StatusMessage = $"置換エラー: {result.ErrorMessage}";
                MessageBox.Show($"置換に失敗しました:\n{result.ErrorMessage}", "置換エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "置換処理に失敗しました");
            StatusMessage = $"置換エラー: {ex.Message}";
            MessageBox.Show($"置換処理に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReplaceAllAsync()
    {
        if (DiffResult == null || string.IsNullOrWhiteSpace(SearchText) || string.IsNullOrWhiteSpace(ReplaceText))
            return;

        var confirmResult = MessageBox.Show(
            $"すべての「{SearchText}」を「{ReplaceText}」に置換しますか？",
            "全置換の確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmResult != MessageBoxResult.Yes)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "全置換中...";

            var result = await _diffViewService.ReplaceInDiffAsync(DiffResult, SearchText, ReplaceText, IsCaseSensitive, true);
            
            if (result.IsSuccess)
            {
                StatusMessage = $"全置換完了: {result.ReplacedCount}件の置換";
                
                // 差分結果を更新してビューに反映
                OnPropertyChanged(nameof(DiffResult));
                
                // 検索結果をクリア（置換によって位置が変わるため）
                SearchResults.Clear();
                CurrentSearchIndex = -1;
                
                MessageBox.Show($"全置換が完了しました:\n{result.ReplacedCount}件の置換を行いました", 
                    "全置換完了", MessageBoxButton.OK, MessageBoxImage.Information);
                
                _logger.Info("全置換完了: SearchText={SearchText}, ReplaceText={ReplaceText}, Count={Count}", 
                    SearchText, ReplaceText, result.ReplacedCount);
            }
            else
            {
                StatusMessage = $"全置換エラー: {result.ErrorMessage}";
                MessageBox.Show($"全置換に失敗しました:\n{result.ErrorMessage}", "全置換エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "全置換処理に失敗しました");
            StatusMessage = $"全置換エラー: {ex.Message}";
            MessageBox.Show($"全置換処理に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ExportAsync(string filePath)
    {
        if (DiffResult == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = "エクスポート中...";

            await _diffViewService.SaveDiffResultAsync(DiffResult, filePath);
            
            StatusMessage = $"エクスポート完了: {filePath}";
            MessageBox.Show($"差分結果を正常にエクスポートしました:\n{filePath}", "エクスポート完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "差分結果のエクスポートに失敗しました");
            StatusMessage = $"エクスポートエラー: {ex.Message}";
            MessageBox.Show($"エクスポートに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task RefreshAsync()
    {
        if (!string.IsNullOrEmpty(LeftFilePath) && !string.IsNullOrEmpty(RightFilePath))
        {
            await LoadFilesAsync(LeftFilePath, RightFilePath);
        }
    }

    public void ChangeTheme(string themeName)
    {
        CurrentTheme = themeName;
        _logger.Info("テーマ変更: {ThemeName}", themeName);
        
        // DiffResultがある場合、再描画をトリガー
        if (DiffResult != null)
        {
            OnPropertyChanged(nameof(DiffResult));
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}