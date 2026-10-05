using System.Reflection;
using System.Text.RegularExpressions;
using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Visualization;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Analysis.Services.Reports;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AndroidAdbAnalyze.Analysis.Tests.Services;

/// <summary>
/// 코드 리뷰에서 확인된 결함의 회귀 테스트
/// </summary>
public sealed class AnalysisRegressionTests
{
    private delegate void DeduplicateCallback(
        IReadOnlyList<NormalizedLogEvent> events,
        out IReadOnlyList<DeduplicationInfo> details);

    [Fact]
    public void AddAndroidAdbAnalysis_WithoutAnalysisOptions_ResolvesOrchestrator()
    {
        // Arrange: AnalysisOptions를 등록하지 않은 라이브러리 사용자
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));

        // Act
        services.AddAndroidAdbAnalysis();
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<IAnalysisOrchestrator>().Should().NotBeNull();
    }

    [Fact]
    public void AddAndroidAdbAnalysis_WithRegisteredAnalysisOptions_KeepsCallerOptions()
    {
        // Arrange
        var options = new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 };
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton(options);

        // Act
        services.AddAndroidAdbAnalysis();
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetRequiredService<AnalysisOptions>().Should().BeSameAs(options);
        services.Count(d => d.ServiceType == typeof(AnalysisOptions)).Should().Be(1);
    }

    [Theory]
    [InlineData(1, 5, 5)]   // 평균 1분 → 최소값 5분
    [InlineData(3, 10, 5)]  // 평균 3분 → 최소값 5분
    [InlineData(7, 10, 7)]  // 평균 7분 → 그대로
    [InlineData(20, 10, 10)] // 평균 20분 → 설정값 10분
    [InlineData(1, 2, 2)]   // 설정값(2분) < 최소값(5분) → 설정값 (이전에는 ArgumentException)
    public void CalculateDynamicMaxSessionGap_ShouldNotThrow_AndRespectConfiguredMax(
        int averageMinutes, int configuredMaxMinutes, int expectedMinutes)
    {
        // Arrange: private 메서드 (불완전 세션 처리 중 다음 세션이 있을 때만 호출됨)
        var detector = new CameraSessionDetector(
            NullLogger<CameraSessionDetector>.Instance,
            Mock.Of<IConfidenceCalculator>(),
            new[] { Mock.Of<ISessionSource>() });
        var method = typeof(CameraSessionDetector).GetMethod(
            "CalculateDynamicMaxSessionGap", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Act
        var result = (TimeSpan)method.Invoke(detector, new object[]
        {
            TimeSpan.FromMinutes(averageMinutes),
            TimeSpan.FromMinutes(configuredMaxMinutes)
        })!;

        // Assert
        result.Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldPreserveCameraDeviceIds_OfDetectedSessions()
    {
        // Arrange
        var events = new List<NormalizedLogEvent>
        {
            new() { EventId = Guid.NewGuid(), Timestamp = DateTime.UtcNow, EventType = LogEventTypes.CAMERA_CONNECT, SourceSection = "test" }
        };
        var session = new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow,
            PackageName = "com.sec.android.app.camera",
            SourceLogTypes = new[] { "media_camera" },
            CaptureEventIds = Array.Empty<Guid>(),
            SourceEventIds = Array.Empty<Guid>(),
            SessionCompletenessScore = 1.0,
            CameraDeviceIds = new[] { 0, 1 }
        };

        var deduplicator = new Mock<IEventDeduplicator>();
        deduplicator
            .Setup(x => x.Deduplicate(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), out It.Ref<IReadOnlyList<DeduplicationInfo>>.IsAny))
            .Callback(new DeduplicateCallback((IReadOnlyList<NormalizedLogEvent> _, out IReadOnlyList<DeduplicationInfo> details) =>
            {
                details = Array.Empty<DeduplicationInfo>();
            }))
            .Returns((IReadOnlyList<NormalizedLogEvent> e, IReadOnlyList<DeduplicationInfo> _) => e);
        var sessionDetector = new Mock<ISessionDetector>();
        sessionDetector
            .Setup(x => x.DetectSessions(It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
            .Returns(new[] { session });
        var captureDetector = new Mock<ICaptureDetector>();
        captureDetector
            .Setup(x => x.DetectCaptures(It.IsAny<CameraSession>(), It.IsAny<IReadOnlyList<NormalizedLogEvent>>(), It.IsAny<AnalysisOptions>()))
            .Returns(Array.Empty<CameraCaptureEvent>());

        var orchestrator = new AnalysisOrchestrator(
            deduplicator.Object,
            sessionDetector.Object,
            captureDetector.Object,
            NullLogger<AnalysisOrchestrator>.Instance);

        // Act
        var result = await orchestrator.AnalyzeAsync(events);

        // Assert
        result.Success.Should().BeTrue();
        result.Sessions.Should().ContainSingle()
            .Which.CameraDeviceIds.Should().Equal(0, 1);
    }

    [Fact]
    public void Deduplicate_CalledConcurrentlyOnSharedInstance_ShouldNotThrow()
    {
        // Arrange: Singleton으로 등록되는 EventDeduplicator를 여러 스레드에서 동시에 사용
        var eventTypes = new[]
        {
            LogEventTypes.DATABASE_INSERT, LogEventTypes.PLAYER_CREATED, LogEventTypes.PLAYER_EVENT,
            LogEventTypes.PLAYER_RELEASED, LogEventTypes.MEDIA_EXTRACTOR,
            LogEventTypes.URI_PERMISSION_GRANT, LogEventTypes.URI_PERMISSION_REVOKE
        };
        var baseTime = new DateTime(2025, 9, 1, 10, 0, 0);
        var events = Enumerable.Range(0, eventTypes.Length * 2)
            .Select(i => new NormalizedLogEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = baseTime.AddSeconds(i),
                EventType = eventTypes[i % eventTypes.Length],
                SourceSection = "test",
                Attributes = new Dictionary<string, object> { ["package"] = "com.example" }
            })
            .ToList();

        const int threadCount = 8;
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act: 매 반복마다 새 인스턴스(빈 캐시)에 여러 스레드가 동시에 진입
        for (int iteration = 0; iteration < 2000; iteration++)
        {
            var deduplicator = new EventDeduplicator(NullLogger<EventDeduplicator>.Instance, new AnalysisOptions());
            using var barrier = new Barrier(threadCount);
            var threads = Enumerable.Range(0, threadCount)
                .Select(_ => new Thread(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        deduplicator.Deduplicate(events, out var details);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }))
                .ToList();
            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());
        }

        // Assert
        exceptions.Should().BeEmpty();
    }

    [Fact]
    public void GenerateReport_ChartOptions_ShouldDefinePluginsOnce()
    {
        // Arrange: JS 객체 리터럴에서 같은 키가 두 번 있으면 나중 값이 앞의 값을 덮어씀
        var timelineBuilder = new Mock<ITimelineBuilder>();
        timelineBuilder
            .Setup(x => x.BuildTimeline(It.IsAny<AnalysisResult>()))
            .Returns(Array.Empty<TimelineItem>());
        var generator = new HtmlReportGenerator(timelineBuilder.Object, NullLogger<HtmlReportGenerator>.Instance);
        var result = new AnalysisResult
        {
            Success = true,
            Sessions = Array.Empty<CameraSession>(),
            CaptureEvents = Array.Empty<CameraCaptureEvent>(),
            DeduplicationDetails = Array.Empty<DeduplicationInfo>(),
            SourceEvents = Array.Empty<NormalizedLogEvent>(),
            Statistics = new AnalysisStatistics(),
            Errors = Array.Empty<string>(),
            Warnings = Array.Empty<string>()
        };

        // Act
        var html = generator.GenerateReport(result);

        // Assert
        var chartOptions = html.Substring(html.IndexOf("timelineChart = new Chart(", StringComparison.Ordinal));
        chartOptions = chartOptions.Substring(0, chartOptions.IndexOf("});", StringComparison.Ordinal));
        Regex.Matches(chartOptions, @"\bplugins\s*:").Should().HaveCount(1);
        chartOptions.Should().Contain("legend: { display: false }");
        chartOptions.Should().Contain("tooltip: {");
        chartOptions.Should().Contain("zoom: {");
    }
}
