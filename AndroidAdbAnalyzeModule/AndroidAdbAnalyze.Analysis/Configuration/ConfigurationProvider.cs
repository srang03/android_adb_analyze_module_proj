using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Configuration;

/// <summary>
/// 아티팩트 탐지 설정 제공자
/// </summary>
/// <remarks>
/// 기본 설정값을 제공하며, 향후 YAML 파일 로드 기능을 추가할 수 있습니다.
/// Backward Compatibility를 보장하기 위해 기존 하드코딩된 값들을 기본값으로 제공합니다.
/// </remarks>
public static class ConfigurationProvider
{
    /// <summary>
    /// 기본 설정 반환
    /// </summary>
    /// <remarks>
    /// 기존 하드코딩된 값들을 Configuration 객체로 변환하여 반환합니다.
    /// 테스트 코드 및 기존 코드 호환성을 보장합니다.
    /// </remarks>
    public static ArtifactDetectionConfig GetDefault()
    {
        return new ArtifactDetectionConfig
        {
            ArtifactWeights = GetDefaultArtifactWeights(),
            Strategies = GetDefaultStrategies(),
            Validation = GetDefaultValidation(),
            AnalysisOptions = GetDefaultAnalysisOptions()
        };
    }
    
    /// <summary>
    /// 기본 아티팩트 가중치 반환
    /// </summary>
    /// <remarks>
    /// 기존 ConfidenceCalculator의 EventTypeWeights와 동일한 값들입니다.
    /// </remarks>
    private static ArtifactWeightsConfig GetDefaultArtifactWeights()
    {
        return new ArtifactWeightsConfig
        {
            Session = new Dictionary<string, double>
            {
                // usagestats 세션: 완전(0.95) = RESUMED(0.7) + STOPPED/PAUSED(0.25)
                { LogEventTypes.ACTIVITY_RESUMED, 0.7 },
                { LogEventTypes.ACTIVITY_STOPPED, 0.25 },
                { LogEventTypes.ACTIVITY_PAUSED, 0.25 },
                
                // media.camera 세션: 완전(0.85) = CONNECT(0.6) + DISCONNECT(0.25)
                { LogEventTypes.CAMERA_CONNECT, 0.6 },
                { LogEventTypes.CAMERA_DISCONNECT, 0.25 }
            },
            Capture = new Dictionary<string, double>
            {
                // Critical 증거 (0.5): 직접성 90~100% - 파일 저장 확정
                { LogEventTypes.DATABASE_INSERT, 0.5 },
                { LogEventTypes.DATABASE_EVENT, 0.5 },
                { LogEventTypes.SILENT_CAMERA_CAPTURE, 0.5 },
                
                // Strong 증거 (0.35~0.4): 직접성 70~85% - HAL 레벨, 햅틱
                { LogEventTypes.VIBRATION_EVENT, 0.4 },
                { LogEventTypes.PLAYER_EVENT, 0.35 }, // tags=CAMERA 검증 필요
                
                // Medium 증거 (0.25~0.3): 직접성 50~65% - 권한, Activity, Foreground Service
            { LogEventTypes.FOREGROUND_SERVICE, 0.3 }, // 추가 (2025-10-26): usagestats 기반, 재부팅 휘발성 대응
            { LogEventTypes.URI_PERMISSION_GRANT, 0.3 },
            { LogEventTypes.PLAYER_CREATED, 0.25 },
            { LogEventTypes.URI_PERMISSION_REVOKE, 0.22 }, // 하향 조정 (2025-10-28): Exclusivity High → Medium
                
                // Supporting 증거 (0.15~0.2): 직접성 35~45% - 보조 이벤트
                { LogEventTypes.SHUTTER_SOUND, 0.2 },
                { LogEventTypes.MEDIA_EXTRACTOR, 0.2 },
                { LogEventTypes.PLAYER_RELEASED, 0.15 },
                { LogEventTypes.CAMERA_ACTIVITY_REFRESH, 0.15 }
            }
        };
    }
    
    /// <summary>
    /// 기본 Strategy 설정 반환
    /// </summary>
    /// <remarks>
    /// 기존 BasePatternStrategy, TelegramStrategy, KakaoTalkStrategy의 값들과 동일합니다.
    /// </remarks>
    private static Dictionary<string, StrategyConfig> GetDefaultStrategies()
    {
        return new Dictionary<string, StrategyConfig>
        {
            {
                "base_pattern", new StrategyConfig
                {
                    PackagePattern = null, // fallback
                    KeyArtifacts = new List<string>
                    {
                        LogEventTypes.DATABASE_INSERT,
                        LogEventTypes.DATABASE_EVENT
                    },
                    ConditionalKeyArtifacts = new List<string>
                    {
                        LogEventTypes.VIBRATION_EVENT,
                        LogEventTypes.PLAYER_EVENT, // 승격 (2025-10-28): 조건부 핵심으로 승격, tags=CAMERA 검증 필요
                        LogEventTypes.URI_PERMISSION_GRANT,
                        LogEventTypes.SILENT_CAMERA_CAPTURE,
                        LogEventTypes.FOREGROUND_SERVICE // 추가 (2025-10-26): usagestats 기반, 재부팅 휘발성 대응
                    },
                    SupportingArtifacts = new List<string>
                    {
                        LogEventTypes.PLAYER_CREATED,
                        LogEventTypes.PLAYER_EVENT,
                        LogEventTypes.PLAYER_RELEASED,
                        LogEventTypes.MEDIA_EXTRACTOR,
                        LogEventTypes.SHUTTER_SOUND,
                        LogEventTypes.VIBRATION_EVENT,
                        LogEventTypes.URI_PERMISSION_GRANT,
                        LogEventTypes.CAMERA_ACTIVITY_REFRESH,
                        LogEventTypes.FOREGROUND_SERVICE // 추가 (2025-10-26)
                    }
                }
            },
            {
                "telegram", new StrategyConfig
                {
                    PackagePattern = "org.telegram.messenger",
                    KeyArtifacts = new List<string>
                    {
                        LogEventTypes.DATABASE_INSERT,
                        LogEventTypes.DATABASE_EVENT
                    },
                    ConditionalKeyArtifacts = new List<string>
                    {
                        LogEventTypes.VIBRATION_EVENT,
                        LogEventTypes.URI_PERMISSION_GRANT,
                        LogEventTypes.SILENT_CAMERA_CAPTURE
                        // PLAYER_EVENT 제외 (TelegramStrategy 특징)
                    },
                    SupportingArtifacts = new List<string>
                    {
                        LogEventTypes.PLAYER_CREATED,
                        // LogEventTypes.PLAYER_EVENT,  // 제외됨!
                        LogEventTypes.PLAYER_RELEASED,
                        LogEventTypes.MEDIA_EXTRACTOR,
                        LogEventTypes.SHUTTER_SOUND,
                        LogEventTypes.VIBRATION_EVENT,
                        LogEventTypes.URI_PERMISSION_GRANT,
                        LogEventTypes.CAMERA_ACTIVITY_REFRESH
                    }
                }
            },
            {
                "kakao_talk", new StrategyConfig
                {
                    PackagePattern = "com.kakao.talk",
                    KeyArtifacts = new List<string>
                    {
                        LogEventTypes.VIBRATION_EVENT // hapticType=50061 (촬영 버튼 - 필수)
                    },
                    ConditionalKeyArtifacts = new List<string>
                    {
                        LogEventTypes.URI_PERMISSION_GRANT,      // 임시 파일 경로 (Secondary)
                        LogEventTypes.CAMERA_ACTIVITY_REFRESH    // activity.log (Secondary)
                    },
                    SupportingArtifacts = new List<string>
                    {
                        LogEventTypes.PLAYER_CREATED,
                        LogEventTypes.PLAYER_RELEASED,
                        LogEventTypes.MEDIA_EXTRACTOR,
                        LogEventTypes.VIBRATION_EVENT,
                        LogEventTypes.URI_PERMISSION_GRANT,
                        LogEventTypes.CAMERA_ACTIVITY_REFRESH,
                        LogEventTypes.FOREGROUND_SERVICE // 추가 (2025-10-26)
                    }
                }
            }
        };
    }
    
    /// <summary>
    /// 기본 검증 상수 반환
    /// </summary>
    /// <remarks>
    /// 기존 BasePatternStrategy의 상수값들과 동일합니다.
    /// </remarks>
    private static ValidationConstantsConfig GetDefaultValidation()
    {
        return new ValidationConstantsConfig
        {
            HapticTypeCameraShutter = 50061,
            PlayerEventStateStarted = "started",
            PlayerTagCamera = "CAMERA",
            ServiceClassPostProcess = "PostProcessService",
            // 추가 (2025-10-26): usagestats 기반 촬영 탐지 서비스 클래스
            ServiceClassCaptureConfirmed = new List<string>
            {
                "PostProcessService"
            },
            ServiceClassCapturePossible = new List<string>
            {
                "NotificationService"
            }
        };
    }
    
    /// <summary>
    /// 기본 분석 옵션 반환
    /// </summary>
    /// <remarks>
    /// 기존 AnalysisOptions의 기본값들과 동일합니다.
    /// </remarks>
    private static AnalysisOptionsConfig GetDefaultAnalysisOptions()
    {
        return new AnalysisOptionsConfig
        {
            Thresholds = new ThresholdsConfig
            {
                MinConfidence = 0.3,
                DeduplicationSimilarity = 0.8
            },
            TimeWindows = new TimeWindowsConfig
            {
                MaxSessionGapMinutes = 5,
                EventCorrelationSeconds = 30,
                CaptureDeduplicationSeconds = 1
            },
            Packages = new PackagesConfig
            {
                Whitelist = null,
                Blacklist = new List<string>()
            },
            PathPatterns = new PathPatternsConfig
            {
                Screenshot = new List<string>
                {
                    "/Screenshots/",
                    "/screenshot/",
                    "Screenshot_"
                },
                Download = new List<string>
                {
                    "/Download/",
                    "/download/",
                    "Download_"
                }
            }
        };
    }
}

