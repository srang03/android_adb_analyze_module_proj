using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Strategies;

/// <summary>
/// KakaoTalkStrategy 단위 테스트
/// 
/// 현재 비즈니스 로직:
/// - 주 증거: VIBRATION_EVENT (hapticType=50061)
/// - 보조 증거: URI_PERMISSION_GRANT, CAMERA_ACTIVITY_REFRESH
/// </summary>
public sealed class KakaoTalkStrategyTests
{
    private readonly KakaoTalkStrategy _strategy;
    private readonly IConfidenceCalculator _confidenceCalculator;
    private readonly AnalysisOptions _defaultOptions;

    public KakaoTalkStrategyTests()
    {
        _confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        _strategy = new KakaoTalkStrategy(
            NullLogger<KakaoTalkStrategy>.Instance,
            _confidenceCalculator);

        _defaultOptions = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MinConfidenceThreshold = 0.4, // VIBRATION_EVENT (hapticType=50061) 신뢰도 = 0.4
            ScreenshotPathPatterns = new[] { "/Screenshots/", "/screenshot/" },
            DownloadPathPatterns = new[] { "/Download/", "/download/" }
        };
    }

    #region Basic Properties

    [Fact]
    public void PackageNamePattern_ReturnsCorrectValue()
    {
        // Act
        var pattern = _strategy.PackageNamePattern;

        // Assert
        pattern.Should().Be("com.kakao.talk");
    }

    #endregion

    #region VIBRATION_EVENT (hapticType=50061) Detection

    [Fact]
    public void DetectCaptures_VibrationEvent_HapticType50061_DetectsCapture()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMilliseconds(660), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished",
                    ["usage"] = "TOUCH",
                    ["duration"] = "125"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "VIBRATION_EVENT (hapticType=50061)는 KakaoTalk의 주 증거");
        captures[0].CaptureTime.Should().Be(baseTime.AddMilliseconds(660));
        captures[0].PackageName.Should().Be("com.kakao.talk");
        captures[0].IsEstimated.Should().BeFalse();
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_WrongHapticType_Excluded()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 48, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(51), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50072, // ❌ 50061이 아님
                    ["usage"] = "TOUCH",
                    ["duration"] = "10"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("hapticType이 50061이 아니면 제외");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_NoHapticType_Excluded()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 48, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(51), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["usage"] = "TOUCH", // ❌ hapticType 없음
                    ["duration"] = "10"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("hapticType 정보가 없으면 제외");
    }

    #endregion


    #region Supporting Artifact

    [Fact]
    public void DetectCaptures_WithSupportingArtifact_IncreasesConfidence()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMilliseconds(660), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished",
                    ["usage"] = "TOUCH"
                }),
            
            // 보조 주 증거
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddMilliseconds(700), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["uri"] = "content://test" }),
            
            CreateEvent(LogEventTypes.CAMERA_ACTIVITY_REFRESH, baseTime.AddMilliseconds(650), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["refreshRate"] = "120" }),
            
            // 일반 보조 증거
            CreateEvent(LogEventTypes.PLAYER_CREATED, baseTime.AddMilliseconds(650), "com.kakao.talk"),
            CreateEvent(LogEventTypes.MEDIA_EXTRACTOR, baseTime.AddMilliseconds(700), "com.kakao.talk")
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "VIBRATION_EVENT (hapticType=50061)만 주 증거");
        captures[0].CaptureDetectionScore.Should().BeGreaterThan(0.4, "보조 증거가 있으면 신뢰도 증가");
        captures[0].ArtifactTypes.Should().HaveCountGreaterThan(1, "주 증거 + 보조 증거");
        captures[0].FileUri.Should().NotBeNull("URI_PERMISSION_GRANT에서 URI 추출");
    }

    #endregion

    #region Multiple Captures

    [Fact]
    public void DetectCaptures_MultipleVibrationEvents_DetectsMultiple()
    {
        // Arrange: 샘플 4 데이터 기반 (카카오톡 2회 촬영)
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 0);
        var events = new List<NormalizedLogEvent>
        {
            // 1차 촬영 (22:49:56)
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(56).AddMilliseconds(660), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished",
                    ["usage"] = "TOUCH"
                }),
            
            // 2차 촬영 (22:50:58)
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMinutes(1).AddSeconds(58).AddMilliseconds(595), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "cancelled_superseded",
                    ["usage"] = "TOUCH"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(2, "2개의 VIBRATION_EVENT (hapticType=50061) → 2개 촬영");
        captures[0].CaptureTime.Should().Be(baseTime.AddSeconds(56).AddMilliseconds(660));
        captures[1].CaptureTime.Should().Be(baseTime.AddMinutes(1).AddSeconds(58).AddMilliseconds(595));
    }

    #endregion

    #region Confidence Threshold

    [Fact]
    public void DetectCaptures_KeyArtifactExists_AlwaysDetects()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 48, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(51), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished",
                    ["usage"] = "TOUCH"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);
        
        // 임계값 제거됨
        var options = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MinConfidenceThreshold = 0.0, // 임계값 제거됨 (핵심 아티팩트 존재 여부만 체크)
            ScreenshotPathPatterns = new[] { "/Screenshots/" },
            DownloadPathPatterns = new[] { "/Download/" }
        };

        // Act
        var captures = _strategy.DetectCaptures(context, options);

        // Assert
        captures.Should().HaveCount(1, "핵심 아티팩트(VIBRATION_EVENT)가 있으면 항상 탐지됨");
    }

    #endregion

    #region HapticType Parsing

    [Fact]
    public void DetectCaptures_VibrationEvent_HapticTypeAsString_ParsesCorrectly()
    {
        // Arrange: hapticType이 문자열로 전달되는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMilliseconds(660), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = "50061", // ✅ 문자열
                    ["status"] = "finished",
                    ["usage"] = "TOUCH"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "hapticType 문자열을 int로 파싱 성공");
        captures[0].PackageName.Should().Be("com.kakao.talk");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_HapticTypeInvalidString_Excluded()
    {
        // Arrange: hapticType이 파싱 불가능한 문자열
        var baseTime = new DateTime(2025, 10, 6, 22, 48, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(51), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = "invalid", // ❌ 파싱 불가
                    ["usage"] = "TOUCH"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("hapticType 파싱 실패 시 제외");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_HapticTypeNull_Excluded()
    {
        // Arrange: hapticType이 null
        var baseTime = new DateTime(2025, 10, 6, 22, 48, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(51), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = null!, // ❌ null
                    ["usage"] = "TOUCH"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("hapticType이 null이면 제외");
    }

    #endregion

    #region EventCorrelationWindow Tests

    [Fact]
    public void DetectCaptures_SupportingArtifact_OutsideWindow_Excluded()
    {
        // Arrange: 보조 증거가 correlation window 밖에 있음
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                }),
            
            // 보조 증거 (31초 전 - 윈도우 밖)
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(-31), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["uri"] = "content://test" }),
            
            // 보조 증거 (31초 후 - 윈도우 밖)
            CreateEvent(LogEventTypes.CAMERA_ACTIVITY_REFRESH, baseTime.AddSeconds(31), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["refreshRate"] = "120" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "주 증거는 탐지됨");
        captures[0].CaptureDetectionScore.Should().Be(0.4, "보조 증거가 윈도우 밖이므로 신뢰도 증가 없음");
    }

    [Fact]
    public void DetectCaptures_SupportingArtifact_InsideWindow_Included()
    {
        // Arrange: 보조 증거가 correlation window 안에 있음
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                }),
            
            // 보조 증거 (29초 전 - 윈도우 안)
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(-29), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["uri"] = "content://test" }),
            
            // 보조 증거 (29초 후 - 윈도우 안)
            CreateEvent(LogEventTypes.CAMERA_ACTIVITY_REFRESH, baseTime.AddSeconds(29), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["refreshRate"] = "120" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "주 증거 탐지");
        captures[0].CaptureDetectionScore.Should().BeGreaterThan(0.4, "보조 증거가 윈도우 안이므로 신뢰도 증가");
        captures[0].ArtifactTypes.Should().Contain(LogEventTypes.URI_PERMISSION_GRANT);
        captures[0].ArtifactTypes.Should().Contain(LogEventTypes.CAMERA_ACTIVITY_REFRESH);
    }

    [Fact]
    public void DetectCaptures_SupportingArtifact_ExactWindowBoundary_Included()
    {
        // Arrange: 보조 증거가 정확히 window 경계에 있음
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                }),
            
            // 보조 증거 (정확히 30초 전 - 경계)
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(-30), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["uri"] = "content://test1" }),
            
            // 보조 증거 (정확히 30초 후 - 경계)
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(30), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["uri"] = "content://test2" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].ArtifactTypes.Should().Contain(LogEventTypes.URI_PERMISSION_GRANT, 
            "정확히 경계에 있는 보조 증거도 포함 (>= windowStart && <= windowEnd)");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DetectCaptures_EmptyEventList_ReturnsEmpty()
    {
        // Arrange: 이벤트가 없는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 48, 0);
        var events = new List<NormalizedLogEvent>();
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("이벤트가 없으면 촬영도 없음");
    }

    [Fact]
    public void DetectCaptures_OnlyVibrationWithoutValidHapticType_ReturnsEmpty()
    {
        // Arrange: VIBRATION_EVENT만 있지만 hapticType이 다른 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 48, 0);
        var events = new List<NormalizedLogEvent>
        {
            // VIBRATION_EVENT이지만 hapticType이 다름
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(51), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50072, // ❌ 50061이 아님
                    ["status"] = "finished",
                    ["usage"] = "TOUCH"
                }),
            
            // PLAYER_EVENT (약한 증거, VIBRATION_EVENT와 다른 타입)
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime.AddSeconds(52), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("VIBRATION_EVENT (hapticType=50061)가 없으면 촬영 탐지 안 됨");
    }

    [Fact]
    public void DetectCaptures_ZeroCorrelationWindow_OnlyKeyArtifact()
    {
        // Arrange: correlation window가 0인 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                }),
            
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(1), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["uri"] = "content://test" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);
        
        var zeroWindowOptions = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.Zero, // ✅ 0초
            MinConfidenceThreshold = 0.4,
            ScreenshotPathPatterns = new[] { "/Screenshots/" },
            DownloadPathPatterns = new[] { "/Download/" }
        };

        // Act
        var captures = _strategy.DetectCaptures(context, zeroWindowOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].CaptureDetectionScore.Should().Be(0.4, "correlation window가 0이면 보조 증거 수집 안 됨");
    }

    #endregion

    #region Metadata and URI Extraction

    [Fact]
    public void DetectCaptures_Metadata_IsSetCorrectly()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMilliseconds(660), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished",
                    ["usage"] = "TOUCH",
                    ["duration"] = "125"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        var capture = captures[0];
        
        capture.Metadata.Should().ContainKey("detection_strategy");
        capture.Metadata["detection_strategy"].Should().Be("KakaoTalkStrategy");
        
        capture.Metadata.Should().ContainKey("key_artifact_type");
        capture.Metadata["key_artifact_type"].Should().Be("VIBRATION_EVENT (hapticType=50061)");
        
        capture.Metadata.Should().ContainKey("hapticType");
        capture.Metadata["hapticType"].Should().Be("50061");
        
        capture.Metadata.Should().ContainKey("status");
        capture.Metadata["status"].Should().Be("finished");
    }

    [Fact]
    public void DetectCaptures_UriExtraction_WithUriPermission_ExtractsUri()
    {
        // Arrange: URI_PERMISSION_GRANT가 있는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var expectedUri = "content://com.sec.android.app.camera.provider/external/image/media/12345";
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                }),
            
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddMilliseconds(100), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["uri"] = expectedUri })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].FileUri.Should().Be(expectedUri, "URI_PERMISSION_GRANT에서 URI 추출");
    }

    [Fact]
    public void DetectCaptures_UriExtraction_WithoutUriPermission_FileUriIsNull()
    {
        // Arrange: URI_PERMISSION_GRANT가 없는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].FileUri.Should().BeNull("URI_PERMISSION_GRANT가 없으면 URI도 없음");
    }

    [Fact]
    public void DetectCaptures_UriExtraction_UriPermissionWithoutUriAttribute_FileUriIsNull()
    {
        // Arrange: URI_PERMISSION_GRANT는 있지만 uri 속성이 없는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                }),
            
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddMilliseconds(100), 
                "com.kakao.talk",
                new Dictionary<string, object> { ["mode"] = "rw" }) // ❌ uri 속성 없음
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].FileUri.Should().BeNull("uri 속성이 없으면 URI 추출 실패");
    }

    #endregion

    #region Status Variations

    [Fact]
    public void DetectCaptures_VibrationEvent_StatusCancelledSuperseded_DetectsCapture()
    {
        // Arrange: status=cancelled_superseded (실제 샘플 5에서 발생)
        var baseTime = new DateTime(2025, 10, 7, 23, 16, 39);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMilliseconds(12), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "cancelled_superseded", // ✅ 다른 status
                    ["usage"] = "TOUCH"
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "status와 무관하게 hapticType=50061이면 탐지");
        captures[0].CaptureTime.Should().Be(baseTime.AddMilliseconds(12));
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_NoStatusAttribute_DetectsCapture()
    {
        // Arrange: status 속성이 아예 없는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMilliseconds(660), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["usage"] = "TOUCH"
                    // status 없음
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "status 속성 없어도 hapticType=50061이면 탐지");
    }

    #endregion

    #region Helper Methods

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

    private SessionContext CreateContext(
        DateTime startTime, 
        DateTime endTime, 
        string packageName,
        List<NormalizedLogEvent> events)
    {
        var session = new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = startTime,
            EndTime = endTime,
            PackageName = packageName,
            ProcessId = null,
            SourceLogTypes = new[] { "usagestats" },
            IncompleteReason = null,
            SessionCompletenessScore = 1.0,
            SourceEventIds = events.Select(e => e.EventId).ToList()
        };

        return new SessionContext
        {
            Session = session,
            AllEvents = events,
            ForegroundServices = Array.Empty<ForegroundServiceInfo>(),
        };
    }

    #endregion
}

