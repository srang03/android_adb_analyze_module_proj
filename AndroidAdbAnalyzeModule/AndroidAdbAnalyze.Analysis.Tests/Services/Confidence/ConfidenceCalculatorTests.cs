using AndroidAdbAnalyze.Analysis.Services.Confidence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Confidence;

/// <summary>
/// ConfidenceCalculator 단위 테스트
/// </summary>
public sealed class ConfidenceCalculatorTests
{
    private readonly ConfidenceCalculator _calculator;

    public ConfidenceCalculatorTests()
    {
        _calculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
    }

    [Fact]
    public void CalculateConfidence_EmptyList_ReturnsZero()
    {
        // Arrange
        var events = Array.Empty<NormalizedLogEvent>();

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void CalculateConfidence_NullList_ReturnsZero()
    {
        // Act
        var result = _calculator.CalculateConfidence(null!);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void CalculateConfidence_SingleHighWeightEvent_ReturnsCorrectScore()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT)
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().Be(0.5, "DATABASE_INSERT는 가중치 0.5");
    }

    [Fact]
    public void CalculateConfidence_MultipleDifferentTypes_SumsWeights()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT),    // 0.5
            CreateEvent(LogEventTypes.CAMERA_CONNECT),        // 0.4
            CreateEvent(LogEventTypes.PLAYER_EVENT)         // 0.35
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().BeApproximately(1.0, 0.01, "0.5 + 0.4 + 0.35 = 1.25, 하지만 최대값 1.0으로 제한");
    }

    [Fact]
    public void CalculateConfidence_DuplicateTypes_CountsOnlyOnce()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT),
            CreateEvent(LogEventTypes.CAMERA_CONNECT),
            CreateEvent(LogEventTypes.CAMERA_CONNECT)
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().Be(0.4, "동일 타입은 한 번만 계산");
    }

    [Fact]
    public void CalculateConfidence_MaxValueCapped_ReturnsOne()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT),    // 0.5
            CreateEvent(LogEventTypes.DATABASE_EVENT),     // 0.5
            CreateEvent(LogEventTypes.CAMERA_CONNECT),        // 0.4
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT)        // 0.4
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().Be(1.0, "최대값은 1.0으로 제한됨");
    }

    [Fact]
    public void CalculateConfidence_UnknownEventType_UsesDefaultWeight()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent("UNKNOWN_EVENT_TYPE")
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().Be(0.1, "알 수 없는 타입은 기본 가중치 0.1");
    }

    [Fact]
    public void CalculateConfidence_MixedKnownAndUnknown_SumsCorrectly()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT),  // 0.4
            CreateEvent("UNKNOWN_EVENT")             // 0.1
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void GetEventTypeWeight_KnownTypes_ReturnsCorrectWeights()
    {
        // Assert - 가장 강력한 증거
        _calculator.GetEventTypeWeight(LogEventTypes.DATABASE_INSERT).Should().Be(0.5);
        _calculator.GetEventTypeWeight(LogEventTypes.DATABASE_EVENT).Should().Be(0.5);
        _calculator.GetEventTypeWeight(LogEventTypes.MEDIA_INSERT_END).Should().Be(0.5);
        
        // 강력한 증거
        _calculator.GetEventTypeWeight(LogEventTypes.CAMERA_CONNECT).Should().Be(0.4);
        _calculator.GetEventTypeWeight(LogEventTypes.CAMERA_DISCONNECT).Should().Be(0.4);
        
        // 중간 증거
        _calculator.GetEventTypeWeight(LogEventTypes.PLAYER_EVENT).Should().Be(0.35);
        _calculator.GetEventTypeWeight(LogEventTypes.URI_PERMISSION_GRANT).Should().Be(0.3);
        _calculator.GetEventTypeWeight(LogEventTypes.URI_PERMISSION_REVOKE).Should().Be(0.3);
        _calculator.GetEventTypeWeight(LogEventTypes.ACTIVITY_LIFECYCLE).Should().Be(0.25);
        _calculator.GetEventTypeWeight(LogEventTypes.PLAYER_CREATED).Should().Be(0.25);
        
        // 보조 증거
        _calculator.GetEventTypeWeight(LogEventTypes.SHUTTER_SOUND).Should().Be(0.2);
        _calculator.GetEventTypeWeight(LogEventTypes.MEDIA_EXTRACTOR).Should().Be(0.2);
        _calculator.GetEventTypeWeight(LogEventTypes.PLAYER_RELEASED).Should().Be(0.15);
        _calculator.GetEventTypeWeight(LogEventTypes.VIBRATION).Should().Be(0.15);
        _calculator.GetEventTypeWeight(LogEventTypes.VIBRATION_EVENT).Should().Be(0.4);
    }

    [Fact]
    public void GetEventTypeWeight_UnknownType_ReturnsDefaultWeight()
    {
        // Act
        var result = _calculator.GetEventTypeWeight("UNKNOWN_EVENT");

        // Assert
        result.Should().Be(0.1);
    }

    [Theory]
    [InlineData(LogEventTypes.DATABASE_INSERT, LogEventTypes.DATABASE_EVENT, 1.0)]
    [InlineData(LogEventTypes.CAMERA_CONNECT, LogEventTypes.CAMERA_DISCONNECT, 0.8)]
    [InlineData(LogEventTypes.CAMERA_CONNECT, LogEventTypes.PLAYER_EVENT, 0.75)]  // 0.4 + 0.35
    [InlineData(LogEventTypes.CAMERA_CONNECT, LogEventTypes.VIBRATION, 0.55)]
    public void CalculateConfidence_CommonCombinations_ReturnsExpectedScores(
        string type1, string type2, double expectedScore)
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(type1),
            CreateEvent(type2)
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().BeApproximately(expectedScore, 0.01);
    }

    [Fact]
    public void CalculateConfidence_StrongEvidenceOnly_HighConfidence()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT),
            CreateEvent(LogEventTypes.CAMERA_CONNECT),
            CreateEvent(LogEventTypes.CAMERA_DISCONNECT)
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().BeGreaterThanOrEqualTo(0.9, "강력한 증거들의 조합은 높은 신뢰도");
    }

    [Fact]
    public void CalculateConfidence_WeakEvidenceOnly_LowConfidence()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.VIBRATION),           // 0.15
            CreateEvent(LogEventTypes.PLAYER_RELEASED)      // 0.15
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().BeLessThanOrEqualTo(0.3, "약한 증거들의 조합은 낮은 신뢰도");
    }

    [Fact]
    public void CalculateConfidence_MixedEvidence_ModerateConfidence()
    {
        // Arrange
        var events = new[]
        {
            CreateEvent(LogEventTypes.CAMERA_CONNECT),        // 0.4 - 강력한 증거
            CreateEvent(LogEventTypes.PLAYER_EVENT),          // 0.35 - 중간 증거
            CreateEvent(LogEventTypes.VIBRATION)              // 0.15 - 약한 증거
        };

        // Act
        var result = _calculator.CalculateConfidence(events);

        // Assert
        result.Should().BeInRange(0.5, 0.95, "혼합 증거는 중간-높은 신뢰도 (0.9)");
    }

    // Helper method
    private NormalizedLogEvent CreateEvent(string eventType)
    {
        return new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            SourceSection = "test_section",
            Attributes = new Dictionary<string, object>()
        };
    }
}
