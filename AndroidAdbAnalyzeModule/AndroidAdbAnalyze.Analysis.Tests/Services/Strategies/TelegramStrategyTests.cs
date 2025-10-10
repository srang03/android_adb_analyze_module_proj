using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Strategies;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Strategies;

/// <summary>
/// TelegramStrategy 단위 테스트
/// </summary>
public sealed class TelegramStrategyTests
{
    private readonly TelegramStrategy _strategy;
    private readonly IConfidenceCalculator _confidenceCalculator;
    private readonly AnalysisOptions _defaultOptions;

    public TelegramStrategyTests()
    {
        _confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        _strategy = new TelegramStrategy(
            NullLogger<TelegramStrategy>.Instance,
            _confidenceCalculator);

        _defaultOptions = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MinConfidenceThreshold = 0.15, // Telegram: VIBRATION_EVENT (0.15 가중치)만으로 탐지 가능
            ScreenshotPathPatterns = new[] { "/Screenshots/", "/screenshot/" },
            DownloadPathPatterns = new[] { "/Download/", "/download/" }
        };
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_Touch_DetectsCapture()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> 
                { 
                    ["usage"] = "TOUCH",
                    ["duration"] = "10"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "VIBRATION_EVENT (usage: TOUCH)는 Telegram의 주 증거");
        captures[0].CaptureTime.Should().Be(baseTime.AddSeconds(38));
        captures[0].PackageName.Should().Be("org.telegram.messenger");
        captures[0].IsEstimated.Should().BeFalse();
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_NonTouch_Excluded()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> 
                { 
                    ["usage"] = "NOTIFICATION", // TOUCH가 아님
                    ["duration"] = "10"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("VIBRATION_EVENT의 usage가 TOUCH가 아니면 제외");
    }

    [Fact]
    public void DetectCaptures_ExcludesPlayerEvents()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime.AddSeconds(40), 
                "org.telegram.messenger",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 123, 
                    ["event"] = "started" 
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].SupportingEvidenceIds.Should().BeEmpty("PLAYER_EVENT는 보조 증거에서 제외됨");
        captures[0].EvidenceTypes.Should().NotContain(LogEventTypes.PLAYER_EVENT);
    }

    [Fact]
    public void DetectCaptures_MultipleVibrations_DetectsAll()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(30), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMinutes(2), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(2, "2개의 VIBRATION_EVENT (TOUCH) 발생");
        captures.Select(c => c.CaptureTime).Should().BeInAscendingOrder();
    }

    [Fact]
    public void PackageNamePattern_ReturnsTelegramPattern()
    {
        // Act
        var pattern = _strategy.PackageNamePattern;

        // Assert
        pattern.Should().Be("org.telegram.messenger", "Telegram 전용 전략");
    }

    [Fact]
    public void Priority_Returns100()
    {
        // Act
        var priority = _strategy.Priority;

        // Assert
        priority.Should().Be(100, "BaseStrategy(0)보다 높은 우선순위");
    }

    [Fact]
    public void DetectCaptures_SetsMetadata_DetectionStrategy()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].Metadata.Should().ContainKey("detection_strategy");
        captures[0].Metadata["detection_strategy"].Should().Be("TelegramStrategy");
        captures[0].Metadata.Should().ContainKey("primary_evidence_type");
        captures[0].Metadata["primary_evidence_type"].Should().Be("VIBRATION_EVENT");
    }

    #region Usage Attribute Validation

    [Fact]
    public void DetectCaptures_VibrationEvent_NoUsageAttribute_Excluded()
    {
        // Arrange: usage 속성이 없는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> 
                { 
                    ["duration"] = "10" // usage 없음
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("usage 속성이 없으면 제외");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_UsageNull_Excluded()
    {
        // Arrange: usage가 null
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> 
                { 
                    ["usage"] = null! // null
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("usage가 null이면 제외");
    }

    [Theory]
    [InlineData("touch")] // 소문자
    [InlineData("Touch")] // 첫 글자 대문자
    [InlineData("TOUCH")] // 대문자
    [InlineData("TouCH")] // 혼합
    public void DetectCaptures_VibrationEvent_UsageCaseInsensitive_DetectsCapture(string usageValue)
    {
        // Arrange: usage 대소문자 무관
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = usageValue })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, $"usage='{usageValue}'는 TOUCH로 인식 (대소문자 무관)");
    }

    #endregion

    #region Package Validation

    [Fact]
    public void DetectCaptures_VibrationEvent_NoPackageAttribute_Excluded()
    {
        // Arrange: package 속성이 없는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var eventWithoutPackage = new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = baseTime.AddSeconds(38),
            EventType = LogEventTypes.VIBRATION_EVENT,
            SourceSection = "test_section",
            SourceFileName = "test.log",
            PackageName = "org.telegram.messenger",
            Attributes = new Dictionary<string, object> 
            { 
                ["usage"] = "TOUCH"
                // package 속성 없음
            },
            RawLine = "Test log line"
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", 
            new List<NormalizedLogEvent> { eventWithoutPackage });

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("package 속성이 없으면 제외");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_PackageMismatch_Excluded()
    {
        // Arrange: 세션 패키지와 이벤트 패키지가 다름
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "com.other.app", // ❌ 다른 패키지
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("세션 패키지와 이벤트 패키지가 다르면 제외");
    }

    #endregion

    #region Confidence Threshold

    [Fact]
    public void DetectCaptures_BelowMinConfidence_Excluded()
    {
        // Arrange: 신뢰도가 임계값보다 낮음
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);
        
        // 높은 임계값 설정
        var highThresholdOptions = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MinConfidenceThreshold = 0.9, // VIBRATION_EVENT (0.4)보다 높음
            ScreenshotPathPatterns = new[] { "/Screenshots/" },
            DownloadPathPatterns = new[] { "/Download/" }
        };

        // Act
        var captures = _strategy.DetectCaptures(context, highThresholdOptions);

        // Assert
        captures.Should().BeEmpty("신뢰도가 임계값 미만이면 제외");
    }

    [Fact]
    public void DetectCaptures_WithSupportingEvidence_IncreasesConfidence()
    {
        // Arrange: 보조 증거가 있어 신뢰도 증가
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            // 보조 증거
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(37), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(39), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["uri"] = "content://test" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].ConfidenceScore.Should().BeGreaterThan(0.4, "보조 증거가 있어 신뢰도 증가");
        captures[0].SupportingEvidenceIds.Should().NotBeEmpty("보조 증거가 있어야 함");
    }

    #endregion

    #region EventCorrelationWindow Tests

    [Fact]
    public void DetectCaptures_SupportingEvidence_OutsideWindow_Excluded()
    {
        // Arrange: 보조 증거가 correlation window 밖에 있음
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 38);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            // 보조 증거 (31초 전 - 윈도우 밖)
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(-31), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            // 보조 증거 (31초 후 - 윈도우 밖)
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(31), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["uri"] = "content://test" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "주 증거는 탐지됨");
        captures[0].ConfidenceScore.Should().Be(0.4, "보조 증거가 윈도우 밖이므로 신뢰도 증가 없음");
        captures[0].SupportingEvidenceIds.Should().BeEmpty();
    }

    [Fact]
    public void DetectCaptures_SupportingEvidence_InsideWindow_Included()
    {
        // Arrange: 보조 증거가 correlation window 안에 있음
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 38);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            // 보조 증거 (29초 전 - 윈도우 안)
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(-29), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            // 보조 증거 (29초 후 - 윈도우 안)
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(29), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["uri"] = "content://test" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "주 증거 탐지");
        captures[0].ConfidenceScore.Should().BeGreaterThan(0.4, "보조 증거가 윈도우 안이므로 신뢰도 증가");
        captures[0].SupportingEvidenceIds.Should().HaveCount(2);
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.VIBRATION);
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.URI_PERMISSION_GRANT);
    }

    [Fact]
    public void DetectCaptures_SupportingEvidence_ExactWindowBoundary_Included()
    {
        // Arrange: 보조 증거가 정확히 window 경계에 있음
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 38);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            // 보조 증거 (정확히 30초 전 - 경계)
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(-30), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            // 보조 증거 (정확히 30초 후 - 경계)
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(30), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["uri"] = "content://test" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].SupportingEvidenceIds.Should().HaveCount(2, 
            "정확히 경계에 있는 보조 증거도 포함 (>= windowStart && <= windowEnd)");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DetectCaptures_EmptyEventList_ReturnsEmpty()
    {
        // Arrange: 이벤트가 없는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>();
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("이벤트가 없으면 촬영도 없음");
    }

    [Fact]
    public void DetectCaptures_ZeroCorrelationWindow_OnlyPrimaryEvidence()
    {
        // Arrange: correlation window가 0인 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 38);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(1), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);
        
        var zeroWindowOptions = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.Zero, // ✅ 0초
            MinConfidenceThreshold = 0.15,
            ScreenshotPathPatterns = new[] { "/Screenshots/" },
            DownloadPathPatterns = new[] { "/Download/" }
        };

        // Act
        var captures = _strategy.DetectCaptures(context, zeroWindowOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].ConfidenceScore.Should().Be(0.4, "correlation window가 0이면 보조 증거 수집 안 됨");
        captures[0].SupportingEvidenceIds.Should().BeEmpty();
    }

    [Fact]
    public void DetectCaptures_OnlyVibrationWithoutTouch_ReturnsEmpty()
    {
        // Arrange: VIBRATION_EVENT만 있지만 usage가 TOUCH가 아님
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            // VIBRATION_EVENT이지만 usage가 다름
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "NOTIFICATION" }),
            
            // VIBRATION이지만 VIBRATION_EVENT가 아님
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(39), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("VIBRATION_EVENT (usage=TOUCH)가 없으면 촬영 탐지 안 됨");
    }

    #endregion

    #region Telegram-Specific Features

    [Fact]
    public void DetectCaptures_FilePath_AlwaysNull()
    {
        // Arrange: Telegram은 파일 경로 정보 없음
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].FilePath.Should().BeNull("Telegram은 파일 경로 정보 없음");
        captures[0].FileUri.Should().BeNull("Telegram은 파일 URI 정보 없음");
    }

    [Fact]
    public void DetectCaptures_IsEstimated_AlwaysFalse()
    {
        // Arrange: VIBRATION_EVENT가 주 증거이므로 IsEstimated=false
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].IsEstimated.Should().BeFalse("VIBRATION_EVENT가 주 증거이므로 추정 아님");
    }

    [Fact]
    public void DetectCaptures_Metadata_IncludesAllAttributes()
    {
        // Arrange: 메타데이터에 모든 속성 포함
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(38), 
                "org.telegram.messenger",
                new Dictionary<string, object> 
                { 
                    ["usage"] = "TOUCH",
                    ["duration"] = "125",
                    ["package"] = "org.telegram.messenger"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        var capture = captures[0];
        
        capture.Metadata.Should().ContainKey("detection_strategy");
        capture.Metadata["detection_strategy"].Should().Be("TelegramStrategy");
        
        capture.Metadata.Should().ContainKey("primary_evidence_type");
        capture.Metadata["primary_evidence_type"].Should().Be("VIBRATION_EVENT");
        
        capture.Metadata.Should().ContainKey("usage");
        capture.Metadata["usage"].Should().Be("TOUCH");
        
        capture.Metadata.Should().ContainKey("duration");
        capture.Metadata["duration"].Should().Be("125");
        
        capture.Metadata.Should().ContainKey("package");
        capture.Metadata["package"].Should().Be("org.telegram.messenger");
    }

    [Fact]
    public void DetectCaptures_SupportingEvidenceTypes_CorrectlyFiltered()
    {
        // Arrange: 다양한 보조 증거 타입 테스트
        var baseTime = new DateTime(2025, 10, 6, 22, 54, 38);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            // 보조 증거 (포함)
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(1), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["usage"] = "TOUCH" }),
            
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(2), 
                "org.telegram.messenger",
                new Dictionary<string, object> { ["uri"] = "content://test" }),
            
            // PLAYER_EVENT (제외됨)
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime.AddSeconds(3), 
                "org.telegram.messenger",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 123,
                    ["event"] = "started"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "org.telegram.messenger", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].SupportingEvidenceIds.Should().HaveCount(2, "PLAYER_EVENT 제외");
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.VIBRATION);
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.URI_PERMISSION_GRANT);
        captures[0].EvidenceTypes.Should().NotContain(LogEventTypes.PLAYER_EVENT, "PLAYER_EVENT는 제외됨");
    }

    #endregion

    #region Helper Methods

    private SessionContext CreateContext(
        DateTime startTime, 
        DateTime? endTime, 
        string packageName,
        List<NormalizedLogEvent>? events = null)
    {
        var session = new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = startTime,
            EndTime = endTime,
            PackageName = packageName,
            SourceLogTypes = new[] { "vibrator_manager" },
            ConfidenceScore = 0.8,
            SourceEventIds = Array.Empty<Guid>()
        };

        return new SessionContext
        {
            Session = session,
            AllEvents = events ?? new List<NormalizedLogEvent>(),
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