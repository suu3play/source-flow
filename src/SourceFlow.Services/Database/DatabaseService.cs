using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SourceFlow.Data.Context;

namespace SourceFlow.Services.Database;

public interface IDatabaseService
{
    Task<bool> InitializeDatabaseAsync();
    Task<bool> IsDatabaseCreatedAsync();
    Task<bool> ApplyMigrationsAsync();
}

public class DatabaseService : IDatabaseService
{
    private readonly SourceFlowDbContext _context;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(SourceFlowDbContext context, ILogger<DatabaseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> InitializeDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("データベース初期化を開始します...");

            // データベースが存在するかチェック
            var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
            {
                _logger.LogInformation("データベースが存在しないため、新規作成します。");
                await _context.Database.EnsureCreatedAsync();
                _logger.LogInformation("データベースの作成が完了しました。");
                return true;
            }

            _logger.LogInformation("データベースが既に存在します。マイグレーション適用を確認中...");
            
            // マイグレーション適用
            var migrationResult = await ApplyMigrationsAsync();
            if (migrationResult)
            {
                _logger.LogInformation("データベース初期化が正常に完了しました。");
            }
            else
            {
                _logger.LogWarning("マイグレーション適用で問題が発生しましたが、データベースは使用可能です。");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データベース初期化中にエラーが発生しました: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> IsDatabaseCreatedAsync()
    {
        try
        {
            return await _context.Database.CanConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "データベース接続確認中にエラーが発生しました: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> ApplyMigrationsAsync()
    {
        try
        {
            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("適用待ちのマイグレーションが {Count} 件見つかりました。", pendingMigrations.Count());
                await _context.Database.MigrateAsync();
                _logger.LogInformation("マイグレーションの適用が完了しました。");
            }
            else
            {
                _logger.LogInformation("適用待ちのマイグレーションはありません。");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "マイグレーション適用中にエラーが発生しました: {Message}", ex.Message);
            return false;
        }
    }
}