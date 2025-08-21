using SourceFlow.Core.Interfaces;
using SourceFlow.Core.Models;
using NLog;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace SourceFlow.Services.Diff;

public class DiffCacheService : IDiffCacheService
{
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
    private readonly string _cacheDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly int _maxCacheAgeDays = 30;
    private readonly long _maxCacheSizeBytes = 100 * 1024 * 1024; // 100MB

    public DiffCacheService(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(Path.GetTempPath(), "SourceFlowDiffCache");
        Directory.CreateDirectory(_cacheDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        _logger.Info("差分キャッシュサービス初期化: {CacheDirectory}", _cacheDirectory);
    }

    public async Task<FileDiffResult?> GetCachedDiffAsync(
        string leftPath, 
        string rightPath, 
        DateTime leftModified, 
        DateTime rightModified)
    {
        try
        {
            var cacheKey = GenerateCacheKey(leftPath, rightPath, leftModified, rightModified);
            var cacheFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");

            if (!File.Exists(cacheFilePath))
            {
                _logger.Debug("キャッシュファイルが見つかりません: {CacheKey}", cacheKey);
                return null;
            }

            var cacheFileInfo = new FileInfo(cacheFilePath);
            if (DateTime.Now - cacheFileInfo.CreationTime > TimeSpan.FromDays(_maxCacheAgeDays))
            {
                _logger.Debug("キャッシュファイルが期限切れ: {CacheKey}", cacheKey);
                await DeleteCacheFileAsync(cacheFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(cacheFilePath, Encoding.UTF8);
            var cachedResult = JsonSerializer.Deserialize<CachedDiffResult>(json, _jsonOptions);

            if (cachedResult?.DiffResult != null)
            {
                _logger.Info("キャッシュからの差分結果読み込み成功: {CacheKey}", cacheKey);
                return cachedResult.DiffResult;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "キャッシュからの差分結果読み込みに失敗");
            return null;
        }
    }

    public async Task CacheDiffResultAsync(
        FileDiffResult diffResult, 
        DateTime leftModified, 
        DateTime rightModified)
    {
        try
        {
            var cacheKey = GenerateCacheKey(diffResult.LeftFilePath, diffResult.RightFilePath, leftModified, rightModified);
            var cacheFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");

            var cachedResult = new CachedDiffResult
            {
                CacheKey = cacheKey,
                LeftModified = leftModified,
                RightModified = rightModified,
                CachedAt = DateTime.Now,
                DiffResult = diffResult
            };

            var json = JsonSerializer.Serialize(cachedResult, _jsonOptions);
            await File.WriteAllTextAsync(cacheFilePath, json, Encoding.UTF8);

            _logger.Info("差分結果をキャッシュに保存: {CacheKey}, Size={Size}bytes", cacheKey, json.Length);

            // キャッシュサイズ制限チェック
            await EnforceCacheLimitsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "差分結果のキャッシュ保存に失敗");
        }
    }

    public async Task<VirtualizedDiffResult?> GetCachedVirtualizedDiffAsync(
        string leftPath, 
        string rightPath, 
        DateTime leftModified, 
        DateTime rightModified)
    {
        try
        {
            var cacheKey = GenerateVirtualizedCacheKey(leftPath, rightPath, leftModified, rightModified);
            var cacheFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}_virt.json");

            if (!File.Exists(cacheFilePath))
            {
                _logger.Debug("仮想化キャッシュファイルが見つかりません: {CacheKey}", cacheKey);
                return null;
            }

            var cacheFileInfo = new FileInfo(cacheFilePath);
            if (DateTime.Now - cacheFileInfo.CreationTime > TimeSpan.FromDays(_maxCacheAgeDays))
            {
                _logger.Debug("仮想化キャッシュファイルが期限切れ: {CacheKey}", cacheKey);
                await DeleteCacheFileAsync(cacheFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(cacheFilePath, Encoding.UTF8);
            var cachedResult = JsonSerializer.Deserialize<CachedVirtualizedDiffResult>(json, _jsonOptions);

            if (cachedResult?.VirtualizedResult != null)
            {
                _logger.Info("キャッシュからの仮想化差分結果読み込み成功: {CacheKey}", cacheKey);
                return cachedResult.VirtualizedResult;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "仮想化キャッシュからの読み込みに失敗");
            return null;
        }
    }

    public async Task CacheVirtualizedDiffAsync(
        VirtualizedDiffResult diffResult, 
        DateTime leftModified, 
        DateTime rightModified)
    {
        try
        {
            var cacheKey = GenerateVirtualizedCacheKey(diffResult.LeftFilePath, diffResult.RightFilePath, leftModified, rightModified);
            var cacheFilePath = Path.Combine(_cacheDirectory, $"{cacheKey}_virt.json");

            var cachedResult = new CachedVirtualizedDiffResult
            {
                CacheKey = cacheKey,
                LeftModified = leftModified,
                RightModified = rightModified,
                CachedAt = DateTime.Now,
                VirtualizedResult = diffResult
            };

            var json = JsonSerializer.Serialize(cachedResult, _jsonOptions);
            await File.WriteAllTextAsync(cacheFilePath, json, Encoding.UTF8);

            _logger.Info("仮想化差分結果をキャッシュに保存: {CacheKey}, Size={Size}bytes", cacheKey, json.Length);

            await EnforceCacheLimitsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "仮想化差分結果のキャッシュ保存に失敗");
        }
    }

    public async Task ClearCacheAsync()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                var files = Directory.GetFiles(_cacheDirectory, "*.json");
                var deleteTasks = files.Select(DeleteCacheFileAsync);
                await Task.WhenAll(deleteTasks);
                
                _logger.Info("キャッシュクリア完了: {FileCount}ファイル削除", files.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "キャッシュクリアに失敗");
        }
    }

    public async Task<long> GetCacheSizeAsync()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                return 0;

            var files = Directory.GetFiles(_cacheDirectory, "*.json");
            var tasks = files.Select(async f => await Task.Run(() => new FileInfo(f).Length));
            var sizes = await Task.WhenAll(tasks);
            
            return sizes.Sum();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "キャッシュサイズ計算に失敗");
            return 0;
        }
    }

    public async Task<int> GetCacheCountAsync()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                return 0;

            await Task.CompletedTask; // 非同期形式維持
            return Directory.GetFiles(_cacheDirectory, "*.json").Length;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "キャッシュファイル数計算に失敗");
            return 0;
        }
    }

    private string GenerateCacheKey(string leftPath, string rightPath, DateTime leftModified, DateTime rightModified)
    {
        var input = $"{leftPath}|{rightPath}|{leftModified:O}|{rightModified:O}";
        return ComputeHash(input);
    }

    private string GenerateVirtualizedCacheKey(string leftPath, string rightPath, DateTime leftModified, DateTime rightModified)
    {
        var input = $"VIRT|{leftPath}|{rightPath}|{leftModified:O}|{rightModified:O}";
        return ComputeHash(input);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16]; // 最初の16文字のみ使用
    }

    private async Task EnforceCacheLimitsAsync()
    {
        try
        {
            var currentSize = await GetCacheSizeAsync();
            if (currentSize <= _maxCacheSizeBytes)
                return;

            _logger.Info("キャッシュサイズ制限に達しました。古いファイルを削除開始: {CurrentSize}bytes", currentSize);

            var files = Directory.GetFiles(_cacheDirectory, "*.json")
                .Select(f => new FileInfo(f))
                .OrderBy(fi => fi.CreationTime)
                .ToArray();

            var deletedSize = 0L;
            var deletedCount = 0;

            foreach (var file in files)
            {
                if (currentSize - deletedSize <= _maxCacheSizeBytes * 0.8) // 80%まで削減
                    break;

                deletedSize += file.Length;
                deletedCount++;
                await DeleteCacheFileAsync(file.FullName);
            }

            _logger.Info("キャッシュサイズ制限適用完了: {DeletedCount}ファイル削除, {DeletedSize}bytes解放", 
                deletedCount, deletedSize);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "キャッシュサイズ制限適用に失敗");
        }
    }

    private async Task DeleteCacheFileAsync(string filePath)
    {
        try
        {
            await Task.Run(() => File.Delete(filePath));
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "キャッシュファイル削除に失敗: {FilePath}", filePath);
        }
    }
}

public class CachedDiffResult
{
    public string CacheKey { get; set; } = string.Empty;
    public DateTime LeftModified { get; set; }
    public DateTime RightModified { get; set; }
    public DateTime CachedAt { get; set; }
    public FileDiffResult? DiffResult { get; set; }
}

public class CachedVirtualizedDiffResult
{
    public string CacheKey { get; set; } = string.Empty;
    public DateTime LeftModified { get; set; }
    public DateTime RightModified { get; set; }
    public DateTime CachedAt { get; set; }
    public VirtualizedDiffResult? VirtualizedResult { get; set; }
}