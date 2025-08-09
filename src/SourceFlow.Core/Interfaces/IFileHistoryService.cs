using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface IFileHistoryService
{
    Task<List<FileHistory>> GetHistoryAsync(string filePath);
    Task<FileHistory?> GetVersionAsync(string filePath, int version);
    Task AddHistoryAsync(FileHistory history);
    Task<List<FileHistory>> GetRecentHistoryAsync(int count = 100);
}