using SourceFlow.UI.ViewModels;
using SourceFlow.Core.Models;
using SourceFlow.Core.Enums;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Win32;
using System.Windows.Media;
using System.Text;

namespace SourceFlow.UI.Views;

public partial class FileDiffView : UserControl
{
    private FileDiffViewModel? ViewModel => DataContext as FileDiffViewModel;
    
    public FileDiffView()
    {
        InitializeComponent();
        
        // ViewModelのPropertyChangedイベントを購読
        DataContextChanged += OnDataContextChanged;
        
        // スクロール同期の設定
        SetupScrollSynchronization();
        
        // 検索結果のナビゲーション設定
        SetupSearchNavigation();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is FileDiffViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        
        if (e.NewValue is FileDiffViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FileDiffViewModel.DiffResult):
                UpdateDiffDisplay();
                break;
            case nameof(FileDiffViewModel.ViewMode):
                UpdateViewMode();
                break;
            case nameof(FileDiffViewModel.LeftSyntaxHighlighting):
                UpdateSyntaxHighlighting(LeftTextEditor, ViewModel?.LeftSyntaxHighlighting ?? "Text");
                break;
            case nameof(FileDiffViewModel.RightSyntaxHighlighting):
                UpdateSyntaxHighlighting(RightTextEditor, ViewModel?.RightSyntaxHighlighting ?? "Text");
                break;
            case nameof(FileDiffViewModel.CurrentSearchIndex):
                NavigateToSearchResult();
                break;
        }
    }

    private void UpdateDiffDisplay()
    {
        if (ViewModel?.DiffResult == null) return;

        try
        {
            var diffResult = ViewModel.DiffResult;

            // 左側エディター
            var leftContent = new StringBuilder();
            foreach (var line in diffResult.LeftLines)
            {
                leftContent.AppendLine(line.Content);
            }
            LeftTextEditor.Text = leftContent.ToString();

            // 右側エディター  
            var rightContent = new StringBuilder();
            foreach (var line in diffResult.RightLines)
            {
                rightContent.AppendLine(line.Content);
            }
            RightTextEditor.Text = rightContent.ToString();

            // インラインエディター（差分形式）
            var inlineContent = new StringBuilder();
            foreach (var line in diffResult.LeftLines)
            {
                var prefix = line.ChangeType switch
                {
                    ChangeType.Add => "+ ",
                    ChangeType.Delete => "- ",
                    ChangeType.Modify => "~ ",
                    _ => "  "
                };
                inlineContent.AppendLine($"{prefix}{line.Content}");
            }
            InlineTextEditor.Text = inlineContent.ToString();

            // 統合エディター（Git diff形式）
            var unifiedContent = new StringBuilder();
            unifiedContent.AppendLine($"--- {diffResult.LeftFilePath}");
            unifiedContent.AppendLine($"+++ {diffResult.RightFilePath}");
            unifiedContent.AppendLine($"@@ -{diffResult.LeftLines.Count},+{diffResult.RightLines.Count} @@");
            
            foreach (var line in diffResult.LeftLines.Where(l => l.ChangeType != ChangeType.NoChange))
            {
                var prefix = line.ChangeType switch
                {
                    ChangeType.Add => "+",
                    ChangeType.Delete => "-",
                    ChangeType.Modify => "~",
                    _ => " "
                };
                unifiedContent.AppendLine($"{prefix}{line.Content}");
            }
            UnifiedTextEditor.Text = unifiedContent.ToString();

            // 行の色付け
            ApplyDiffColoring();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"差分表示の更新に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateViewMode()
    {
        if (ViewModel == null) return;

        // タブの表示切り替え
        foreach (object item in DiffTabControl.Items)
        {
            if (item is TabItem tab)
            {
                tab.Visibility = Visibility.Collapsed;
            }
        }

        switch (ViewModel.ViewMode)
        {
            case DiffViewMode.SideBySide:
                if (DiffTabControl.Items[0] is TabItem sideTab)
                    sideTab.Visibility = Visibility.Visible;
                DiffTabControl.SelectedIndex = 0;
                break;
            case DiffViewMode.Inline:
                if (DiffTabControl.Items[1] is TabItem inlineTab)
                    inlineTab.Visibility = Visibility.Visible;
                DiffTabControl.SelectedIndex = 1;
                break;
            case DiffViewMode.Unified:
                if (DiffTabControl.Items[2] is TabItem unifiedTab)
                    unifiedTab.Visibility = Visibility.Visible;
                DiffTabControl.SelectedIndex = 2;
                break;
        }
    }

    private void UpdateSyntaxHighlighting(TextEditor editor, string syntaxName)
    {
        try
        {
            var highlightingDefinition = HighlightingManager.Instance.GetDefinition(syntaxName);
            editor.SyntaxHighlighting = highlightingDefinition;
        }
        catch
        {
            // フォールバック: テキストハイライト
            editor.SyntaxHighlighting = null;
        }
    }

    private void ApplyDiffColoring()
    {
        if (ViewModel?.DiffResult == null) return;

        try
        {
            // 左側エディターの行の色付け
            ApplyLineColoring(LeftTextEditor, ViewModel.DiffResult.LeftLines);
            
            // 右側エディターの行の色付け  
            ApplyLineColoring(RightTextEditor, ViewModel.DiffResult.RightLines);
            
            // インライン・統合エディターの色付け
            ApplyInlineDiffColoring();
        }
        catch (Exception ex)
        {
            // 色付けエラーは警告レベル（表示は継続）
            System.Diagnostics.Debug.WriteLine($"差分の色付けに失敗: {ex.Message}");
        }
    }

    private void ApplyLineColoring(TextEditor editor, List<LineDiff> lines)
    {
        // 背景色設定のカスタム実装（簡易版）
        var document = editor.Document;
        
        for (int i = 0; i < Math.Min(lines.Count, document.LineCount); i++)
        {
            var line = lines[i];
            var documentLine = document.GetLineByNumber(i + 1);
            
            // 行の背景色を設定（実装例）
            var brush = line.ChangeType switch
            {
                ChangeType.Add => new SolidColorBrush(Color.FromArgb(80, 0, 255, 0)), // 薄い緑
                ChangeType.Delete => new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), // 薄い赤
                ChangeType.Modify => new SolidColorBrush(Color.FromArgb(80, 255, 255, 0)), // 薄い黄
                _ => Brushes.Transparent
            };
            
            // Note: 実際の背景色適用はAvalonEditのTransformationインターフェースを使用
            // ここでは簡易実装のプレースホルダー
        }
    }

    private void ApplyInlineDiffColoring()
    {
        // インライン表示での +/- プレフィックスに応じた色付け
        // 実装は省略（AvalonEditのColorizing機能を使用）
    }

    private void SetupScrollSynchronization()
    {
        // 左右エディターのスクロール同期（簡易実装）
        // Note: AvalonEditの正確なスクロール同期は、TextAreaのScrollViewerプロパティアクセスが必要
        // 実装は将来のバージョンで改良
        try
        {
            // プレースホルダー実装
        }
        catch
        {
            // スクロール同期機能はオプショナル
        }
    }

    private void SetupSearchNavigation()
    {
        // 検索テキストボックスでのEnterキー処理
        SearchTextBox.KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ViewModel?.SearchCommand.Execute(null);
            }
        };
    }

    private void NavigateToSearchResult()
    {
        if (ViewModel?.SearchResults?.Any() != true || ViewModel.CurrentSearchIndex < 0)
            return;

        try
        {
            var currentResult = ViewModel.SearchResults[ViewModel.CurrentSearchIndex];
            var editor = currentResult.IsLeftSide ? LeftTextEditor : RightTextEditor;
            
            // 該当行にスクロール・ハイライト
            if (currentResult.LineNumber <= editor.Document.LineCount)
            {
                var line = editor.Document.GetLineByNumber(currentResult.LineNumber);
                editor.ScrollToLine(currentResult.LineNumber);
                editor.Select(line.Offset + currentResult.CharIndex, currentResult.Length);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"検索結果のナビゲーションに失敗: {ex.Message}");
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.DiffResult == null)
        {
            MessageBox.Show("エクスポートする差分結果がありません。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new SaveFileDialog
        {
            Title = "差分結果をエクスポート",
            Filter = "HTML ファイル (*.html)|*.html|JSON ファイル (*.json)|*.json|テキスト ファイル (*.txt)|*.txt|CSV ファイル (*.csv)|*.csv",
            DefaultExt = "html",
            FileName = $"diff_result_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (saveDialog.ShowDialog() == true)
        {
            ViewModel.ExportCommand.Execute(saveDialog.FileName);
        }
    }

    // ファイルドロップ対応（オプション）
    private void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length >= 2 && ViewModel != null)
            {
                _ = ViewModel.LoadFilesAsync(files[0], files[1]);
            }
        }
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }
}