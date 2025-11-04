using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using AndroidAdbAnalyze.Console.Executor.Commands;
using AndroidAdbAnalyze.Console.Executor.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AndroidAdbAnalyze.Console.Executor;

/// <summary>
/// 콘솔 애플리케이션 진입점
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Serilog 초기 설정 (부트스트랩)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("=== AndroidAdbAnalyze Console Executor 시작 ===");

            // Host Builder 생성
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    // 실행 파일이 위치한 디렉토리를 기준으로 설정
                    var baseDirectory = AppContext.BaseDirectory;
                    config.SetBasePath(baseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddEnvironmentVariables(prefix: "ADBANALYZE_");
                })
                .UseSerilog((context, services, loggerConfig) => loggerConfig
                    .Enrich.FromLogContext()
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                    .WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: "logs/executor-.txt",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
                .ConfigureServices((context, services) =>
                {
                    services.AddAndroidAdbExecutor(context.Configuration);
                })
                .Build();

            await host.StartAsync();

            // Root Command 생성
            var rootCommand = new RootCommand("AndroidAdbAnalyze - ADB 로그 수집 및 분석 도구");
            rootCommand.AddCommand(AnalyzeCommand.Create(host.Services));

            // CLI 실행
            var exitCode = await rootCommand.InvokeAsync(args);
            Log.Information("=== AndroidAdbAnalyze Console Executor 종료 (ExitCode: {ExitCode}) ===", exitCode);
            
            return exitCode;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "애플리케이션 시작 중 치명적 오류 발생");
            return 99;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
