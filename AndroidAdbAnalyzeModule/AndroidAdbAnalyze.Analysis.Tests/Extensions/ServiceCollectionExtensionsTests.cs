using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AndroidAdbAnalyze.Analysis.Tests.Extensions;

/// <summary>
/// ServiceCollectionExtensions 단위 테스트
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAndroidAdbAnalysis_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        
        // AnalysisOptions 등록 (EventDeduplicator 의존성)
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });

        // Act
        services.AddAndroidAdbAnalysis();
        var serviceProvider = services.BuildServiceProvider();

        // Assert - 모든 핵심 서비스가 등록되었는지 확인
        serviceProvider.GetService<ISessionContextProvider>().Should().NotBeNull("SessionContextProvider 등록됨");
        serviceProvider.GetService<ICaptureDetector>().Should().NotBeNull("CaptureDetector 등록됨");
        serviceProvider.GetService<IConfidenceCalculator>().Should().NotBeNull("ConfidenceCalculator 등록됨");
        serviceProvider.GetService<ISessionDetector>().Should().NotBeNull("SessionDetector 등록됨");
        serviceProvider.GetService<IEventDeduplicator>().Should().NotBeNull("EventDeduplicator 등록됨");
        serviceProvider.GetService<IReportGenerator>().Should().NotBeNull("ReportGenerator 등록됨");
        serviceProvider.GetService<ITimelineBuilder>().Should().NotBeNull("TimelineBuilder 등록됨");
        serviceProvider.GetService<IAnalysisOrchestrator>().Should().NotBeNull("AnalysisOrchestrator 등록됨");
    }

    [Fact]
    public void AddAndroidAdbAnalysis_ResolvesCameraCaptureDetector()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddAndroidAdbAnalysis();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var captureDetector = serviceProvider.GetService<ICaptureDetector>();

        // Assert
        captureDetector.Should().NotBeNull();
        captureDetector.Should().BeOfType<Analysis.Services.Captures.CameraCaptureDetector>();
    }

    [Fact]
    public void AddAndroidAdbAnalysis_ResolvesMultipleStrategies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddAndroidAdbAnalysis();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var strategies = serviceProvider.GetServices<ICaptureDetectionStrategy>().ToList();

        // Assert
        strategies.Should().HaveCountGreaterThanOrEqualTo(2, "최소 BaseStrategy + TelegramStrategy 등록");
        strategies.Should().Contain(s => s.GetType().Name == "BasePatternStrategy");
        strategies.Should().Contain(s => s.GetType().Name == "TelegramStrategy");
    }

    [Fact]
    public void AddAndroidAdbAnalysis_ResolvesAnalysisOrchestrator()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        
        // AnalysisOptions 등록 (EventDeduplicator 의존성)
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });
        
        services.AddAndroidAdbAnalysis();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var orchestrator = serviceProvider.GetService<IAnalysisOrchestrator>();

        // Assert
        orchestrator.Should().NotBeNull();
        orchestrator.Should().BeOfType<Analysis.Services.Orchestration.AnalysisOrchestrator>();
    }

    [Fact]
    public void AddAndroidAdbAnalysis_VerifiesSingletonLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        
        // AnalysisOptions 등록 (EventDeduplicator 의존성)
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });
        
        services.AddAndroidAdbAnalysis();
        var serviceProvider = services.BuildServiceProvider();

        // Act - 동일한 인스턴스가 반환되는지 확인
        var orchestrator1 = serviceProvider.GetService<IAnalysisOrchestrator>();
        var orchestrator2 = serviceProvider.GetService<IAnalysisOrchestrator>();
        
        var captureDetector1 = serviceProvider.GetService<ICaptureDetector>();
        var captureDetector2 = serviceProvider.GetService<ICaptureDetector>();

        // Assert
        orchestrator1.Should().BeSameAs(orchestrator2, "Singleton 생명주기");
        captureDetector1.Should().BeSameAs(captureDetector2, "Singleton 생명주기");
    }
}
