using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Sessions;

/// <summary>
/// UsagestatsSessionSource 단위 테스트
/// </summary>
/// <remarks>
/// usagestats 로그에서 ACTIVITY_RESUMED → ACTIVITY_PAUSED/STOPPED 패턴 처리를 테스트합니다.
/// </remarks>
public sealed class UsagestatsSessionSourceTests
{
    private readonly UsagestatsSessionSource _source;
    private readonly AnalysisOptions _defaultOptions;

    public UsagestatsSessionSourceTests()
    {
        var confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        _source = new UsagestatsSessionSource(
            NullLogger<UsagestatsSessionSource>.Instance,
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
        priority.Should().Be(100, "UsagestatsSessionSource는 Primary 소스로 Priority 100을 가져야 합니다");
    }

    [Fact]
    public void SourceName_ReturnsCorrectValue()
    {
        // Act
        var sourceName = _source.SourceName;

        // Assert
        sourceName.Should().Be("usagestats");
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
    public void ExtractSessions_CompleteSession_DefaultCamera_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.sec.android.app.camera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(10), "com.sec.android.app.camera")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().Be(baseTime.AddSeconds(10));
        session.PackageName.Should().Be("com.sec.android.app.camera");
        session.IsIncomplete.Should().BeFalse();
        session.Duration.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ExtractSessions_CompleteSession_STOPPED_EndsSession()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.sec.android.app.camera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_STOPPED, baseTime.AddSeconds(10), "com.sec.android.app.camera")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.EndTime.Should().Be(baseTime.AddSeconds(10));
        session.IsIncomplete.Should().BeFalse();
    }

    [Fact]
    public void ExtractSessions_IncompleteSession_MissingEnd_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.sec.android.app.camera")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().BeNull();
        session.PackageName.Should().Be("com.sec.android.app.camera");
        session.IsIncomplete.Should().BeTrue();
        session.IncompleteReason.Should().Be(SessionIncompleteReason.MissingEnd);
    }

    [Fact]
    public void ExtractSessions_MultipleSessions_SamePackage_DetectsAll()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 첫 번째 세션
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.sec.android.app.camera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(5), "com.sec.android.app.camera"),
            // 두 번째 세션
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime.AddSeconds(10), "com.sec.android.app.camera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(15), "com.sec.android.app.camera")
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
    public void ExtractSessions_KakaoTalk_UsesTaskRootPackage()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_RESUMED, 
                baseTime, 
                "com.sec.android.app.camera",
                "com.kakao.talk"),
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_PAUSED, 
                baseTime.AddSeconds(10), 
                "com.sec.android.app.camera",
                "com.kakao.talk")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.PackageName.Should().Be("com.kakao.talk", "taskRootPackage가 우선 사용되어야 합니다");
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().Be(baseTime.AddSeconds(10));
    }

    [Fact]
    public void ExtractSessions_TaskRootPackage_PrioritizedOverPackage()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_RESUMED, 
                baseTime, 
                "com.sec.android.app.camera",
                "com.samsung.android.messaging"),
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_PAUSED, 
                baseTime.AddSeconds(10), 
                "com.sec.android.app.camera",
                "com.samsung.android.messaging")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.PackageName.Should().Be("com.samsung.android.messaging");
    }

    [Fact]
    public void ExtractSessions_SilentCamera_DetectsCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.peace.SilentCamera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(10), "com.peace.SilentCamera")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.PackageName.Should().Be("com.peace.SilentCamera");
        session.IsIncomplete.Should().BeFalse();
    }

    [Fact]
    public void ExtractSessions_NonCameraActivity_Ignored()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.some.other.app"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(10), "com.some.other.app")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().BeEmpty("카메라 관련 앱이 아닌 이벤트는 무시되어야 합니다");
    }

    [Fact]
    public void ExtractSessions_NonActivityEvents_Ignored()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent("OTHER_EVENT", baseTime, "com.sec.android.app.camera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime.AddSeconds(1), "com.sec.android.app.camera"),
            CreateEvent("ANOTHER_EVENT", baseTime.AddSeconds(5), "com.sec.android.app.camera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(10), "com.sec.android.app.camera")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(1);
        var session = result[0];
        session.StartTime.Should().Be(baseTime.AddSeconds(1));
        session.EndTime.Should().Be(baseTime.AddSeconds(10));
    }

    [Fact]
    public void ExtractSessions_NestedSessions_HandlesCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.sec.android.app.camera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime.AddSeconds(5), "com.sec.android.app.camera"), // 중첩
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(10), "com.sec.android.app.camera")
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
    public void ExtractSessions_MultipleDifferentApps_DetectsAllSeparately()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 기본 카메라
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime, "com.sec.android.app.camera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(5), "com.sec.android.app.camera"),
            // 무음 카메라
            CreateActivityEvent(LogEventTypes.ACTIVITY_RESUMED, baseTime.AddSeconds(10), "com.peace.SilentCamera"),
            CreateActivityEvent(LogEventTypes.ACTIVITY_PAUSED, baseTime.AddSeconds(15), "com.peace.SilentCamera"),
            // 카카오톡
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_RESUMED, 
                baseTime.AddSeconds(20), 
                "com.sec.android.app.camera",
                "com.kakao.talk"),
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_PAUSED, 
                baseTime.AddSeconds(25), 
                "com.sec.android.app.camera",
                "com.kakao.talk")
        };

        // Act
        var result = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(s => s.PackageName == "com.sec.android.app.camera");
        result.Should().Contain(s => s.PackageName == "com.peace.SilentCamera");
        result.Should().Contain(s => s.PackageName == "com.kakao.talk");
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
            SourceSection = "usagestats",
            Attributes = new Dictionary<string, object>
            {
                ["package"] = packageName
            }
        };
    }

    private NormalizedLogEvent CreateActivityEvent(
        string eventType,
        DateTime timestamp,
        string packageName)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = "usagestats",
            Attributes = new Dictionary<string, object>
            {
                ["package"] = packageName
            }
        };
    }

    private NormalizedLogEvent CreateActivityEventWithTaskRoot(
        string eventType,
        DateTime timestamp,
        string packageName,
        string taskRootPackage)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = "usagestats",
            Attributes = new Dictionary<string, object>
            {
                ["package"] = packageName,
                ["taskRootPackage"] = taskRootPackage
            }
        };
    }

    /// <summary>
    /// 카카오톡 카메라 시나리오 테스트: taskRootPackage 우선 처리 검증
    /// </summary>
    /// <remarks>
    /// 실제 로그: 22:48:50 ACTIVITY_RESUMED package=com.sec.android.app.camera taskRootPackage=com.kakao.talk
    /// 기대 결과: 세션 PackageName이 com.kakao.talk이어야 함
    /// </remarks>
    [Fact]
    public void ExtractSessions_KakaoTalkCamera_UsesTaskRootPackage()
    {
        // Arrange: 카카오톡에서 카메라 실행 시나리오
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_RESUMED,
                baseTime,
                "com.sec.android.app.camera",
                "com.kakao.talk"),
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_STOPPED,
                baseTime.AddSeconds(5),
                "com.sec.android.app.camera",
                "com.kakao.talk")
        };

        // Act
        var sessions = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        sessions.Should().HaveCount(1, "하나의 완전한 세션이 생성되어야 합니다");
        
        var session = sessions.First();
        session.PackageName.Should().Be("com.kakao.talk", 
            "taskRootPackage가 CameraUsingApps에 포함되므로 세션 패키지명으로 사용되어야 합니다");
        session.StartTime.Should().Be(baseTime);
        session.EndTime.Should().Be(baseTime.AddSeconds(5));
        session.IsIncomplete.Should().BeFalse("RESUMED와 STOPPED 쌍이 있으므로 완전한 세션입니다");
        session.SessionCompletenessScore.Should().BeGreaterThan(0.0);
    }

    /// <summary>
    /// 카카오톡 카메라 다중 촬영 시나리오 테스트
    /// </summary>
    [Fact]
    public void ExtractSessions_KakaoTalkCamera_MultipleCaptures()
    {
        // Arrange: 카카오톡에서 카메라를 여러 번 실행
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 첫 번째 촬영
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_RESUMED,
                baseTime,
                "com.sec.android.app.camera",
                "com.kakao.talk"),
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_STOPPED,
                baseTime.AddSeconds(5),
                "com.sec.android.app.camera",
                "com.kakao.talk"),
            
            // 두 번째 촬영
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_RESUMED,
                baseTime.AddSeconds(60),
                "com.sec.android.app.camera",
                "com.kakao.talk"),
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_STOPPED,
                baseTime.AddSeconds(69),
                "com.sec.android.app.camera",
                "com.kakao.talk")
        };

        // Act
        var sessions = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        sessions.Should().HaveCount(2, "2개의 독립적인 세션이 생성되어야 합니다");
        sessions.Should().OnlyContain(s => s.PackageName == "com.kakao.talk",
            "모든 세션이 taskRootPackage를 사용해야 합니다");
    }

    /// <summary>
    /// 기본 카메라와 카카오톡 카메라 혼합 시나리오
    /// </summary>
    [Fact]
    public void ExtractSessions_MixedCameraUsage_DefaultAndKakaoTalk()
    {
        // Arrange: 기본 카메라 후 카카오톡 카메라
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 기본 카메라 (taskRootPackage 없음)
            CreateActivityEvent(
                LogEventTypes.ACTIVITY_RESUMED,
                baseTime,
                "com.sec.android.app.camera"),
            CreateActivityEvent(
                LogEventTypes.ACTIVITY_STOPPED,
                baseTime.AddSeconds(10),
                "com.sec.android.app.camera"),
            
            // 카카오톡 카메라 (taskRootPackage 있음)
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_RESUMED,
                baseTime.AddSeconds(30),
                "com.sec.android.app.camera",
                "com.kakao.talk"),
            CreateActivityEventWithTaskRoot(
                LogEventTypes.ACTIVITY_STOPPED,
                baseTime.AddSeconds(35),
                "com.sec.android.app.camera",
                "com.kakao.talk")
        };

        // Act
        var sessions = _source.ExtractSessions(events, _defaultOptions);

        // Assert
        sessions.Should().HaveCount(2, "2개의 독립적인 세션이 생성되어야 합니다");
        
        var defaultCameraSession = sessions.First();
        defaultCameraSession.PackageName.Should().Be("com.sec.android.app.camera",
            "taskRootPackage가 없으면 package를 사용해야 합니다");
        
        var kakaoTalkSession = sessions.Last();
        kakaoTalkSession.PackageName.Should().Be("com.kakao.talk",
            "taskRootPackage가 CameraUsingApps에 있으면 우선 사용해야 합니다");
    }

    // TODO: 파싱 검증 테스트는 Integration Test를 통해 확인
    // 실제 로그 파싱은 EndToEndAnalysisTests에서 이미 검증되고 있음
}

