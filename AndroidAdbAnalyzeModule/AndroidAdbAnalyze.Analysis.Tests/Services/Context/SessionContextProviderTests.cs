using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Context;
using FluentAssertions;
using Xunit;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Context;

/// <summary>
/// SessionContextProvider 단위 테스트
/// </summary>
public sealed class SessionContextProviderTests
{
    private readonly SessionContextProvider _provider;

    public SessionContextProviderTests()
    {
        _provider = new SessionContextProvider();
    }

    [Fact]
    public void CreateContext_ValidSession_ReturnsContext()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var session = CreateSession(baseTime, baseTime.AddMinutes(5));
        var events = CreateBasicEvents(baseTime, "com.sec.android.app.camera");

        // Act
        var context = _provider.CreateContext(session, events);

        // Assert
        context.Should().NotBeNull();
        context.Session.Should().Be(session);
        context.AllEvents.Should().NotBeEmpty();
        context.AllEvents.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void CreateContext_ExtractsForegroundServices()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var serviceStartTime = baseTime.AddSeconds(10);
        var serviceStopTime = baseTime.AddMinutes(2);
        var session = CreateSession(baseTime, baseTime.AddMinutes(5));
        
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.sec.android.app.camera"),
            CreateForegroundServiceEvent(serviceStartTime, "com.sec.android.app.camera", 
                "com.samsung.android.camera.core2.processor.PostProcessService", "FOREGROUND_SERVICE_START"),
            CreateForegroundServiceEvent(serviceStopTime, "com.sec.android.app.camera", 
                "com.samsung.android.camera.core2.processor.PostProcessService", "FOREGROUND_SERVICE_STOP"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddMinutes(5), "com.sec.android.app.camera")
        };

        // Act
        var context = _provider.CreateContext(session, events);

        // Assert
        context.ForegroundServices.Should().HaveCount(1);
        context.ForegroundServices[0].ServiceClass.Should().Contain("PostProcessService");
        context.ForegroundServices[0].StartTime.Should().Be(serviceStartTime);
        context.ForegroundServices[0].StopTime.Should().Be(serviceStopTime);
    }

    [Fact]
    public void CreateContext_FiltersEventsWithinSession()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var session = CreateSession(baseTime.AddMinutes(1), baseTime.AddMinutes(3)); // 1분~3분
        
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime, "com.sec.android.app.camera"), // 세션 전
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddMinutes(2), "com.sec.android.app.camera"), // 세션 내
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddMinutes(5), "com.sec.android.app.camera") // 세션 후 (확장 범위 밖)
        };

        // Act
        var context = _provider.CreateContext(session, events);

        // Assert
        context.AllEvents.Should().HaveCount(1, "세션 시간 범위(+10초 확장) 내 이벤트만 포함");
        context.AllEvents[0].Timestamp.Should().Be(baseTime.AddMinutes(2));
    }

    [Fact]
    public void CreateContext_NoUsagestatsEvents_ReturnsEmptyActivityTimes()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var session = CreateSession(baseTime, baseTime.AddMinutes(5));
        
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, "com.sec.android.app.camera"),
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(30), "com.sec.android.app.camera"),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddMinutes(5), "com.sec.android.app.camera")
        };

        // Act
        var context = _provider.CreateContext(session, events);

        // Assert
        context.ForegroundServices.Should().BeEmpty("FOREGROUND_SERVICE 이벤트가 없음");
    }

    [Fact]
    public void CreateContext_NullSession_ThrowsException()
    {
        // Arrange
        var events = new List<NormalizedLogEvent>();

        // Act
        var act = () => _provider.CreateContext(null!, events);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("session");
    }

    #region Helper Methods

    private CameraSession CreateSession(DateTime startTime, DateTime? endTime, string packageName = "com.sec.android.app.camera")
    {
        return new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = startTime,
            EndTime = endTime,
            PackageName = packageName,
            SourceLogTypes = new[] { "media.camera" },
            SessionCompletenessScore = 0.8,
            SourceEventIds = Array.Empty<Guid>()
        };
    }

    private List<NormalizedLogEvent> CreateBasicEvents(DateTime baseTime, string packageName)
    {
        return new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, packageName),
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(30), packageName),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddMinutes(5), packageName)
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

    private NormalizedLogEvent CreateActivityLifecycleEvent(
        DateTime timestamp,
        string packageName,
        string activityState)
    {
        // Parser는 activityState를 EventType으로 직접 파싱합니다 (eventType: "{subType}")
        // 예: ACTIVITY_RESUMED, ACTIVITY_PAUSED, ACTIVITY_STOPPED
        return CreateEvent(activityState, timestamp, packageName);
    }

    private NormalizedLogEvent CreateForegroundServiceEvent(
        DateTime timestamp,
        string packageName,
        string className,
        string serviceState)
    {
        return CreateEvent(LogEventTypes.FOREGROUND_SERVICE, timestamp, packageName,
            new Dictionary<string, object>
            {
                ["className"] = className,
                ["serviceState"] = serviceState
            });
    }

    #endregion
}
