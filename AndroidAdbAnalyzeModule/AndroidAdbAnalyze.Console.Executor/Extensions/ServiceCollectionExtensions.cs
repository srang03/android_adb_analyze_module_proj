using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Console.Executor.Configuration;
using AndroidAdbAnalyze.Console.Executor.Services.Adb;
using AndroidAdbAnalyze.Console.Executor.Services.Device;
using AndroidAdbAnalyze.Console.Executor.Services.LogCollection;
using AndroidAdbAnalyze.Console.Executor.Services.Output;
using AndroidAdbAnalyze.Console.Executor.Services.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        
        // Result Output Service (결과 저장)
        services.AddScoped<IResultOutputService, ResultOutputService>();
        
        // ===== Parser Services =====
        // ILogParser는 PipelineService에서 각 로그 파일마다 동적으로 생성
        // (각 로그마다 다른 YAML 설정 파일을 사용하기 때문)
        
        // ===== YAML Configuration Loading =====
        // Analysis 설정에서 YAML 파일 경로 읽기
        var analysisConfig = configuration.GetSection("Analysis").Get<AnalysisConfiguration>()
            ?? new AnalysisConfiguration();
        
        ArtifactDetectionConfig? artifactConfig = null;
        if (!string.IsNullOrWhiteSpace(analysisConfig.ConfigFile))
        {
            // YAML 파일 경로 해석 (상대 경로는 AppContext.BaseDirectory 기준)
            var baseDirectory = AppContext.BaseDirectory;
            var yamlPath = Path.IsPathRooted(analysisConfig.ConfigFile)
                ? analysisConfig.ConfigFile
                : Path.Combine(baseDirectory, analysisConfig.ConfigFile);
            
            // YAML 설정 로드 (파일이 없거나 오류 시 기본값 반환)
            // 순환 참조 방지를 위해 NullLogger 사용 (로드 실패 시 기본값 반환되므로 안전)
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            artifactConfig = YamlConfigurationLoader.LoadFromFile(yamlPath, logger);
        }
        
        // ===== Analysis Options =====
        // Analysis 모듈이 필요로 하는 AnalysisOptions를 DI에 등록
        // YAML의 AnalysisOptions 섹션이 있으면 우선 적용, 없으면 appsettings.json 사용
        services.AddSingleton(sp =>
        {
            var yamlAnalysisOptions = artifactConfig?.AnalysisOptions;
            
            return new AndroidAdbAnalyze.Analysis.Models.Options.AnalysisOptions
            {
                MinConfidenceThreshold = yamlAnalysisOptions?.Thresholds?.MinConfidence ?? analysisConfig.MinConfidenceThreshold,
                EventCorrelationWindow = TimeSpan.FromSeconds(
                    yamlAnalysisOptions?.TimeWindows?.EventCorrelationSeconds ?? analysisConfig.EventCorrelationWindowSeconds),
                MaxSessionGap = TimeSpan.FromMinutes(
                    yamlAnalysisOptions?.TimeWindows?.MaxSessionGapMinutes ?? analysisConfig.MaxSessionGapMinutes),
                DeduplicationSimilarityThreshold = yamlAnalysisOptions?.Thresholds?.DeduplicationSimilarity ?? analysisConfig.DeduplicationSimilarityThreshold,
                SameCameraUsageTimeThreshold = TimeSpan.FromSeconds(
                    yamlAnalysisOptions?.TimeWindows?.SameCameraUsageTimeThresholdSeconds ?? analysisConfig.SameCameraUsageTimeThresholdSeconds),
                EnableIncompleteSessionHandling = true // 테스트 코드와 동일하게 명시적 설정
            };
        });
        
        // ===== Artifact Detection Config =====
        // YAML 설정이 있으면 DI에 등록 (Config 주입 생성자 사용을 위해)
        if (artifactConfig != null)
        {
            services.AddSingleton(artifactConfig);
        }
        
        // ===== Analysis Services =====
        // YAML 설정이 있으면 Config 주입 생성자 사용, 없으면 기본 생성자 사용
        if (artifactConfig != null)
        {
            RegisterAnalysisServicesWithConfig(services);
        }
        else
        {
        services.AddAndroidAdbAnalysis();
        }
        
        return services;
    }
    
    /// <summary>
    /// YAML Configuration을 주입하여 Analysis 서비스 등록
    /// </summary>
    /// <remarks>
    /// 테스트 코드의 RegisterServicesWithConfig()와 동일한 로직을 재사용합니다.
    /// YAML 설정이 있을 때만 호출되며, Config 주입 생성자를 사용합니다.
    /// </remarks>
    private static void RegisterAnalysisServicesWithConfig(IServiceCollection services)
    {
        // ===== Core Services =====
        
        // Session Context Provider
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ISessionContextProvider, 
            AndroidAdbAnalyze.Analysis.Services.Context.SessionContextProvider>();
        
        // Capture Detection Strategies (Configuration 주입) - 테스트 코드와 동일한 순서
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.TelegramStrategy>>();
            var calculator = sp.GetRequiredService<AndroidAdbAnalyze.Analysis.Interfaces.IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.TelegramStrategy(logger, calculator, config);
        });
        
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.KakaoTalkStrategy>>();
            var calculator = sp.GetRequiredService<AndroidAdbAnalyze.Analysis.Interfaces.IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.KakaoTalkStrategy(logger, calculator, config);
        });
        
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.BasePatternStrategy>>();
            var calculator = sp.GetRequiredService<AndroidAdbAnalyze.Analysis.Interfaces.IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.BasePatternStrategy(logger, calculator, config);
        });
        
        // Capture Detector
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ICaptureDetector, 
            AndroidAdbAnalyze.Analysis.Services.Captures.CameraCaptureDetector>();
        
        // Confidence Calculator (Configuration 주입) - 테스트 코드와 동일한 순서 (Strategies 이후)
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.IConfidenceCalculator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AndroidAdbAnalyze.Analysis.Services.Confidence.ConfidenceCalculator>>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new AndroidAdbAnalyze.Analysis.Services.Confidence.ConfidenceCalculator(logger, config);
        });
        
        // Session Sources
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ISessionSource, 
            AndroidAdbAnalyze.Analysis.Services.Sessions.Sources.UsagestatsSessionSource>();
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ISessionSource, 
            AndroidAdbAnalyze.Analysis.Services.Sessions.Sources.MediaCameraSessionSource>();
        
        // Session Detector
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ISessionDetector, 
            AndroidAdbAnalyze.Analysis.Services.Sessions.CameraSessionDetector>();
        
        // ===== Deduplication Services =====
        
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.IEventDeduplicator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AndroidAdbAnalyze.Analysis.Services.Deduplication.EventDeduplicator>>();
            var options = sp.GetRequiredService<AndroidAdbAnalyze.Analysis.Models.Options.AnalysisOptions>();
            return new AndroidAdbAnalyze.Analysis.Services.Deduplication.EventDeduplicator(logger, options);
        });
        
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.IDeduplicationStrategy, 
            AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies.TimeBasedDeduplicationStrategy>();
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.IDeduplicationStrategy, 
            AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies.CameraEventDeduplicationStrategy>();
        
        // ===== Transmission Detection Services =====
        
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ITransmissionDetector, 
            AndroidAdbAnalyze.Analysis.Services.Transmission.WifiTransmissionDetector>();
        
        // ===== Reporting Services =====
        
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.IReportGenerator, 
            AndroidAdbAnalyze.Analysis.Services.Reports.HtmlReportGenerator>();
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.ITimelineBuilder, 
            AndroidAdbAnalyze.Analysis.Services.Visualization.TimelineBuilder>();
        
        // ===== Orchestration =====
        
        services.AddSingleton<AndroidAdbAnalyze.Analysis.Interfaces.IAnalysisOrchestrator, 
            AndroidAdbAnalyze.Analysis.Services.Orchestration.AnalysisOrchestrator>();
    }
}

