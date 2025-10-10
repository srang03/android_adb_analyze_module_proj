using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Sessions;

/// <summary>
/// MediaCameraSessionSource 단위 테스트
/// </summary>
/// <remarks>
/// media_camera 로그에서 CAMERA_CONNECT → CAMERA_DISCONNECT 패턴 처리를 테스트합니다.
/// </remarks>
public sealed class MediaCameraSessionSourceTests
{
    private readonly MediaCameraSessionSource _source;
    private readonly AnalysisOptions _defaultOptions;

    public MediaCameraSessionSourceTests()
    {
        var confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        _source = new MediaCameraSessionSource(
            NullLogger<MediaCameraSessionSource>.Instance,
            confidenceCalculator);

        _defaultOptions = new AnalysisOptions
        {
            MinConfidenceThreshold = 0.0
        };
    }

    [Fact]
    public void Priority_ReturnsCorrectValue()
    {
        // Act
        var priority = _source.Priority;

        // Assert
        priority.Should().Be(50, "MediaCameraSessionSource는 Secondary 소스로 Priority 50을 가져야 합니다");
    }

    [Fact]
    public void SourceName_ReturnsCorrectValue()
    {
        // Act
        var sourceName = _source.SourceName;

        // Assert
        sourceName.Should().Be("media_camera");
    }

    [Fact]
    public void ExtractSessions_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var events = Array.Empty<NormalizedLogEvent>();

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractSessions_CompleteSession_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.telegram.messenger")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().Be(baseTime.AddSeconds(10));
        session.PackageName.Should().Be("com.telegram.messenger");
        session.IsIncomplete.Should().BeFalse();
        session.IncompleteReason.Should().BeNull();
        session.Duration.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ExtractSessions_IncompleteSession_MissingEnd_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().BeNull();
        session.PackageName.Should().Be("com.telegram.messenger");
        session.IsIncomplete.Should().BeTrue();
        session.IncompleteReason.Should().Be(SessionIncompleteReason.MissingEnd);
    }

    [Fact]
    public void ExtractSessions_IncompleteSession_MissingStart_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime, "com.telegram.messenger")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().Be(baseTime, "MissingStart 세션은 DISCONNECT 이벤트로 시작/종료를 모두 설정합니다");
        session.PackageName.Should().Be("com.telegram.messenger");
        // MissingStart 세션은 EndTime != null이므로 IsIncomplete = false
        session.IsIncomplete.Should().BeFalse("EndTime이 null이 아니므로");
        session.IncompleteReason.Should().Be(SessionIncompleteReason.MissingStart, "IncompleteReason으로 불완전 상태를 표현합니다");
    }

    [Fact]
    public void ExtractSessions_MultipleSessions_SamePackage_DetectsAll()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 첫 번째 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(5), "com.telegram.messenger"),
            // 두 번째 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(10), "com.telegram.messenger"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(15), "com.telegram.messenger")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(2);
        result[0].StartTime.Should().Be(baseTime);
        result[0].EndTime.Should().Be(baseTime.AddSeconds(5));
        result[1].StartTime.Should().Be(baseTime.AddSeconds(10));
        result[1].EndTime.Should().Be(baseTime.AddSeconds(15));
    }

    [Fact]
    public void ExtractSessions_MultipleSessions_DifferentPackages_DetectsAll()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // Telegram 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(5), "com.telegram.messenger"),
            // Instagram 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(10), "com.instagram.android"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(15), "com.instagram.android")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.PackageName == "com.telegram.messenger");
        result.Should().Contain(s => s.PackageName == "com.instagram.android");
    }

    [Fact]
    public void ExtractSessions_NestedSessions_HandlesCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger"),
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(5), "com.telegram.messenger"), // 중첩
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.telegram.messenger")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(2);
        
        // 첫 번째 세션: 불완전 (MissingEnd)
        result[0].StartTime.Should().Be(baseTime);
        result[0].EndTime.Should().BeNull();
        result[0].IsIncomplete.Should().BeTrue();
        result[0].IncompleteReason.Should().Be(SessionIncompleteReason.MissingEnd);
        
        // 두 번째 세션: 완전
        result[1].StartTime.Should().Be(baseTime.AddSeconds(5));
        result[1].EndTime.Should().Be(baseTime.AddSeconds(10));
        result[1].IsIncomplete.Should().BeFalse();
    }

    [Fact]
    public void ExtractSessions_ProcessId_ExtractsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEventWithPid(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger", 12345),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.telegram.messenger")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.ProcessId.Should().Be(12345);
    }

    [Fact]
    public void ExtractSessions_SourceLogTypes_CollectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEventWithSource(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger", "media_camera"),
            CreateEventWithSource(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.telegram.messenger", "media_camera")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.SourceLogTypes.Should().Contain("media_camera");
    }

    [Fact]
    public void ExtractSessions_NoPackageAttribute_IgnoresEvent()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEventWithoutPackage(LogEventTypes.CAMERA_CONNECT, baseTime)
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().BeEmpty("package attribute가 없는 이벤트는 무시되어야 합니다");
    }

    [Fact]
    public void ExtractSessions_SessionWithMiddleEvents_IncludesAllEvents()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger"),
            CreateEvent("OTHER_EVENT", baseTime.AddSeconds(2), "com.telegram.messenger"),
            CreateEvent("ANOTHER_EVENT", baseTime.AddSeconds(5), "com.telegram.messenger"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.telegram.messenger")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.SourceEventIds.Should().HaveCount(4, "세션 내 모든 이벤트가 포함되어야 합니다");
    }

    [Fact]
    public void ExtractSessions_EventsOutsideSession_NotIncluded()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent("OTHER_EVENT_BEFORE", baseTime.AddSeconds(-5), "com.telegram.messenger"), // 세션 외부
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.telegram.messenger"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.telegram.messenger"),
            CreateEvent("OTHER_EVENT_AFTER", baseTime.AddSeconds(15), "com.telegram.messenger") // 세션 외부
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.SourceEventIds.Should().HaveCount(2, "세션 시작~종료 사이의 이벤트만 포함되어야 합니다");
    }

    // Helper methods
    private NormalizedLogEvent CreateEvent(
        string eventType,
        DateTime timestamp,
        string packageName)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = "media_camera",
            Attributes = new Dictionary<string, object>
            {
                ["package"] = packageName
            }
        };
    }

    private NormalizedLogEvent CreateEventWithPid(
        string eventType,
        DateTime timestamp,
        string packageName,
        int pid)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = "media_camera",
            Attributes = new Dictionary<string, object>
            {
                ["package"] = packageName,
                ["pid"] = pid
            }
        };
    }

    private NormalizedLogEvent CreateEventWithSource(
        string eventType,
        DateTime timestamp,
        string packageName,
        string sourceSection)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = sourceSection,
            Attributes = new Dictionary<string, object>
            {
                ["package"] = packageName
            }
        };
    }

    private NormalizedLogEvent CreateEventWithoutPackage(
        string eventType,
        DateTime timestamp)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = "media_camera",
            Attributes = new Dictionary<string, object>()
        };
    }
}

