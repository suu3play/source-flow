using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SourceFlow.Core.Interfaces;
using SourceFlow.Data.Extensions;
using SourceFlow.Services.Extensions;
using SourceFlow.Services.Database;
using SourceFlow.UI.ViewModels;
using NLog;

namespace SourceFlow.UI;

public partial class App : Application
{
    private readonly IHost _host;
    private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

    public App()
    {
        _host = CreateHostBuilder().Build();
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // データベース設定
                var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                var dbPath = Path.Combine(dataDir, "history.db");
                var connectionString = $"Data Source={dbPath}";
                services.AddDataServices(connectionString);
                
                // アプリケーションサービス
                services.AddApplicationServices();
                
                // ViewModels
                services.AddTransient<MainWindowViewModel>();
                
                // Views (必要に応じて)
                services.AddTransient<MainWindow>();
            });
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            _logger.Info("SourceFlowアプリケーションを開始しています...");
            
            await _host.StartAsync();
            
            // データディレクトリの作成
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
                _logger.Info("データディレクトリを作成しました: {DataDir}", dataDir);
            }

            // データベース初期化
            var databaseService = _host.Services.GetRequiredService<IDatabaseService>();
            var dbInitialized = await databaseService.InitializeDatabaseAsync();
            if (!dbInitialized)
            {
                _logger.Error("データベースの初期化に失敗しました");
                MessageBox.Show("データベースの初期化に失敗しました。\nアプリケーションが正常に動作しない可能性があります。", 
                              "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // メインウィンドウの表示
            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            var mainWindowViewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
            mainWindow.DataContext = mainWindowViewModel;
            mainWindow.Show();
            
            _logger.Info("SourceFlowアプリケーションを開始しました");
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "アプリケーションの開始に失敗しました");
            MessageBox.Show($"アプリケーションの開始に失敗しました:\n{ex.Message}", 
                          "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
        
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger.Info("SourceFlowアプリケーションを終了しています...");
            
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            
            _logger.Info("SourceFlowアプリケーションを終了しました");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "アプリケーションの終了処理でエラーが発生しました");
        }
        
        base.OnExit(e);
    }
}

