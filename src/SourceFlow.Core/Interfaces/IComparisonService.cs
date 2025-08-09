using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface IComparisonService
{
    Task<List<FileComparisonResult>> CompareDirectoriesAsync(string sourcePath, string targetPath);
    Task LaunchWinMergeAsync(string leftFile, string rightFile);
    Task<bool> IsWinMergeAvailableAsync();
}