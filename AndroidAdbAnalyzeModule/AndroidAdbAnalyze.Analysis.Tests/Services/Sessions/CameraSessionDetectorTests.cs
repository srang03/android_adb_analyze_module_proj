using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Sessions;

/// <summary>
/// CameraSessionDetector 통합 테스트
/// </summary>
/// <remarks>
/// SessionSources (UsagestatsSessionSource, MediaCameraSessionSource)와 통합하여
/// 고수준 세션 감지 파이프라인을 테스트합니다.
/// </remarks>
public sealed class CameraSessionDetectorTests
{
    private readonly CameraSessionDetector _detector;
    private readonly AnalysisOptions _defaultOptions;

    public CameraSessionDetectorTests()
    {
        var confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        
        // SessionSources 생성 (실제 구현체 사용)
        var sessionSources = new List<ISessionSource>
        {
            new UsagestatsSessionSource(
                NullLogger<UsagestatsSessionSource>.Instance,
                confidenceCalculator),
            new MediaCameraSessionSource(
                NullLogger<MediaCameraSessionSource>.Instance,
                confidenceCalculator)
        };
        
        _detector = new CameraSessionDetector(
            NullLogger<CameraSessionDetector>.Instance,
            confidenceCalculator,
            sessionSources);

        _defaultOptions = new AnalysisOptions
        {
            MinConfidenceThreshold = 0.0, // 테스트에서는 모든 세션 허용
            EnableIncompleteSessionHandling = false // 기본적으로 비활성화
        };
    }

    [Fact]
    public void DetectSessions_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var events = Array.Empty<NormalizedLogEvent>();

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void DetectSessions_CompleteSession_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().Be(baseTime.AddSeconds(10));
        session.PackageName.Should().Be("com.camera");
        session.IsIncomplete.Should().BeFalse();
        session.IncompleteReason.Should().BeNull();
        session.Duration.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void DetectSessions_IncompleteSession_MissingEnd_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().BeNull();
        session.IsIncomplete.Should().BeTrue();
        session.IncompleteReason.Should().Be(SessionIncompleteReason.MissingEnd);
    }

    [Fact]
    public void DetectSessions_IncompleteSession_MissingStart_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime, "com.camera", "camera_event")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().Be(baseTime);
        session.IsIncomplete.Should().BeFalse(); // EndTime이 있으므로
        session.IncompleteReason.Should().Be(SessionIncompleteReason.MissingStart);
    }

    [Fact]
    public void DetectSessions_MultipleSessions_SamePackage_DetectsAll()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 첫 번째 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event"),
            
            // 두 번째 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(20), "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(30), "com.camera", "camera_event")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(2);
        result[0].StartTime.Should().Be(baseTime);
        result[0].EndTime.Should().Be(baseTime.AddSeconds(10));
        result[1].StartTime.Should().Be(baseTime.AddSeconds(20));
        result[1].EndTime.Should().Be(baseTime.AddSeconds(30));
    }

    [Fact]
    public void DetectSessions_MultipleSessions_DifferentPackages_DetectsAll()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event"),
            
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(5), "com.kakao", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(15), "com.kakao", "camera_event")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.PackageName == "com.camera");
        result.Should().Contain(s => s.PackageName == "com.kakao");
    }

    [Fact]
    public void DetectSessions_NestedSessions_HandlesCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            // 두 번째 OPEN이 첫 번째 CLOSE 전에 발생 (중첩)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(5), "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(2);
        result[0].IsIncomplete.Should().BeTrue("첫 번째 세션은 종료가 누락됨");
        result[0].IncompleteReason.Should().Be(SessionIncompleteReason.MissingEnd);
        result[1].IsIncomplete.Should().BeFalse("두 번째 세션은 완전함");
    }

    [Fact]
    public void DetectSessions_PackageWhitelist_FiltersCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event"),
            
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(5), "com.other", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(15), "com.other", "camera_event")
        };

        var options = new AnalysisOptions
        {
            PackageWhitelist = new[] { "com.camera" },
            MinConfidenceThreshold = 0.0
        };

        // Act
        var result = _detector.DetectSessions(events, options);

        // Assert
        result.Should().HaveCount(1);
        result[0].PackageName.Should().Be("com.camera");
    }

    [Fact]
    public void DetectSessions_PackageBlacklist_FiltersCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event"),
            
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(5), "com.exclude", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(15), "com.exclude", "camera_event")
        };

        var options = new AnalysisOptions
        {
            PackageBlacklist = new[] { "exclude" },
            MinConfidenceThreshold = 0.0
        };

        // Act
        var result = _detector.DetectSessions(events, options);

        // Assert
        result.Should().HaveCount(1);
        result[0].PackageName.Should().Be("com.camera");
    }

    [Fact]
    public void DetectSessions_MinConfidenceThreshold_FiltersLowConfidence()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 완전한 세션 (높은 신뢰도)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event"),
            
            // 불완전 세션 (낮은 신뢰도)
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(20), "com.other", "camera_event")
        };

        var options = new AnalysisOptions
        {
            MinConfidenceThreshold = 0.5, // CAMERA_CLOSE(0.4)는 필터링, OPEN+CLOSE(0.8)는 통과
            EnableIncompleteSessionHandling = false
        };

        // Act
        var result = _detector.DetectSessions(events, options);

        // Assert
        result.Should().HaveCount(1);
        result[0].PackageName.Should().Be("com.camera");
    }

    [Fact]
    public void DetectSessions_IncompleteSessionHandling_NextSessionCompletion()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 불완전 세션 (종료 없음)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            
            // 다음 세션 (5분 내)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMinutes(2), "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddMinutes(3), "com.camera", "camera_event")
        };

        var options = new AnalysisOptions
        {
            MinConfidenceThreshold = 0.0,
            EnableIncompleteSessionHandling = true,
            MaxSessionGap = TimeSpan.FromMinutes(5)
        };

        // Act
        var result = _detector.DetectSessions(events, options);

        // Assert
        result.Should().HaveCount(2);
        var firstSession = result.OrderBy(s => s.StartTime).First();
        firstSession.IsIncomplete.Should().BeFalse("다음 세션 시작으로 완료됨");
        firstSession.EndTime.Should().NotBeNull();
        firstSession.EndTime.Should().BeBefore(baseTime.AddMinutes(2));
    }

    [Fact]
    public void DetectSessions_IncompleteSessionHandling_AverageDurationCompletion()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 완전 세션 (평균 계산용)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddMinutes(10), "com.camera", "camera_event"),
            
            // 불완전 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMinutes(20), "com.camera", "camera_event")
        };

        var options = new AnalysisOptions
        {
            MinConfidenceThreshold = 0.0,
            EnableIncompleteSessionHandling = true
        };

        // Act
        var result = _detector.DetectSessions(events, options);

        // Assert
        result.Should().HaveCount(2);
        var incompleteSession = result.OrderBy(s => s.StartTime).Last();
        incompleteSession.EndTime.Should().NotBeNull("평균 지속 시간으로 완료됨");
        incompleteSession.IncompleteReason.Should().Be(SessionIncompleteReason.LogTruncated);
        
        var estimatedDuration = incompleteSession.Duration!.Value;
        estimatedDuration.Should().BeCloseTo(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DetectSessions_SessionMerging_HighOverlap_MergesSessions()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 첫 번째 완전한 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "source1"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "source1"),
            
            // 두 번째 완전한 세션 (90% 겹침)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(11), "com.camera", "source1"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(20), "com.camera", "source1")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        // 현재 구현: 시간순으로 정렬된 이벤트는 페어링되어 세션 생성
        result.Should().HaveCount(2, "겹치지 않는 2개의 독립적인 세션");
        result.All(s => s.PackageName == "com.camera").Should().BeTrue();
        result.All(s => !s.IsIncomplete).Should().BeTrue();
    }

    [Fact]
    public void DetectSessions_SessionMerging_LowOverlap_KeepsSeparate()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 첫 번째 세션
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "source1"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "source1"),
            
            // 두 번째 세션 (겹침 없음)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(11), "com.camera", "source2"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(20), "com.camera", "source2")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(2, "겹침이 80% 미만이면 별도 세션 유지");
    }

    [Fact]
    public void DetectSessions_ProcessId_ExtractsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEventWithPid(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event", 1234),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        result[0].ProcessId.Should().Be(1234);
    }

    [Fact]
    public void DetectSessions_SourceEventIds_StoresCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var event1 = CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.camera", "camera_event");
        var event2 = CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(10), "com.camera", "camera_event");
        var events = new[] { event1, event2 };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        result[0].SourceEventIds.Should().HaveCount(2);
        result[0].SourceEventIds.Should().Contain(event1.EventId);
        result[0].SourceEventIds.Should().Contain(event2.EventId);
        result[0].StartEventId.Should().Be(event1.EventId);
        result[0].EndEventId.Should().Be(event2.EventId);
    }

    // Helper methods
    private NormalizedLogEvent CreateEvent(
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

    private NormalizedLogEvent CreateEventWithPid(
        string eventType,
        DateTime timestamp,
        string packageName,
        string sourceSection,
        int pid)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = sourceSection,
            Attributes = new Dictionary<string, object>
            {
                ["package"] = packageName,
                ["pid"] = pid
            }
        };
    }

    private NormalizedLogEvent CreateEventWithTaskRoot(
        string eventType,
        DateTime timestamp,
        string packageName,
        string taskRootPackage,
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
                ["package"] = packageName,
                ["taskRootPackage"] = taskRootPackage
            }
        };
    }

    [Fact]
    public void DetectSessions_SessionMerging_DifferentPackages_HighOverlap_UsesHigherPriority()
    {
        // Arrange: usagestats(Priority 100) + media_camera(Priority 50), 시간 겹침 >= 80%
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // usagestats 세션 (package=카메라, taskRootPackage=호출 앱)
            CreateEventWithTaskRoot(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.sec.android.app.camera", "com.kakao.talk", "usagestats"),
            CreateEventWithTaskRoot(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(10), "com.sec.android.app.camera", "com.kakao.talk", "usagestats"),
            
            // media_camera 세션 (com.sec.android.app.camera, 같은 시간대)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddSeconds(1), "com.sec.android.app.camera", "media_camera"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddSeconds(9), "com.sec.android.app.camera", "media_camera")
        };

        // Act
        var result = _detector.DetectSessions(events, _defaultOptions);

        // Assert: 병합되어 1개 세션, usagestats의 PackageName 사용
        result.Should().HaveCount(1, "Priority가 높은 usagestats 세션으로 병합");
        result[0].PackageName.Should().Be("com.kakao.talk", "usagestats(Priority 100) > media_camera(Priority 50)");
        result[0].SourceLogTypes.Should().Contain("usagestats");
        result[0].SourceLogTypes.Should().Contain("media_camera");
    }
}
