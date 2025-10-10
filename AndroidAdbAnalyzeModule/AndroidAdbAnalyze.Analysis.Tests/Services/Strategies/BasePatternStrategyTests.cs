using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Strategies;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Tests.Services.Strategies;

/// <summary>
/// BasePatternStrategy 단위 테스트
/// </summary>
public sealed class BasePatternStrategyTests
{
    private readonly BasePatternStrategy _strategy;
    private readonly IConfidenceCalculator _confidenceCalculator;
    private readonly AnalysisOptions _defaultOptions;

    public BasePatternStrategyTests()
    {
        _confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        _strategy = new BasePatternStrategy(
            NullLogger<BasePatternStrategy>.Instance,
            _confidenceCalculator);

        _defaultOptions = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MinConfidenceThreshold = 0.3,
            ScreenshotPathPatterns = new[] { "/Screenshots/", "/screenshot/" },
            DownloadPathPatterns = new[] { "/Download/", "/download/", "download" }
        };
    }

    [Fact]
    public void DetectCaptures_DatabaseInsert_DetectsCapture()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(30), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "DATABASE_INSERT는 확정 주 증거");
        captures[0].CaptureTime.Should().Be(baseTime.AddSeconds(30));
        captures[0].FilePath.Should().Be("/DCIM/Camera/IMG_001.jpg");
        captures[0].IsEstimated.Should().BeFalse();
    }

    [Fact]
    public void DetectCaptures_MediaInsertEnd_DetectsCapture()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.MEDIA_INSERT_END, baseTime.AddSeconds(30), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_002.jpg" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "MEDIA_INSERT_END는 확정 주 증거");
        captures[0].CaptureTime.Should().Be(baseTime.AddSeconds(30));
    }

    [Fact]
    public void DetectCaptures_PlayerEvent_WithPostProcess_DetectsCapture()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var foregroundServices = new List<ForegroundServiceInfo>
        {
            new ForegroundServiceInfo
            {
                ServiceClass = "com.samsung.android.camera.core2.processor.PostProcessService",
                StartTime = baseTime.AddSeconds(10),
                StopTime = baseTime.AddSeconds(50)
            }
        };

        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.PLAYER_CREATED, baseTime.AddSeconds(29), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["tags"] = ";CAMERA" 
                }),
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime.AddSeconds(30), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["event"] = "started" 
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera", events, foregroundServices);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "PLAYER_EVENT + PostProcessService는 조건부 주 증거");
        captures[0].CaptureTime.Should().Be(baseTime.AddSeconds(30));
    }

    [Fact]
    public void DetectCaptures_PlayerEvent_WithoutPostProcess_Excluded()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.PLAYER_CREATED, baseTime.AddSeconds(29), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["tags"] = ";CAMERA" 
                }),
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime.AddSeconds(30), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["event"] = "started" 
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("PostProcessService 없으면 PLAYER_EVENT 단독은 제외");
    }

    [Fact]
    public void DetectCaptures_UriPermission_TempPath_DetectsCapture()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(30), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["uri"] = "content://com.kakao.talk/tmp/temp_image_12345.jpg" 
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "임시 경로 URI_PERMISSION은 조건부 주 증거");
        captures[0].FileUri.Should().Contain("/tmp/");
    }

    [Fact]
    public void DetectCaptures_UriPermission_AlbumPath_Excluded()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddSeconds(30), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["uri"] = "content://media/external/images/media/12345" 
                })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.kakao.talk", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("앨범 경로 URI_PERMISSION은 제외");
    }

    [Fact]
    public void DetectCaptures_SilentCameraCapture_DetectsCapture()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 58, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.SILENT_CAMERA_CAPTURE, baseTime.AddSeconds(27), 
                "com.peace.SilentCamera")
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(1), "com.peace.SilentCamera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "SILENT_CAMERA_CAPTURE는 조건부 주 증거");
        captures[0].CaptureTime.Should().Be(baseTime.AddSeconds(27));
    }

    [Fact]
    public void DetectCaptures_ScreenshotPath_Excluded()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(10), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" }),
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(20), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/Screenshots/Screenshot_001.png" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "스크린샷 경로는 제외");
        captures[0].FilePath.Should().Contain("/DCIM/Camera/");
    }

    [Fact]
    public void DetectCaptures_DownloadPath_Excluded()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(10), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" }),
            CreateEvent(LogEventTypes.DATABASE_EVENT, baseTime.AddSeconds(20), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/Download/image_download.jpg" })
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "다운로드 경로는 제외");
        captures[0].FilePath.Should().Contain("/DCIM/Camera/");
    }

    [Fact]
    public void DetectCaptures_BelowConfidenceThreshold_Excluded()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(10), "com.sec.android.app.camera")
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera", events);

        var options = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MinConfidenceThreshold = 0.9, // 매우 높은 임계값
            ScreenshotPathPatterns = Array.Empty<string>(),
            DownloadPathPatterns = Array.Empty<string>()
        };

        // Act
        var captures = _strategy.DetectCaptures(context, options);

        // Assert
        captures.Should().BeEmpty("신뢰도가 임계값 미만이므로 제외");
    }

    [Fact]
    public void DetectCaptures_MultipleCaptures_ReturnsAll()
    {
        // Arrange
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddSeconds(30), "com.sec.android.app.camera"),
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime.AddMinutes(2), "com.sec.android.app.camera"),
            CreateEvent(LogEventTypes.MEDIA_INSERT_END, baseTime.AddMinutes(4), "com.sec.android.app.camera")
        };
        var context = CreateContext(baseTime, baseTime.AddMinutes(10), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(3, "3개의 주 증거가 있으므로 3개 감지");
        captures.Select(c => c.CaptureTime).Should().BeInAscendingOrder("시간순 정렬");
    }

    [Fact]
    public void PackageNamePattern_ReturnsNull()
    {
        // Act
        var pattern = _strategy.PackageNamePattern;

        // Assert
        pattern.Should().BeNull("BaseStrategy는 fallback 전략으로 모든 패키지에 적용");
    }

    [Fact]
    public void Priority_ReturnsZero()
    {
        // Act
        var priority = _strategy.Priority;

        // Assert
        priority.Should().Be(0, "BaseStrategy는 가장 낮은 우선순위");
    }

    #region VIBRATION_EVENT Tests

    [Fact]
    public void DetectCaptures_VibrationEvent_HapticType50061_StatusFinished_DetectsCapture()
    {
        // Arrange: hapticType=50061, status=finished (정상 촬영)
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "hapticType=50061, status=finished는 조건부 주 증거");
        captures[0].CaptureTime.Should().Be(baseTime);
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.VIBRATION_EVENT);
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_HapticTypeAsString_ParsesCorrectly()
    {
        // Arrange: hapticType이 문자열로 전달되는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = "50061", // 문자열
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "hapticType 문자열을 int로 파싱 성공");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_InvalidHapticType_Excluded()
    {
        // Arrange: hapticType=50072 (일반 UI 터치)
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50072, // 촬영 아님
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("hapticType=50072는 촬영 버튼이 아님");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_NoHapticType_Excluded()
    {
        // Arrange: hapticType 속성이 없는 경우
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("hapticType 속성이 없으면 제외");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_StatusCancelled_Excluded()
    {
        // Arrange: status=cancelled_superseded (취소된 진동)
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "cancelled_superseded"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("취소된 진동은 제외");
    }

    [Fact]
    public void DetectCaptures_VibrationEvent_HapticTypeParsingFailure_Excluded()
    {
        // Arrange: hapticType 파싱 실패
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = "invalid_string",
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("hapticType 파싱 실패 시 제외");
    }

    #endregion

    #region Time Window Deduplication Tests

    [Fact]
    public void DetectCaptures_MultipleEvidencesWithinTimeWindow_SelectsBestByPriority()
    {
        // Arrange: 1초 이내에 VIBRATION_EVENT + PLAYER_EVENT 발생
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var foregroundServices = new List<ForegroundServiceInfo>
        {
            new ForegroundServiceInfo
            {
                ServiceClass = "com.samsung.android.camera.core2.processor.PostProcessService",
                StartTime = baseTime.AddSeconds(-10),
                StopTime = baseTime.AddSeconds(50)
            }
        };

        var events = new List<NormalizedLogEvent>
        {
            // VIBRATION_EVENT (우선순위 100)
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                }),
            // PLAYER_EVENT (우선순위 80)
            CreateEvent(LogEventTypes.PLAYER_CREATED, baseTime.AddMilliseconds(100), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["tags"] = ";CAMERA" 
                }),
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime.AddMilliseconds(200), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["event"] = "started" 
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events, foregroundServices);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "시간 윈도우 내 중복 제거로 1개만 남음");
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.VIBRATION_EVENT, "VIBRATION_EVENT가 우선순위 최고");
    }

    [Fact]
    public void DetectCaptures_MultipleEvidencesOutsideTimeWindow_DetectsAll()
    {
        // Arrange: 30초 이상 간격으로 VIBRATION_EVENT 2개 발생 (별도 촬영)
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                }),
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddSeconds(31), // 31초 후
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(2), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(2, "시간 윈도우 밖이므로 별도 촬영으로 인식");
        captures[0].CaptureTime.Should().Be(baseTime);
        captures[1].CaptureTime.Should().Be(baseTime.AddSeconds(31));
    }

    [Fact]
    public void DetectCaptures_PriorityOrder_VibrationOverPlayerEvent()
    {
        // Arrange: VIBRATION_EVENT와 PLAYER_EVENT가 거의 동시 발생
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var foregroundServices = new List<ForegroundServiceInfo>
        {
            new ForegroundServiceInfo
            {
                ServiceClass = "com.samsung.android.camera.core2.processor.PostProcessService",
                StartTime = baseTime.AddSeconds(-10),
                StopTime = baseTime.AddSeconds(50)
            }
        };

        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.PLAYER_CREATED, baseTime.AddMilliseconds(-100), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["tags"] = ";CAMERA" 
                }),
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime, // PLAYER_EVENT 먼저
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["event"] = "started" 
                }),
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMilliseconds(500), // VIBRATION_EVENT 나중
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events, foregroundServices);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.VIBRATION_EVENT, 
            "우선순위: VIBRATION_EVENT(100) > PLAYER_EVENT(80)");
    }

    [Fact]
    public void DetectCaptures_PriorityOrder_PlayerEventOverUriPermission()
    {
        // Arrange: PLAYER_EVENT와 URI_PERMISSION_GRANT가 거의 동시 발생
        var baseTime = new DateTime(2025, 10, 6, 22, 49, 56);
        var foregroundServices = new List<ForegroundServiceInfo>
        {
            new ForegroundServiceInfo
            {
                ServiceClass = "com.samsung.android.camera.core2.processor.PostProcessService",
                StartTime = baseTime.AddSeconds(-10),
                StopTime = baseTime.AddSeconds(50)
            }
        };

        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.PLAYER_CREATED, baseTime.AddMilliseconds(-100), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["tags"] = ";CAMERA" 
                }),
            CreateEvent(LogEventTypes.PLAYER_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["piid"] = 311, 
                    ["event"] = "started" 
                }),
            CreateEvent(LogEventTypes.URI_PERMISSION_GRANT, baseTime.AddMilliseconds(300), 
                "com.kakao.talk",
                new Dictionary<string, object> 
                { 
                    ["uri"] = "content://com.kakao.talk/tmp/temp_image_12345.jpg" 
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events, foregroundServices);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.PLAYER_EVENT, 
            "우선순위: PLAYER_EVENT(80) > URI_PERMISSION_GRANT(60)");
    }

    #endregion

    #region Primary vs Conditional Evidence Tests

    [Fact]
    public void DetectCaptures_PrimaryAndConditionalEvidence_PrimaryTakesPriority()
    {
        // Arrange: 확정 주 증거(DATABASE_INSERT)와 조건부 주 증거(VIBRATION_EVENT)가 모두 존재
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            // 확정 주 증거
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" }),
            // 조건부 주 증거
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime.AddMilliseconds(500), 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["hapticType"] = 50061,
                    ["status"] = "finished"
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1, "확정 주 증거가 있으면 조건부 주 증거는 무시");
        captures[0].EvidenceTypes.Should().Contain(LogEventTypes.DATABASE_INSERT);
    }

    #endregion

    #region EventCorrelationWindow Tests

    [Fact]
    public void DetectCaptures_SupportingEvidence_WithinCorrelationWindow_Collected()
    {
        // Arrange: 주 증거 기준 ±30초 내 보조 증거 수집
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" }),
            // 보조 증거 (윈도우 내)
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(-29), "com.sec.android.app.camera"), // -29초
            CreateEvent(LogEventTypes.SHUTTER_SOUND, baseTime.AddSeconds(29), "com.sec.android.app.camera")  // +29초
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].SupportingEvidenceIds.Should().HaveCount(2, "±30초 내 보조 증거 수집");
    }

    [Fact]
    public void DetectCaptures_SupportingEvidence_OutsideCorrelationWindow_NotCollected()
    {
        // Arrange: 주 증거 기준 ±30초 밖 보조 증거
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" }),
            // 보조 증거 (윈도우 밖)
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(-31), "com.sec.android.app.camera"), // -31초
            CreateEvent(LogEventTypes.SHUTTER_SOUND, baseTime.AddSeconds(31), "com.sec.android.app.camera")  // +31초
        };
        var context = CreateContext(baseTime.AddMinutes(-2), baseTime.AddMinutes(2), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].SupportingEvidenceIds.Should().BeEmpty("±30초 밖 보조 증거는 수집 안됨");
    }

    [Fact]
    public void DetectCaptures_SupportingEvidence_ExactlyOnWindowBoundary_Collected()
    {
        // Arrange: 정확히 ±30초 경계에 있는 보조 증거
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            // 주 증거
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" }),
            // 보조 증거 (정확히 경계)
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddSeconds(-30), "com.sec.android.app.camera"), // -30초
            CreateEvent(LogEventTypes.SHUTTER_SOUND, baseTime.AddSeconds(30), "com.sec.android.app.camera")  // +30초
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].SupportingEvidenceIds.Should().HaveCount(2, "경계값도 포함");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DetectCaptures_EmptyEventList_ReturnsEmpty()
    {
        // Arrange: 이벤트가 없는 세션
        var baseTime = new DateTime(2025, 10, 6, 22, 46, 0);
        var context = CreateContext(baseTime, baseTime.AddMinutes(5), "com.sec.android.app.camera", new List<NormalizedLogEvent>());

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("이벤트가 없으면 촬영 감지 불가");
    }

    [Fact]
    public void DetectCaptures_ZeroCorrelationWindow_NoSupportingEvidences()
    {
        // Arrange: EventCorrelationWindow = 0
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.DATABASE_INSERT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> { ["file_path"] = "/DCIM/Camera/IMG_001.jpg" }),
            CreateEvent(LogEventTypes.VIBRATION, baseTime.AddMilliseconds(1), "com.sec.android.app.camera")
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        var options = new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.Zero, // 0초
            MinConfidenceThreshold = 0.3,
            ScreenshotPathPatterns = Array.Empty<string>(),
            DownloadPathPatterns = Array.Empty<string>()
        };

        // Act
        var captures = _strategy.DetectCaptures(context, options);

        // Assert
        captures.Should().HaveCount(1);
        captures[0].SupportingEvidenceIds.Should().BeEmpty("EventCorrelationWindow=0이면 보조 증거 수집 안됨");
    }

    [Fact]
    public void DetectCaptures_OnlyConditionalEvidence_NoValidation_ReturnsEmpty()
    {
        // Arrange: 조건부 주 증거만 있지만 검증 실패 (hapticType 없음)
        var baseTime = new DateTime(2025, 10, 6, 22, 47, 45);
        var events = new List<NormalizedLogEvent>
        {
            CreateEvent(LogEventTypes.VIBRATION_EVENT, baseTime, 
                "com.sec.android.app.camera",
                new Dictionary<string, object> 
                { 
                    ["status"] = "finished" // hapticType 없음
                })
        };
        var context = CreateContext(baseTime.AddMinutes(-1), baseTime.AddMinutes(1), "com.sec.android.app.camera", events);

        // Act
        var captures = _strategy.DetectCaptures(context, _defaultOptions);

        // Assert
        captures.Should().BeEmpty("조건부 주 증거 검증 실패 시 촬영 미탐지");
    }

    #endregion

    #region Helper Methods

    private SessionContext CreateContext(
        DateTime startTime, 
        DateTime? endTime, 
        string packageName,
        List<NormalizedLogEvent>? events = null,
        List<ForegroundServiceInfo>? foregroundServices = null)
    {
        var session = new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = startTime,
            EndTime = endTime,
            PackageName = packageName,
            SourceLogTypes = new[] { "media.camera" },
            ConfidenceScore = 0.8,
            SourceEventIds = Array.Empty<Guid>()
        };

        return new SessionContext
        {
            Session = session,
            AllEvents = events ?? new List<NormalizedLogEvent>(),
            ActivityResumedTime = null,
            ActivityPausedTime = null,
            ForegroundServices = foregroundServices ?? new List<ForegroundServiceInfo>(),
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