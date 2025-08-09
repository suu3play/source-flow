using SourceFlow.Core.Models;

namespace SourceFlow.Core.Interfaces;

public interface ISourceSyncService
{
    Task<SyncJob> SyncAsync(SourceConfiguration source, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(SourceConfiguration source, CancellationToken cancellationToken = default);
    Task<List<string>> ListFilesAsync(SourceConfiguration source, CancellationToken cancellationToken = default);
}