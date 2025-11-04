using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Console.Executor.Configuration;
using AndroidAdbAnalyze.Console.Executor.Services.Adb;
using AndroidAdbAnalyze.Console.Executor.Services.Device;
using AndroidAdbAnalyze.Console.Executor.Services.LogCollection;
using AndroidAdbAnalyze.Console.Executor.Services.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AndroidAdbAnalyze.Console.Executor.Extensions;

/// <summary>
/// DI 컨테이너 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// AndroidAdbAnalyze Console Executor 서비스 등록
    /// </summary>
    public static IServiceCollection AddAndroidAdbExecutor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ===== Configuration Binding =====
        services.Configure<AdbConfiguration>(
            configuration.GetSection("Adb"));
        services.Configure<LogCollectionConfiguration>(
            configuration.GetSection("LogCollection"));
        services.Configure<AnalysisConfiguration>(
            configuration.GetSection("Analysis"));
        services.Configure<OutputConfiguration>(
            configuration.GetSection("Output"));
        
        // ===== Console.Executor Services =====
        
        // ADB Command Executor
        services.AddScoped<IAdbCommandExecutor>(sp =>
        {
            var adbConfig = configuration.GetSection("Adb").Get<AdbConfiguration>()
                ?? new AdbConfiguration();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AdbCommandExecutor>>();
            
            return new AdbCommandExecutor(
                adbPath: adbConfig.ExecutablePath,
                defaultTimeout: TimeSpan.FromSeconds(adbConfig.CommandTimeout),
                defaultRetryCount: adbConfig.RetryCount,
                defaultRetryDelay: TimeSpan.FromMilliseconds(adbConfig.RetryDelayMs),
                logger: logger);
        });
        
        // Device Manager
        services.AddScoped<IDeviceManager, DeviceManager>();
        
        // Log Collector
        services.AddScoped<ILogCollector, LogCollector>();
        
        // Pipeline Service
        services.AddScoped<IPipelineService, PipelineService>();
        
        // ===== Parser Services =====
        // ILogParser는 PipelineService에서 각 로그 파일마다 동적으로 생성
        // (각 로그마다 다른 YAML 설정 파일을 사용하기 때문)
        
        // ===== Analysis Options =====
        // Analysis 모듈이 필요로 하는 AnalysisOptions를 DI에 등록
        services.AddSingleton(sp =>
        {
            var analysisConfig = configuration.GetSection("Analysis").Get<AnalysisConfiguration>()
                ?? new AnalysisConfiguration();
            
            return new AndroidAdbAnalyze.Analysis.Models.Options.AnalysisOptions
            {
                MinConfidenceThreshold = analysisConfig.MinConfidenceThreshold,
                EventCorrelationWindow = TimeSpan.FromSeconds(analysisConfig.EventCorrelationWindowSeconds),
                MaxSessionGap = TimeSpan.FromMinutes(analysisConfig.MaxSessionGapMinutes),
                DeduplicationSimilarityThreshold = analysisConfig.DeduplicationSimilarityThreshold
            };
        });
        
        // ===== Analysis Services =====
        services.AddAndroidAdbAnalysis();
        
        return services;
    }
}

