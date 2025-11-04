using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Captures;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Context;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Analysis.Services.Reports;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;
using AndroidAdbAnalyze.Analysis.Services.Transmission;
using AndroidAdbAnalyze.Analysis.Services.Visualization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Extensions;

/// <summary>
/// IServiceCollection 확장 메서드
/// </summary>
/// <remarks>
/// AndroidAdbAnalyze.Analysis 프로젝트의 모든 서비스를 DI 컨테이너에 등록합니다.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// AndroidAdbAnalyze.Analysis 서비스 등록
    /// </summary>
    /// <param name="services">DI 컨테이너</param>
    /// <returns>DI 컨테이너 (Fluent API)</returns>
    public static IServiceCollection AddAndroidAdbAnalysis(this IServiceCollection services)
    {
        // ===== Core Services =====
        
        // Session Context Provider (usagestats 베이스)
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies
        services.AddSingleton<ICaptureDetectionStrategy, TelegramStrategy>();    // 특화 전략
        services.AddSingleton<ICaptureDetectionStrategy, KakaoTalkStrategy>();   // 특화 전략
        services.AddSingleton<ICaptureDetectionStrategy, BasePatternStrategy>(); // 기본 전략 (fallback)
        
        // Capture Detector (Orchestrator)
        services.AddSingleton<ICaptureDetector, CameraCaptureDetector>();
        
        // Confidence Calculator
        services.AddSingleton<IConfidenceCalculator, ConfidenceCalculator>();
        
        // Session Sources (Priority 순으로 자동 정렬됨)
        services.AddSingleton<ISessionSource, UsagestatsSessionSource>();     // Priority: 100 (Primary)
        services.AddSingleton<ISessionSource, MediaCameraSessionSource>();    // Priority: 50 (Secondary)
        
        // Session Detector
        services.AddSingleton<ISessionDetector, CameraSessionDetector>();
        
        // ===== Deduplication Services =====
        
        // Event Deduplicator (AnalysisOptions 의존성 주입)
        services.AddSingleton<IEventDeduplicator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventDeduplicator>>();
            var options = sp.GetRequiredService<AnalysisOptions>();
            return new EventDeduplicator(logger, options);
        });
        
        // Deduplication Strategies
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        // ===== Transmission Detection Services =====
        
        // Transmission Detector (선택적 기능, AnalysisOptions.EnableTransmissionDetection으로 제어)
        services.AddSingleton<ITransmissionDetector, WifiTransmissionDetector>();
        
        // ===== Reporting Services =====
        
        // Report Generator
        services.AddSingleton<IReportGenerator, HtmlReportGenerator>();
        
        // Timeline Builder
        services.AddSingleton<ITimelineBuilder, TimelineBuilder>();
        
        // ===== Orchestration =====
        
        // Analysis Orchestrator (전체 분석 흐름 조율)
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
        
        return services;
    }
}
