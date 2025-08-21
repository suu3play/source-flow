using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface IDiffViewService
{
    Task<FileDiffResult> GenerateFileDiffAsync(string leftFilePath, string rightFilePath);
    Task<FileDiffResult> GenerateContentDiffAsync(string leftContent, string rightContent, string leftLabel = "Left", string rightLabel = "Right");
    Task<List<SearchResult>> SearchInDiffAsync(FileDiffResult diffResult, string searchText, bool caseSensitive = false);
    Task<ReplaceResult> ReplaceInDiffAsync(FileDiffResult diffResult, string searchText, string replaceText, bool caseSensitive = false, bool replaceAll = false);
    Task SaveDiffResultAsync(FileDiffResult diffResult, string outputPath);
    Task<bool> CanProcessFileAsync(string filePath);
}

public interface ISyntaxHighlightingService
{
    Task<string> DetectSyntaxFromFileAsync(string filePath);
    Task<string> DetectSyntaxFromContentAsync(string content, string fileName = "");
    Task<SyntaxHighlightingConfig> GetHighlightingConfigAsync(string syntaxName);
    Task<List<SyntaxHighlightingConfig>> GetAvailableHighlightingConfigsAsync();
    Task<bool> RegisterCustomHighlightingAsync(SyntaxHighlightingConfig config);
}

public interface ITextDiffEngine
{
    Task<List<LineDiff>> ComputeLineDiffAsync(string[] leftLines, string[] rightLines);
    Task<List<CharacterDiff>> ComputeCharacterDiffAsync(string leftLine, string rightLine);
    Task<FileDiffResult> ProcessFileDiffAsync(string leftContent, string rightContent, string leftPath, string rightPath);
}