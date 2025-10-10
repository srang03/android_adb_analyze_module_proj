using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Strategies;

/// <summary>
/// KakaoTalk 전용 촬영 탐지 전략
/// </summary>
/// <remarks>
/// KakaoTalk은 다음과 같은 특징이 있습니다:
/// 
/// 1. 시스템 카메라 API 사용 (package=com.sec.android.app.camera, taskRootPackage=com.kakao.talk)
/// 2. PLAYER_EVENT 없음 (셔터 음 재생하지 않음)
/// 3. URI_PERMISSION_GRANT 발생 (임시 파일 경로)
/// 4. VIBRATION_EVENT (usage: TOUCH) 발생 가능
/// 5. CAMERA_ACTIVITY_REFRESH 발생 가능
/// 
/// 따라서:
/// - URI_PERMISSION_GRANT를 주 증거로 사용
/// - VIBRATION_EVENT, CAMERA_ACTIVITY_REFRESH를 보조 주 증거로 사용
/// - BaseStrategy의 PLAYER_EVENT 의존성 제거
/// 
/// 적용 패키지:
/// - com.kakao.talk
/// </remarks>
public sealed class KakaoTalkStrategy : ICaptureDetectionStrategy
{
    private readonly ILogger<KakaoTalkStrategy> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    
    // KakaoTalk용 주 증거
    private static readonly HashSet<string> PrimaryEvidenceTypes = new()
    {
        LogEventTypes.VIBRATION_EVENT            // hapticType=50061 (촬영 버튼 - 필수)
    };
    
    // KakaoTalk용 보조 주 증거 (VIBRATION_EVENT와 함께 사용)
    private static readonly HashSet<string> SecondaryEvidenceTypes = new()
    {
        LogEventTypes.URI_PERMISSION_GRANT,      // 임시 파일 경로
        LogEventTypes.CAMERA_ACTIVITY_REFRESH    // activity.log (촬영 시 발생)
    };
    
    // 보조 증거
    private static readonly HashSet<string> SupportingEvidenceTypes = new()
    {
        LogEventTypes.PLAYER_CREATED,
        LogEventTypes.PLAYER_RELEASED,
        LogEventTypes.MEDIA_EXTRACTOR,
        LogEventTypes.VIBRATION,
        LogEventTypes.URI_PERMISSION_GRANT,
        LogEventTypes.CAMERA_ACTIVITY_REFRESH
    };
    
    // VIBRATION_EVENT 검증용 상수
    private const int HAPTIC_TYPE_CAMERA_SHUTTER = 50061;

    public KakaoTalkStrategy(
        ILogger<KakaoTalkStrategy> logger,
        IConfidenceCalculator confidenceCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
    }

    /// <inheritdoc/>
    public string? PackageNamePattern => "com.kakao.talk";

    /// <inheritdoc/>
    public int Priority => 90; // Telegram(100)보다 낮고, Base(0)보다 높음

    /// <inheritdoc/>
    public IReadOnlyList<CameraCaptureEvent> DetectCaptures(
        SessionContext context,
        AnalysisOptions options)
    {
        var captures = new List<CameraCaptureEvent>();

        // KakaoTalk은 VIBRATION_EVENT (hapticType=50061)를 주 증거로 사용
        var vibrationEvents = context.AllEvents
            .Where(e => PrimaryEvidenceTypes.Contains(e.EventType))
            .Where(e => ValidateVibrationEventAsShutter(e))
            .OrderBy(e => e.Timestamp)
            .ToList();

        _logger.LogInformation(
            "[KakaoTalkStrategy] Session {SessionId} ({Package}): VIBRATION_EVENT (hapticType=50061) {Count}개",
            context.Session.SessionId, context.Session.PackageName, vibrationEvents.Count);

        foreach (var vibrationEvent in vibrationEvents)
        {
            // 보조 증거 수집
            var supportingEvidences = CollectSupportingEvidences(
                vibrationEvent,
                context.AllEvents.ToList(),
                options.EventCorrelationWindow);

            // 신뢰도 계산
            var allEvidences = new List<NormalizedLogEvent> { vibrationEvent };
            allEvidences.AddRange(supportingEvidences);
            var confidence = _confidenceCalculator.CalculateConfidence(allEvidences);

            // 최소 신뢰도 확인
            if (confidence < options.MinConfidenceThreshold)
            {
                _logger.LogDebug(
                    "[KakaoTalkStrategy] 신뢰도 미달: EventId={EventId}, Confidence={Confidence:F2}",
                    vibrationEvent.EventId, confidence);
                continue;
            }

            // CameraCaptureEvent 생성
            var capture = CreateCaptureEvent(
                context.Session,
                vibrationEvent,
                supportingEvidences,
                allEvidences,
                confidence);

            captures.Add(capture);

            _logger.LogInformation(
                "[KakaoTalkStrategy] 촬영 감지: CaptureId={CaptureId}, Time={Time:HH:mm:ss.fff}, Confidence={Confidence:F2}",
                capture.CaptureId, capture.CaptureTime, capture.ConfidenceScore);
        }

        return captures;
    }

    /// <summary>
    /// VIBRATION_EVENT를 촬영 버튼 진동으로 검증 (hapticType=50061)
    /// </summary>
    /// <remarks>
    /// KakaoTalk이 기본 카메라를 호출할 때 발생하는 촬영 버튼 진동 이벤트를 감지합니다.
    /// hapticType=50061: 촬영 버튼 터치 (실제 촬영)
    /// </remarks>
    private bool ValidateVibrationEventAsShutter(NormalizedLogEvent evidence)
    {
        if (!evidence.Attributes.TryGetValue("hapticType", out var hapticTypeObj))
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

        if (hapticType != HAPTIC_TYPE_CAMERA_SHUTTER)
        {
            _logger.LogTrace(
                "[KakaoTalkStrategy] VIBRATION_EVENT 제외: hapticType={HapticType} (50061 아님)",
                hapticType);
            return false;
        }

        _logger.LogDebug(
            "[KakaoTalkStrategy] ✅ VIBRATION_EVENT 승인: hapticType=50061 (촬영 버튼), Time={Time:HH:mm:ss.fff}",
            evidence.Timestamp);
        return true;
    }

    /// <summary>
    /// 보조 증거 수집
    /// </summary>
    private List<NormalizedLogEvent> CollectSupportingEvidences(
        NormalizedLogEvent vibrationEvent,
        List<NormalizedLogEvent> allEvents,
        TimeSpan correlationWindow)
    {
        var windowStart = vibrationEvent.Timestamp - correlationWindow;
        var windowEnd = vibrationEvent.Timestamp + correlationWindow;

        // 보조 주 증거 (URI_PERMISSION_GRANT, CAMERA_ACTIVITY_REFRESH)
        var secondaryEvidences = allEvents
            .Where(e => 
                e.EventId != vibrationEvent.EventId &&
                SecondaryEvidenceTypes.Contains(e.EventType) &&
                e.Timestamp >= windowStart &&
                e.Timestamp <= windowEnd)
            .ToList();
        
        // 일반 보조 증거
        var supportingEvidences = allEvents
            .Where(e => 
                e.EventId != vibrationEvent.EventId &&
                SupportingEvidenceTypes.Contains(e.EventType) &&
                e.Timestamp >= windowStart &&
                e.Timestamp <= windowEnd)
            .ToList();

        // 통합
        var allSupportingEvidences = secondaryEvidences.Concat(supportingEvidences)
            .Distinct()
            .ToList();

        _logger.LogDebug(
            "[KakaoTalkStrategy] 보조 증거: {Count}개 (보조 주 증거: {Secondary}개)",
            allSupportingEvidences.Count, secondaryEvidences.Count);

        return allSupportingEvidences;
    }

    /// <summary>
    /// CameraCaptureEvent 생성
    /// </summary>
    private CameraCaptureEvent CreateCaptureEvent(
        CameraSession session,
        NormalizedLogEvent primaryEvidence,
        List<NormalizedLogEvent> supportingEvidences,
        List<NormalizedLogEvent> allEvidences,
        double confidence)
    {
        // 증거 타입 목록
        var evidenceTypes = allEvidences
            .Select(e => e.EventType)
            .Distinct()
            .ToList();

        // 메타데이터 수집
        var metadata = new Dictionary<string, string>
        {
            ["detection_strategy"] = "KakaoTalkStrategy",
            ["primary_evidence_type"] = "VIBRATION_EVENT (hapticType=50061)"
        };
        
        foreach (var attr in primaryEvidence.Attributes)
        {
            if (attr.Value != null)
                metadata[attr.Key] = attr.Value.ToString() ?? string.Empty;
        }

        // URI 추출 (보조 증거에서 URI_PERMISSION_GRANT 찾기)
        string? fileUri = null;
        var uriPermissionEvidence = supportingEvidences.FirstOrDefault(e => 
            e.EventType == LogEventTypes.URI_PERMISSION_GRANT);
        if (uriPermissionEvidence != null &&
            uriPermissionEvidence.Attributes.TryGetValue("uri", out var uriObj))
        {
            fileUri = uriObj?.ToString();
        }

        return new CameraCaptureEvent
        {
            CaptureId = Guid.NewGuid(),
            ParentSessionId = session.SessionId,
            CaptureTime = primaryEvidence.Timestamp,
            PackageName = session.PackageName,
            FilePath = null, // KakaoTalk은 파일 경로 정보 없음 (임시 파일 사용)
            FileUri = fileUri,
            PrimaryEvidenceId = primaryEvidence.EventId,
            SupportingEvidenceIds = supportingEvidences.Select(e => e.EventId).ToList(),
            IsEstimated = false, // 주 증거가 있으므로 false
            ConfidenceScore = confidence,
            EvidenceTypes = evidenceTypes,
            SourceEventIds = allEvidences.Select(e => e.EventId).ToList(),
            Metadata = metadata
        };
    }
}

