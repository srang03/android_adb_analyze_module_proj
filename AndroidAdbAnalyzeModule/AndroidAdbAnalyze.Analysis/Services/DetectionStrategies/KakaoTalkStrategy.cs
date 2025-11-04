using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;

/// <summary>
/// KakaoTalk 전용 촬영 탐지 전략
/// </summary>
/// <remarks>
/// KakaoTalk은 다음과 같은 특징이 있습니다:
/// 
/// 1. 시스템 카메라 API 사용 (package=com.sec.android.app.camera, taskRootPackage=com.kakao.talk)
/// 2. PLAYER_EVENT 없음 (셔터 음 재생하지 않음)
/// 3. URI_PERMISSION_GRANT 발생 (임시 파일 경로)
/// 4. VIBRATION_EVENT (hapticType=50061) 발생
/// 5. CAMERA_ACTIVITY_REFRESH 발생 가능
/// 
/// 따라서:
/// - VIBRATION_EVENT (hapticType=50061)를 핵심 아티팩트로 사용
/// - URI_PERMISSION_GRANT, CAMERA_ACTIVITY_REFRESH를 보조 아티팩트로 사용
/// - BaseStrategy의 PLAYER_EVENT 의존성 제거
/// 
/// 적용 패키지:
/// - com.kakao.talk
/// </remarks>
public sealed class KakaoTalkStrategy : BaseCaptureDetectionStrategy
{
    // ⚠️ static readonly → 인스턴스 필드로 변경
    // 기존 값들은 ConfigurationProvider.GetDefault()에서 제공됩니다.
    
    private readonly HashSet<string> _keyArtifactTypes;
    private readonly HashSet<string> _secondaryArtifactTypes; // ConditionalKeyArtifacts에서 로드
    private readonly HashSet<string> _supportingArtifactTypes;
    private readonly ValidationConstantsConfig _validation;

    /// <summary>
    /// 기본 생성자 (Backward Compatibility 보장)
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="confidenceCalculator">신뢰도 계산기</param>
    /// <remarks>
    /// 기존 테스트 코드 호환성을 위해 유지됩니다.
    /// 내부적으로 ConfigurationProvider.GetDefault()를 사용하여 기본값을 제공합니다.
    /// </remarks>
    public KakaoTalkStrategy(
        ILogger<KakaoTalkStrategy> logger,
        IConfidenceCalculator confidenceCalculator)
        : this(logger, confidenceCalculator, ConfigurationProvider.GetDefault())
    {
    }

    /// <summary>
    /// Configuration 주입 생성자
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="confidenceCalculator">신뢰도 계산기</param>
    /// <param name="config">아티팩트 탐지 설정</param>
    /// <remarks>
    /// DI 컨테이너에서 Configuration을 주입받아 동적으로 아티팩트 분류를 설정합니다.
    /// YAML 파일 기반 설정 변경이 가능합니다.
    /// </remarks>
    public KakaoTalkStrategy(
        ILogger<KakaoTalkStrategy> logger,
        IConfidenceCalculator confidenceCalculator,
        ArtifactDetectionConfig config)
        : base(logger, confidenceCalculator)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        var strategyConfig = config.Strategies.TryGetValue("kakao_talk", out var cfg)
            ? cfg
            : throw new InvalidOperationException("Configuration에 'kakao_talk' 전략이 정의되지 않았습니다.");
        
        _keyArtifactTypes = new HashSet<string>(strategyConfig.KeyArtifacts);
        _secondaryArtifactTypes = new HashSet<string>(strategyConfig.ConditionalKeyArtifacts); // Secondary로 사용
        _supportingArtifactTypes = new HashSet<string>(strategyConfig.SupportingArtifacts);
        _validation = config.Validation;
        
        _logger.LogInformation(
            "[KakaoTalkStrategy] 초기화 완료: 핵심 {Key}개, Secondary {Secondary}개, 보조 {Supporting}개",
            _keyArtifactTypes.Count,
            _secondaryArtifactTypes.Count,
            _supportingArtifactTypes.Count);
    }

    /// <inheritdoc/>
    public override string? PackageNamePattern => "com.kakao.talk";

    /// <summary>
    /// 핵심 아티팩트 검색 (Template Method 구현)
    /// </summary>
    /// <remarks>
    /// KakaoTalk은 VIBRATION_EVENT (hapticType=50061)를 우선 사용하며,
    /// 재부팅 휘발성 대응을 위해 FOREGROUND_SERVICE를 Fallback으로 사용합니다.
    /// </remarks>
    protected override List<NormalizedLogEvent> GetKeyArtifacts(
        SessionContext context,
        AnalysisOptions options)
    {
        // 1️⃣ 주 검증 수단: VIBRATION_EVENT (hapticType=50061)
        var vibrationEvents = context.AllEvents
            .Where(e => _keyArtifactTypes.Contains(e.EventType))
            .Where(e => ValidateVibrationEventAsShutter(e))
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (vibrationEvents.Count > 0)
        {
            _logger.LogInformation(
                "[KakaoTalkStrategy] Session {SessionId} ({Package}): VIBRATION_EVENT (hapticType=50061) {Count}개",
                context.Session.SessionId, context.Session.PackageName, vibrationEvents.Count);
            return vibrationEvents;
        }

        // 2️⃣ Fallback: FOREGROUND_SERVICE (재부팅 휘발성 대응)
        _logger.LogWarning(
            "[KakaoTalkStrategy] Session {SessionId}: VIBRATION_EVENT 없음 → FOREGROUND_SERVICE Fallback 시도",
            context.Session.SessionId);

        var foregroundServices = context.AllEvents
            .Where(e => e.EventType == LogEventTypes.FOREGROUND_SERVICE)
            .Where(e => ValidateForegroundServiceForKakao(e, context))
            .OrderBy(e => e.Timestamp)
            .ToList();

        _logger.LogInformation(
            "[KakaoTalkStrategy] Session {SessionId}: FOREGROUND_SERVICE {Count}개 (재부팅 대응)",
            context.Session.SessionId, foregroundServices.Count);

        return foregroundServices;
    }

    /// <summary>
    /// 보조 아티팩트 타입 정의 (Template Method 구현)
    /// </summary>
    protected override HashSet<string> GetSupportingArtifactTypes()
    {
        // Secondary + Supporting 통합
        return new HashSet<string>(_secondaryArtifactTypes.Union(_supportingArtifactTypes));
    }

    /// <summary>
    /// CameraCaptureEvent 생성 오버라이드 (FileUri 추출 로직 추가)
    /// </summary>
    protected override CameraCaptureEvent CreateCaptureEvent(
        CameraSession session,
        NormalizedLogEvent keyArtifact,
        List<NormalizedLogEvent> supportingArtifacts,
        List<NormalizedLogEvent> allArtifacts,
        double score)
    {
        // URI 추출 (보조 아티팩트에서 URI_PERMISSION_GRANT 찾기)
        string? fileUri = null;
        var uriPermissionArtifact = supportingArtifacts.FirstOrDefault(e =>
            e.EventType == LogEventTypes.URI_PERMISSION_GRANT);
        if (uriPermissionArtifact != null &&
            uriPermissionArtifact.Attributes.TryGetValue("uri", out var uriObj))
        {
            fileUri = uriObj?.ToString();
        }

        // 증거 타입 목록
        var artifactTypes = allArtifacts
            .Select(e => e.EventType)
            .Distinct()
            .ToList();

        // 메타데이터 수집
        var metadata = new Dictionary<string, string>
        {
            ["detection_strategy"] = GetType().Name,
            ["key_artifact_type"] = "VIBRATION_EVENT (hapticType=50061)"
        };

        foreach (var attr in keyArtifact.Attributes)
        {
            if (attr.Value != null)
                metadata[attr.Key] = attr.Value.ToString() ?? string.Empty;
        }

        return new CameraCaptureEvent
        {
            CaptureId = Guid.NewGuid(),
            ParentSessionId = session.SessionId,
            CaptureTime = keyArtifact.Timestamp,
            PackageName = session.PackageName,
            FilePath = null, // KakaoTalk은 파일 경로 정보 없음 (임시 파일 사용)
            FileUri = fileUri,
            decisiveArtifact = keyArtifact.EventId,
            SupportingArtifactIds = supportingArtifacts.Select(e => e.EventId).ToList(),
            IsEstimated = false,
            CaptureDetectionScore = score,
            ArtifactTypes = artifactTypes,
            SourceEventIds = allArtifacts.Select(e => e.EventId).ToList(),
            Metadata = metadata
        };
    }

    /// <summary>
    /// VIBRATION_EVENT를 촬영 버튼 진동으로 검증 (hapticType=50061)
    /// </summary>
    /// <remarks>
    /// KakaoTalk이 기본 카메라를 호출할 때 발생하는 촬영 버튼 진동 이벤트를 감지합니다.
    /// hapticType=50061: 촬영 버튼 터치 (실제 촬영)
    /// </remarks>
    private bool ValidateVibrationEventAsShutter(NormalizedLogEvent artifact)
    {
        if (!artifact.Attributes.TryGetValue("hapticType", out var hapticTypeObj))
        {
            _logger.LogTrace(
                "[KakaoTalkStrategy] VIBRATION_EVENT 제외: hapticType 정보 없음");
            return false;
        }

        // hapticType 값 추출 (int 또는 string일 수 있음)
        int hapticType;
        if (hapticTypeObj is int hapticTypeInt)
        {
            hapticType = hapticTypeInt;
        }
        else if (int.TryParse(hapticTypeObj?.ToString(), out var parsed))
        {
            hapticType = parsed;
        }
        else
        {
            _logger.LogTrace(
                "[KakaoTalkStrategy] VIBRATION_EVENT 제외: hapticType 파싱 실패 (value={Value})",
                hapticTypeObj);
            return false;
        }

        if (hapticType != _validation.HapticTypeCameraShutter)
        {
            _logger.LogTrace(
                "[KakaoTalkStrategy] VIBRATION_EVENT 제외: hapticType={HapticType} (설정값 아님)",
                hapticType);
            return false;
        }

        _logger.LogDebug(
            "[KakaoTalkStrategy] ✅ VIBRATION_EVENT 승인: hapticType={HapticType} (촬영 버튼), Time={Time:HH:mm:ss.fff}",
            _validation.HapticTypeCameraShutter, artifact.Timestamp);
        return true;
    }

    /// <summary>
    /// FOREGROUND_SERVICE를 카카오톡 촬영으로 검증 (재부팅 휘발성 대응용)
    /// </summary>
    /// <remarks>
    /// 재부팅 후 VIBRATION_EVENT가 휘발되었을 때의 대체 탐지 수단입니다.
    /// 카카오톡은 기본 카메라를 호출하므로 com.sec.android.app.camera의 NotificationService를 체크합니다.
    /// 
    /// 검증 조건:
    /// 1. serviceState == "FOREGROUND_SERVICE_START"
    /// 2. className에 "NotificationService" 포함 (카카오톡 특화)
    /// 
    /// 리팩토링 (2025-10-26): Phase 2 - ForegroundServiceValidator 헬퍼 사용
    /// </remarks>
    /// <param name="artifact">FOREGROUND_SERVICE 이벤트</param>
    /// <param name="context">세션 컨텍스트 (미사용, 인터페이스 일관성 유지)</param>
    /// <returns>카카오톡 촬영 서비스면 true, 아니면 false</returns>
    private bool ValidateForegroundServiceForKakao(
        NormalizedLogEvent artifact,
        SessionContext context)
    {
        // 1단계: serviceState 확인 (START만 허용) - 헬퍼 사용
        if (!Services.Validation.ForegroundServiceValidator.IsValidStartEvent(artifact))
        {
            var serviceState = artifact.Attributes.GetValueOrDefault("serviceState")?.ToString();
            _logger.LogTrace(
                "[KakaoTalkStrategy] FOREGROUND_SERVICE 제외: serviceState={State} (START만 허용)",
                serviceState);
            return false;
        }

        // 2단계: className 추출 - 헬퍼 사용
        var className = Services.Validation.ForegroundServiceValidator.ExtractClassName(artifact);
        if (string.IsNullOrEmpty(className))
        {
            _logger.LogTrace(
                "[KakaoTalkStrategy] FOREGROUND_SERVICE 제외: className 없음");
            return false;
        }

        // 3단계: NotificationService 체크 (카카오톡 특화) - 헬퍼 사용
        // 카카오톡은 기본 카메라를 호출 → com.sec.android.app.camera의 NotificationService 사용
        var isKakaoService = Services.Validation.ForegroundServiceValidator.MatchesAnyPattern(
            className, 
            new[] { "NotificationService" });

        if (isKakaoService)
        {
            _logger.LogDebug(
                "[KakaoTalkStrategy] ✅ FOREGROUND_SERVICE 승인: className={ClassName} (카카오톡 촬영 알림 서비스), Time={Time:HH:mm:ss.fff}",
                className, artifact.Timestamp);
        }
        else
        {
            _logger.LogTrace(
                "[KakaoTalkStrategy] FOREGROUND_SERVICE 제외: className={ClassName} (NotificationService 아님)",
                className);
        }

        return isKakaoService;
    }
}

