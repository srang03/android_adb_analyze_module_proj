using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Deduplication;

/// <summary>
/// EventDeduplicator 단위 테스트
/// </summary>
public sealed class EventDeduplicatorTests
{
    private readonly EventDeduplicator _deduplicator;
    private readonly AnalysisOptions _defaultOptions;

    public EventDeduplicatorTests()
    {
        _defaultOptions = new AnalysisOptions
        {
            DeduplicationSimilarityThreshold = 0.8
        };
        _deduplicator = new EventDeduplicator(
            NullLogger<EventDeduplicator>.Instance,
            _defaultOptions);
    }

    [Fact]
    public void Deduplicate_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var events = Array.Empty<NormalizedLogEvent>();

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().BeEmpty();
        details.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_SingleEvent_ReturnsOriginal()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, DateTime.UtcNow)
        };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(1);
        result[0].EventId.Should().Be(events[0].EventId);
        details.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_DuplicateEventsWithinThreshold_RemovesDuplicates()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
                CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, new Dictionary<string, object> { ["package"] = "com.camera" }),
                CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(50), new Dictionary<string, object> { ["package"] = "com.camera" }),
                CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(100), new Dictionary<string, object> { ["package"] = "com.camera" })
        };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(1, "3개의 중복 이벤트가 1개로 제거되어야 함");
        details.Should().HaveCount(1, "1개의 DeduplicationInfo가 생성되어야 함");
        details[0].DuplicateEventIds.Should().HaveCount(2, "2개의 중복 이벤트 ID가 기록되어야 함");
    }

    [Fact]
    public void Deduplicate_EventsExceedingThreshold_KeepsSeparate()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, 
                new Dictionary<string, object> { ["package"] = "com.camera", ["cameraId"] = 0 }),
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(1100), // 1000ms 초과
                new Dictionary<string, object> { ["package"] = "com.camera", ["cameraId"] = 0 })
        };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(2, "시간 임계값(1000ms)을 초과하므로 2개가 유지되어야 함");
        details.Should().BeEmpty("중복이 없으므로 details가 비어있어야 함");
    }

    [Fact]
    public void Deduplicate_ExactlyOnThreshold_GroupsTogether()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, 
                new Dictionary<string, object> { ["package"] = "com.camera", ["cameraId"] = 0 }),
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(1000), // 정확히 1000ms
                new Dictionary<string, object> { ["package"] = "com.camera", ["cameraId"] = 0 })
        };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(1, "1000ms 이하이므로 1개로 그룹화되어야 함");
        details.Should().HaveCount(1);
    }

    [Fact]
    public void Deduplicate_DifferentEventTypes_KeepsSeparate()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddMilliseconds(10)) // 다른 타입
        };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(2, "EventType이 다르므로 2개가 유지되어야 함");
        details.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_SelectsEventWithMostAttributes()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var event1 = CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, new Dictionary<string, object> 
        { 
            ["package"] = "com.camera" 
        });
        var event2 = CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(50), new Dictionary<string, object> 
        { 
            ["package"] = "com.camera",
            ["pid"] = "1234",
            ["uid"] = "5678"
        });
        var event3 = CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(100), new Dictionary<string, object> 
        { 
            ["package"] = "com.camera",
            ["pid"] = "1234"
        });

        var events = new[] { event1, event2, event3 };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(1);
        result[0].EventId.Should().Be(event2.EventId, "event2가 가장 많은 Attributes를 가지고 있음");
        details[0].RepresentativeEventId.Should().Be(event2.EventId);
    }

    [Fact]
    public void Deduplicate_CalculatesSimilarity()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, new Dictionary<string, object> 
            { 
                ["package"] = "com.camera",
                ["pid"] = "1234"
            }),
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(50), new Dictionary<string, object> 
            { 
                ["package"] = "com.camera",
                ["pid"] = "1234"
            })
        };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(1);
        details.Should().HaveCount(1);
        details[0].Similarity.Should().BeGreaterThan(0.9, "완전히 동일한 속성이므로 유사도가 높아야 함");
    }

    [Fact]
    public void Deduplicate_DifferentTypeThresholds_AppliesCorrectly()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        
        // DATABASE_INSERT는 500ms 임계값
        var dbEvents = new[]
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime),
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddMilliseconds(400)) // 500ms 이하
        };
        
        // PLAYER_EVENT는 100ms 임계값
        var playerEvents = new[]
        {
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime),
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime.AddMilliseconds(150)) // 100ms 초과
        };

        // Act
        var dbResult = _deduplicator.Deduplicate(dbEvents, out var dbDetails);
        var playerResult = _deduplicator.Deduplicate(playerEvents, out var playerDetails);

        // Assert
        dbResult.Should().HaveCount(1, "DATABASE_INSERT는 500ms 이하이므로 1개");
        dbDetails.Should().HaveCount(1);
        
        playerResult.Should().HaveCount(2, "PLAYER_EVENT는 100ms 초과이므로 2개");
        playerDetails.Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_MultipleGroupsOfSameType_ProcessesIndependently()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            // 첫 번째 그룹
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime, 
                new Dictionary<string, object> { ["package"] = "com.camera", ["cameraId"] = 0 }),
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(50),
                new Dictionary<string, object> { ["package"] = "com.camera", ["cameraId"] = 0 }),
            
            // 두 번째 그룹 (첫 그룹과 시간 차이 큼 - 1000ms 초과)
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(1200),
                new Dictionary<string, object> { ["package"] = "com.camera", ["cameraId"] = 0 }),
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(1250),
                new Dictionary<string, object> { ["package"] = "com.camera", ["cameraId"] = 0 })
        };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(2, "2개의 독립적인 그룹이 각각 1개씩 유지되어야 함");
        details.Should().HaveCount(2, "각 그룹에서 1개씩 중복 제거 발생");
    }

    [Fact]
    public void Deduplicate_MixedEventTypes_ProcessesEachTypeSeparately()
    {
        // Arrange
        var baseTime = DateTime.UtcNow;
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime),
            CreateEvent(LogEventTypes.CAMERA_CONNECT, baseTime.AddMilliseconds(50)),
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddMilliseconds(60)),
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddMilliseconds(110)),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT, baseTime.AddMilliseconds(120))
        };

        // Act
        var result = _deduplicator.Deduplicate(events, out var details);

        // Assert
        result.Should().HaveCount(3, "CAMERA_OPEN(1개), DATABASE_INSERT(1개), CAMERA_CLOSE(1개)");
        details.Should().HaveCount(2, "CAMERA_OPEN과 DATABASE_INSERT에서 각각 중복 제거");
    }

    // Helper method
    private NormalizedLogEvent CreateEvent(
        string eventType, 
        DateTime timestamp, 
        Dictionary<string, object>? attributes = null)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = timestamp,
            EventType = eventType,
            SourceSection = "test_section",
            Attributes = (IReadOnlyDictionary<string, object>)(attributes ?? new Dictionary<string, object>())
        };
    }
}
