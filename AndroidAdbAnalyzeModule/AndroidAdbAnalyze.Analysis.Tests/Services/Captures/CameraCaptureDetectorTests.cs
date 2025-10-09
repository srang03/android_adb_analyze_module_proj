using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Captures;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Context;
using AndroidAdbAnalyze.Analysis.Services.Strategies;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Captures;

/// <summary>
/// CameraCaptureDetector 단위 테스트 (Strategy Pattern 기반)
/// </summary>
/// <remarks>
/// Phase 4 리팩토링 이후: CameraCaptureDetector는 Orchestrator 역할만 수행
/// - SessionContext 생성 요청
/// - 적절한 Strategy 선택
/// - Strategy로 탐지 위임
/// </remarks>
public sealed class CameraCaptureDetectorTests
{
    private readonly CameraCaptureDetector _detector;
    private readonly Mock<ISessionContextProvider> _mockContextProvider;
    private readonly IConfidenceCalculator _confidenceCalculator;
    private readonly List<ICaptureDetectionStrategy> _strategies;
    private readonly AnalysisOptions _defaultOptions;

    public CameraCaptureDetectorTests()
    {
        _mockContextProvider = new Mock<ISessionContextProvider>();
        _confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        
        // BaseStrategy와 TelegramStrategy 등록
        _strategies = new List<ICaptureDetectionStrategy>
        {
            new BasePatternStrategy(NullLogger<BasePatternStrategy>.Instance, _confidenceCalculator),
            new TelegramStrategy(NullLogger<TelegramStrategy>.Instance, _confidenceCalculator)
        };

        _detector = new CameraCaptureDetector(
            NullLogger<CameraCaptureDetector>.Instance,
            _mockContextProvider.Object,
            _strategies);

        _defaultOptions = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MinConfidenceThreshold = 0.3,
            ScreenshotPathPatterns = new[] { "/Screenshots/" },
            DownloadPathPatterns = new[] { "/Download/" }
        };
    }

    [Fact]
    public void DetectCaptures_SelectsBaseStrategy_ForDefaultCamera()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var session = CreateSession(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera");
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(30), "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" })
        };

        var context = CreateContext(session, events);
        _mockContextProvider
            .Setup(x => x.CreateContext(session, events))
            .Returns(context);

        // Act
        var captures = _detector.DetectCaptures(session, events, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "BaseStrategy가 선택되어 DATABASE_INSERT 탐지");
        _mockContextProvider.Verify(x => x.CreateContext(session, events), Times.Once);
    }

    [Fact]
    public void DetectCaptures_SelectsTelegramStrategy_ForTelegram()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var session = CreateSession(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger");
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };

        var context = CreateContext(session, events);
        _mockContextProvider
            .Setup(x => x.CreateContext(session, events))
            .Returns(context);

        // Telegram 전용 옵션 (VIBRATION_EVENT 가중치 0.15 고려)
        var telegramOptions = new AnalysisOptions
        {
            EventCorrelationWindow = _defaultOptions.EventCorrelationWindow,
            MinConfidenceThreshold = 0.15, // VIBRATION_EVENT (0.15 가중치)만으로 탐지 가능
            ScreenshotPathPatterns = _defaultOptions.ScreenshotPathPatterns,
            DownloadPathPatterns = _defaultOptions.DownloadPathPatterns
        };

        // Act
        var captures = _detector.DetectCaptures(session, events, telegramOptions);

        // Assert
        captures.Should().HaveCount(1, "TelegramStrategy가 선택되어 VIBRATION_EVENT 탐지");
        captures[0].Metadata.Should().ContainKey("detection_strategy");
        captures[0].Metadata["detection_strategy"].Should().Be("TelegramStrategy");
    }

    [Fact]
    public void DetectCaptures_UsesDefaultStrategy_WhenNoMatch()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 48, 0);
        var session = CreateSession(baseTime, baseTime.AddMinutes(1), "com.unknown.app");
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(30), "com.unknown.app",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" })
        };

        var context = CreateContext(session, events);
        _mockContextProvider
            .Setup(x => x.CreateContext(session, events))
            .Returns(context);

        // Act
        var captures = _detector.DetectCaptures(session, events, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "기본 전략(BaseStrategy)이 fallback으로 사용됨");
    }

    [Fact]
    public void DetectCaptures_CreatesSessionContext()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var session = CreateSession(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera");
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(30), "com.sec.android.app.camera")
        };

        var context = CreateContext(session, events);
        _mockContextProvider
            .Setup(x => x.CreateContext(session, events))
            .Returns(context);

        // Act
        _detector.DetectCaptures(session, events, _defaultOptions);

        // Assert
        _mockContextProvider.Verify(
            x => x.CreateContext(session, events), 
            Times.Once, 
            "SessionContext가 생성되어야 함");
    }

    [Fact]
    public void DetectCaptures_DelegatesToStrategy()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var session = CreateSession(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera");
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(30), "com.sec.android.app.camera"),
            CreateEvent(LogEventTypes.MEDIA_INSERT_END, baseTime.AddMinutes(2), "com.sec.android.app.camera")
        };

        var context = CreateContext(session, events);
        _mockContextProvider
            .Setup(x => x.CreateContext(session, events))
            .Returns(context);

        // Act
        var captures = _detector.DetectCaptures(session, events, _defaultOptions);

        // Assert
        captures.Should().HaveCount(2, "Strategy가 2개의 주 증거를 탐지");
    }

    [Fact]
    public void DetectCaptures_EmptyEvents_ReturnsEmpty()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var session = CreateSession(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera");
        var events = Array.Empty<NormalizedLogEvent>();

        // Act
        var captures = _detector.DetectCaptures(session, events, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("이벤트가 없으므로 탐지 불가");
        _mockContextProvider.Verify(x => x.CreateContext(It.IsAny<CameraSession>(), It.IsAny<IReadOnlyList<NormalizedLogEvent>>()), Times.Never);
    }

    [Fact]
    public void DetectCaptures_NullSession_ThrowsException()
    {
        // Arrange
        var events = new List<NormalizedLogEvent>();

        // Act
        var act = () => _detector.DetectCaptures(null!, events, _defaultOptions);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("session");
    }

    [Fact]
    public void Constructor_NoDefaultStrategy_ThrowsException()
    {
        // Arrange: TelegramStrategy만 등록 (PackageNamePattern = "org.telegram.messenger")
        var strategiesWithoutDefault = new List<ICaptureDetectionStrategy>
        {
            new TelegramStrategy(NullLogger<TelegramStrategy>.Instance, _confidenceCalculator)
        };

        var mockContextProvider = new Mock<ISessionContextProvider>();
        var detector = new CameraCaptureDetector(
            NullLogger<CameraCaptureDetector>.Instance,
            mockContextProvider.Object,
            strategiesWithoutDefault);

        var session = CreateSession(DateTime.Now, DateTime.Now.AddMinutes(1), "com.unknown.app");
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, DateTime.Now, "com.unknown.app")
        };

        var context = CreateContext(session, events);
        mockContextProvider
            .Setup(x => x.CreateContext(session, events))
            .Returns(context);

        // Act
        var act = () => detector.DetectCaptures(session, events, _defaultOptions);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*기본 Strategy*등록되지 않았습니다*");
    }

    #region Helper Methods

    private CameraSession CreateSession(DateTime startTime, DateTime? endTime, string packageName)
    {
        return new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = startTime,
            EndTime = endTime,
            PackageName = packageName,
            SourceLogTypes = new[] { "media.camera" },
            ConfidenceScore = 0.8,
            SourceEventIds = Array.Empty<Guid>()
        };
    }

    private SessionContext CreateContext(CameraSession session, IReadOnlyList<NormalizedLogEvent> events)
    {
        return new SessionContext
        {
            Session = session,
            AllEvents = events,
            ActivityResumedTime = null,
            ActivityPausedTime = null,
            ForegroundServices = Array.Empty<ForegroundServiceInfo>(),
            TimelineEvents = new Dictionary<DateTime, List<NormalizedLogEvent>>()
        };
    }

    private NormalizedLogEvent CreateEvent(
        string eventType,
        DateTime timestamp,
        string packageName,
        Dictionary<string, object>? attributes = null)
    {
        var attrs = new Dictionary<string, object> { ["package"] = packageName };
        if (attributes != null)
        {
            foreach (var attr in attributes)
                attrs[attr.Key] = attr.Value;
        }

        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = "test_section",
            SourceFileName = "test.log",
            PackageName = packageName,
            Attributes = attrs,
            RawLine = $"Test log line for {eventType}"
        };
    }

    #endregion
}