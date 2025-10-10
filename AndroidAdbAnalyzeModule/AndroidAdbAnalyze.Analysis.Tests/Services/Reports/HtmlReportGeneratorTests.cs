using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Visualization;
using AndroidAdbAnalyze.Analysis.Services.Reports;
using AndroidAdbAnalyze.Parser.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Reports;

public sealed class HtmlReportGeneratorTests
{
    private readonly Mock<ITimelineBuilder> _mockTimelineBuilder;
    private readonly HtmlReportGenerator _generator;

    public HtmlReportGeneratorTests()
    {
        _mockTimelineBuilder = new Mock<ITimelineBuilder>();
        _generator = new HtmlReportGenerator(
            _mockTimelineBuilder.Object,
            NullLogger<HtmlReportGenerator>.Instance);
    }

    [Fact]
    public void Constructor_WithNullTimelineBuilder_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new HtmlReportGenerator(
            null!,
            NullLogger<HtmlReportGenerator>.Instance);
        
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("timelineBuilder");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new HtmlReportGenerator(
            _mockTimelineBuilder.Object,
            null!);
        
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Format_ReturnsHTML()
    {
        // Act
        var format = _generator.Format;

        // Assert
        format.Should().Be("HTML");
    }

    [Fact]
    public void GenerateReport_WithNullResult_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _generator.GenerateReport(null!);
        
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }

    [Fact]
    public void GenerateReport_WithEmptyResult_ReturnsValidHtml()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().NotBeNullOrEmpty();
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html lang=\"ko\">");
        html.Should().Contain("</html>");
        html.Should().Contain("모바일 로그 분석 보고서");
    }

    [Fact]
    public void GenerateReport_ContainsRequiredSections()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("보고서 정보"); // Metadata
        html.Should().Contain("Executive Summary"); // Summary
        html.Should().Contain("상세 통계"); // Statistics
        html.Should().Contain("부록"); // Appendix
        html.Should().Contain("AndroidAdbAnalyze"); // Footer
    }

    [Fact]
    public void GenerateReport_WithDeviceInfo_IncludesDeviceInfo()
    {
        // Arrange
        var result = CreateResultWithDeviceInfo();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("Samsung");
        html.Should().Contain("SM-G991N");
        html.Should().Contain("Android 13");
    }

    [Fact]
    public void GenerateReport_WithNullDeviceInfo_DoesNotIncludeDeviceInfo()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().NotContain("Samsung");
        html.Should().NotContain("SM-G991N");
    }

    [Fact]
    public void GenerateReport_WithSessions_IncludesSessionTable()
    {
        // Arrange
        var sessions = new[] { CreateSession() };
        var result = CreateResultWithSessions(sessions);
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("카메라 세션 분석");
        html.Should().Contain("com.sec.android.app.camera");
        html.Should().Contain("<table class=\"data-table\">");
    }

    [Fact]
    public void GenerateReport_WithoutSessions_DoesNotIncludeSessionTable()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().NotContain("카메라 세션 분석");
    }

    [Fact]
    public void GenerateReport_WithCaptures_IncludesCaptureTable()
    {
        // Arrange
        var captures = new[] { CreateCapture() };
        var result = CreateResultWithCaptures(captures);
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("촬영 이벤트 분석");
        html.Should().Contain("com.sec.android.app.camera");
        html.Should().Contain("/storage/DCIM/IMG_001.jpg");
    }

    [Fact]
    public void GenerateReport_WithoutCaptures_DoesNotIncludeCaptureTable()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().NotContain("촬영 이벤트 분석");
    }

    [Fact]
    public void GenerateReport_WithTimelineItems_IncludesTimelineChart()
    {
        // Arrange
        var result = CreateEmptyResult();
        var timelineItems = new[] { CreateTimelineItem() };
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(timelineItems);

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("타임라인 분석");
        html.Should().Contain("<canvas id=\"timelineChart\"></canvas>");
        html.Should().Contain("chart.js");
    }

    [Fact]
    public void GenerateReport_WithoutTimelineItems_DoesNotIncludeTimelineChart()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().NotContain("타임라인 분석");
    }

    [Fact]
    public void GenerateReport_WithErrors_IncludesErrorSection()
    {
        // Arrange
        var result = CreateResultWithErrors();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("에러");
        html.Should().Contain("Test error message");
        html.Should().Contain("alert alert-error");
    }

    [Fact]
    public void GenerateReport_WithWarnings_IncludesWarningSection()
    {
        // Arrange
        var result = CreateResultWithWarnings();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("경고");
        html.Should().Contain("Test warning message");
        html.Should().Contain("alert alert-warning");
    }

    [Fact]
    public void GenerateReport_WithoutErrorsOrWarnings_DoesNotIncludeErrorSection()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().NotContain("⚠️ 에러");
        html.Should().NotContain("⚠️ 경고");
    }

    [Fact]
    public void GenerateReport_IncludesStatistics()
    {
        // Arrange
        var result = CreateResultWithStatistics();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("총 처리 이벤트 수");
        html.Should().Contain("중복 제거된 이벤트 수");
        html.Should().Contain("총 카메라 세션 수");
        html.Should().Contain("총 촬영 이벤트 수");
    }

    [Fact]
    public void GenerateReport_EscapesHtmlSpecialCharacters()
    {
        // Arrange
        var result = CreateResultWithSpecialCharacters();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("&lt;"); // < escaped
        html.Should().Contain("&gt;"); // > escaped
        html.Should().Contain("&amp;"); // & escaped
    }

    [Fact]
    public void GenerateReport_IncludesReportNumber()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Match("*ADB-*-*"); // Report number pattern
    }

    [Fact]
    public void GenerateReport_IncludesProcessingTime()
    {
        // Arrange
        var result = CreateResultWithStatistics();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("처리 시간");
        html.Should().Contain("초");
    }

    [Fact]
    public void GenerateReport_IncludesConfidenceBars()
    {
        // Arrange
        var sessions = new[] { CreateSession() };
        var result = CreateResultWithSessions(sessions);
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("confidence-bar-container");
        html.Should().Contain("confidence-bar");
    }

    [Fact]
    public void GenerateReport_IncludesStatusBadges()
    {
        // Arrange
        var sessions = new[] { CreateSession(), CreateIncompleteSession() };
        var result = CreateResultWithSessions(sessions);
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("badge badge-success");
        html.Should().Contain("badge badge-warning");
    }

    [Fact]
    public void GenerateReport_CallsTimelineBuilder()
    {
        // Arrange
        var result = CreateEmptyResult();
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        _generator.GenerateReport(result);

        // Assert
        _mockTimelineBuilder.Verify(x => x.BuildTimeline(result), Times.Once);
    }

    [Fact]
    public void GenerateReport_WithPartialDeviceInfo_HandlesNullProperties()
    {
        // Arrange
        var result = new AnalysisResult
        {
            Success = true,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = 0,
                CompleteSessions = 0,
                IncompleteSessions = 0,
                TotalCaptureEvents = 0,
                ProcessingTime = TimeSpan.FromSeconds(1.234),
                AnalysisStartTime = DateTime.UtcNow,
                AnalysisEndTime = DateTime.UtcNow
            },
            DeviceInfo = new DeviceInfo
            {
                Manufacturer = null,  // null
                Model = "SM-G991N",
                AndroidVersion = "",  // empty
                TimeZone = "Asia/Seoul"
            }
        };
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("SM-G991N");  // Model은 포함
        html.Should().NotContain("디바이스 제조사");  // Manufacturer는 미포함
        html.Should().NotContain("Android 버전");  // AndroidVersion은 미포함
    }

    [Fact]
    public void GenerateReport_WithNullFilePath_DisplaysDash()
    {
        // Arrange
        var capture = new CameraCaptureEvent
        {
            CaptureId = Guid.NewGuid(),
            PackageName = "com.sec.android.app.camera",
            CaptureTime = DateTime.UtcNow.AddMinutes(-7),
            ConfidenceScore = 0.95,
            IsEstimated = false,
            PrimaryEvidenceId = Guid.NewGuid(),
            SupportingEvidenceIds = Array.Empty<Guid>(),
            FilePath = null,  // null FilePath
            FileUri = null
        };
        var result = CreateResultWithCaptures(new[] { capture });
        _mockTimelineBuilder.Setup(x => x.BuildTimeline(result))
            .Returns(Array.Empty<TimelineItem>());

        // Act
        var html = _generator.GenerateReport(result);

        // Assert
        html.Should().Contain("촬영 이벤트 분석");
        html.Should().Contain("com.sec.android.app.camera");
        // FilePath 컬럼에 "-" 표시 확인 (정확한 패턴 매칭)
        html.Should().MatchRegex(@"<td>\s*-\s*</td>");
    }

    // Helper Methods
    private AnalysisResult CreateEmptyResult()
    {
        return new AnalysisResult
        {
            Success = true,
            Sessions = Array.Empty<CameraSession>(),
            CaptureEvents = Array.Empty<CameraCaptureEvent>(),
            DeduplicationDetails = Array.Empty<DeduplicationInfo>(),
            SourceEvents = Array.Empty<NormalizedLogEvent>(),
            DeviceInfo = null,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = 0,
                CompleteSessions = 0,
                IncompleteSessions = 0,
                TotalCaptureEvents = 0,
                ProcessingTime = TimeSpan.FromSeconds(1.234),
                AnalysisStartTime = DateTime.UtcNow,
                AnalysisEndTime = DateTime.UtcNow
            },
            Errors = Array.Empty<string>(),
            Warnings = Array.Empty<string>()
        };
    }

    private AnalysisResult CreateResultWithDeviceInfo()
    {
        return new AnalysisResult
        {
            Success = true,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = 0,
                CompleteSessions = 0,
                IncompleteSessions = 0,
                TotalCaptureEvents = 0,
                ProcessingTime = TimeSpan.FromSeconds(1.234),
                AnalysisStartTime = DateTime.UtcNow,
                AnalysisEndTime = DateTime.UtcNow
            },
            DeviceInfo = new DeviceInfo
            {
                Manufacturer = "Samsung",
                Model = "SM-G991N",
                AndroidVersion = "13",
                TimeZone = "Asia/Seoul"
            }
        };
    }

    private AnalysisResult CreateResultWithSessions(CameraSession[] sessions)
    {
        return new AnalysisResult
        {
            Success = true,
            Sessions = sessions,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = sessions.Length,
                CompleteSessions = sessions.Count(s => !s.IsIncomplete),
                IncompleteSessions = sessions.Count(s => s.IsIncomplete),
                TotalCaptureEvents = 0,
                ProcessingTime = TimeSpan.FromSeconds(1.234),
                AnalysisStartTime = DateTime.UtcNow,
                AnalysisEndTime = DateTime.UtcNow
            }
        };
    }

    private AnalysisResult CreateResultWithCaptures(CameraCaptureEvent[] captures)
    {
        return new AnalysisResult
        {
            Success = true,
            CaptureEvents = captures,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = 0,
                CompleteSessions = 0,
                IncompleteSessions = 0,
                TotalCaptureEvents = captures.Length,
                ProcessingTime = TimeSpan.FromSeconds(1.234),
                AnalysisStartTime = DateTime.UtcNow,
                AnalysisEndTime = DateTime.UtcNow
            }
        };
    }

    private AnalysisResult CreateResultWithStatistics()
    {
        return new AnalysisResult
        {
            Success = true,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 1000,
                DeduplicatedEvents = 50,
                TotalSessions = 10,
                CompleteSessions = 8,
                IncompleteSessions = 2,
                TotalCaptureEvents = 25,
                ProcessingTime = TimeSpan.FromSeconds(2.5),
                AnalysisStartTime = DateTime.UtcNow.AddSeconds(-2.5),
                AnalysisEndTime = DateTime.UtcNow
            }
        };
    }

    private AnalysisResult CreateResultWithErrors()
    {
        return new AnalysisResult
        {
            Success = true,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = 0,
                CompleteSessions = 0,
                IncompleteSessions = 0,
                TotalCaptureEvents = 0,
                ProcessingTime = TimeSpan.FromSeconds(1.234),
                AnalysisStartTime = DateTime.UtcNow,
                AnalysisEndTime = DateTime.UtcNow
            },
            Errors = new[] { "Test error message" }
        };
    }

    private AnalysisResult CreateResultWithWarnings()
    {
        return new AnalysisResult
        {
            Success = true,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = 0,
                CompleteSessions = 0,
                IncompleteSessions = 0,
                TotalCaptureEvents = 0,
                ProcessingTime = TimeSpan.FromSeconds(1.234),
                AnalysisStartTime = DateTime.UtcNow,
                AnalysisEndTime = DateTime.UtcNow
            },
            Warnings = new[] { "Test warning message" }
        };
    }

    private AnalysisResult CreateResultWithSpecialCharacters()
    {
        return new AnalysisResult
        {
            Success = true,
            Statistics = new AnalysisStatistics
            {
                TotalSourceEvents = 0,
                DeduplicatedEvents = 0,
                TotalSessions = 0,
                CompleteSessions = 0,
                IncompleteSessions = 0,
                TotalCaptureEvents = 0,
                ProcessingTime = TimeSpan.FromSeconds(1.234),
                AnalysisStartTime = DateTime.UtcNow,
                AnalysisEndTime = DateTime.UtcNow
            },
            Errors = new[] { "<script>alert('XSS')</script>" },
            Warnings = new[] { "Test & warning <tag>" }
        };
    }

    private CameraSession CreateSession()
    {
        return new CameraSession
        {
            SessionId = Guid.NewGuid(),
            PackageName = "com.sec.android.app.camera",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            IncompleteReason = null,
            ConfidenceScore = 0.9,
            StartEventId = Guid.NewGuid(),
            EndEventId = Guid.NewGuid(),
            CaptureEventIds = Array.Empty<Guid>()
        };
    }

    private CameraSession CreateIncompleteSession()
    {
        return new CameraSession
        {
            SessionId = Guid.NewGuid(),
            PackageName = "com.sec.android.app.camera",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            EndTime = null,
            IncompleteReason = SessionIncompleteReason.MissingEnd,
            ConfidenceScore = 0.6,
            StartEventId = Guid.NewGuid(),
            EndEventId = null,
            CaptureEventIds = Array.Empty<Guid>()
        };
    }

    private CameraCaptureEvent CreateCapture()
    {
        return new CameraCaptureEvent
        {
            CaptureId = Guid.NewGuid(),
            PackageName = "com.sec.android.app.camera",
            CaptureTime = DateTime.UtcNow.AddMinutes(-7),
            ConfidenceScore = 0.95,
            IsEstimated = false,
            PrimaryEvidenceId = Guid.NewGuid(),
            SupportingEvidenceIds = Array.Empty<Guid>(),
            FilePath = "/storage/DCIM/IMG_001.jpg",
            FileUri = "content://media/external/images/1"
        };
    }

    private TimelineItem CreateTimelineItem()
    {
        return new TimelineItem
        {
            EventId = Guid.NewGuid(),
            EventType = Constants.TimelineEventTypes.CAMERA_CAPTURE,
            StartTime = DateTime.UtcNow.AddMinutes(-7),
            EndTime = null,
            PackageName = "com.sec.android.app.camera",
            Label = "촬영 #1",
            ConfidenceScore = 0.95,
            ColorHint = "green",
            Metadata = new Dictionary<string, string>()
        };
    }
}
