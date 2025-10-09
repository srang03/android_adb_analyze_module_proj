using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Strategies;

/// <summary>
/// Telegram 전용 촬영 탐지 전략
/// </summary>
/// <remarks>
/// Telegram은 다음과 같은 특징이 있습니다:
/// 
/// 1. DATABASE 로그 없음 (기본 MediaStore 사용 안 함)
/// 2. PLAYER_EVENT 없음 (무음 촬영 또는 자체 셔터 음)
/// 3. VIBRATION_EVENT (usage: TOUCH) 발생 ✅ 핵심 증거
/// 
/// 따라서:
/// - VIBRATION_EVENT (usage: TOUCH)를 조건부 주 증거로 승격
/// - BaseStrategy의 DATABASE/PLAYER_EVENT 의존성 제거
/// 
/// 적용 패키지:
/// - org.telegram.messenger
/// - org.telegram.messenger.web (Telegram X)
/// </remarks>
public sealed class TelegramStrategy : ICaptureDetectionStrategy
{
    private readonly ILogger<TelegramStrategy> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    
    // Telegram용 조건부 주 증거
    private static readonly HashSet<string> ConditionalPrimaryEvidenceTypes = new()
    {
        LogEventTypes.VIBRATION_EVENT  // usage: TOUCH
    };
    
    // 보조 증거
    private static readonly HashSet<string> SupportingEvidenceTypes = new()
    {
        LogEventTypes.PLAYER_EVENT,         // 전송 시 발생 (촬영과 무관)
        LogEventTypes.PLAYER_CREATED,
        LogEventTypes.PLAYER_RELEASED,
        LogEventTypes.VIBRATION,
        LogEventTypes.URI_PERMISSION_GRANT
    };
    
    // VIBRATION_EVENT 검증용 문자열 상수
    private const string VIBRATION_USAGE_TOUCH = "TOUCH";

    public TelegramStrategy(
        ILogger<TelegramStrategy> logger,
        IConfidenceCalculator confidenceCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
    }

    /// <inheritdoc/>
    public string? PackageNamePattern => "org.telegram.messenger";

    /// <inheritdoc/>
    public int Priority => 100; // BaseStrategy보다 높은 우선순위

    /// <inheritdoc/>
    public IReadOnlyList<CameraCaptureEvent> DetectCaptures(
        SessionContext context,
        AnalysisOptions options)
    {
        var captures = new List<CameraCaptureEvent>();
        var processedEvidenceIds = new HashSet<Guid>();

        // Telegram은 VIBRATION_EVENT (usage: TOUCH)를 주 증거로 사용
        var primaryEvidences = context.AllEvents
            .Where(e => ConditionalPrimaryEvidenceTypes.Contains(e.EventType))
            .Where(e => ValidateVibrationEvent(e, context.Session.PackageName))
            .ToList();

        _logger.LogDebug(
            "[TelegramStrategy] Session {SessionId} ({Package}): VIBRATION_EVENT {Count}개",
            context.Session.SessionId, context.Session.PackageName, primaryEvidences.Count);

        foreach (var primaryEvidence in primaryEvidences)
        {
            if (processedEvidenceIds.Contains(primaryEvidence.EventId))
                continue;

            // 보조 증거 수집
            var supportingEvidences = CollectSupportingEvidences(
                primaryEvidence,
                context.AllEvents.ToList(),
                options.EventCorrelationWindow);

            // 신뢰도 계산
            var allEvidences = new List<NormalizedLogEvent> { primaryEvidence };
            allEvidences.AddRange(supportingEvidences);
            var confidence = _confidenceCalculator.CalculateConfidence(allEvidences);

            // 최소 신뢰도 확인
            if (confidence < options.MinConfidenceThreshold)
            {
                _logger.LogDebug(
                    "[TelegramStrategy] 신뢰도 미달: EventId={EventId}, Confidence={Confidence:F2}",
                    primaryEvidence.EventId, confidence);
                continue;
            }

            // CameraCaptureEvent 생성
            var capture = CreateCaptureEvent(
                context.Session,
                primaryEvidence,
                supportingEvidences,
                allEvidences,
                confidence);

            captures.Add(capture);
            processedEvidenceIds.Add(primaryEvidence.EventId);

            _logger.LogInformation(
                "[TelegramStrategy] 촬영 감지: CaptureId={CaptureId}, Time={Time:HH:mm:ss.fff}, Confidence={Confidence:F2}",
                capture.CaptureId, capture.CaptureTime, capture.ConfidenceScore);
        }

        return captures;
    }

    /// <summary>
    /// VIBRATION_EVENT 검증 (usage: TOUCH + 패키지 확인)
    /// </summary>
    private bool ValidateVibrationEvent(NormalizedLogEvent evidence, string sessionPackageName)
    {
        // 1. usage 확인
        if (!evidence.Attributes.TryGetValue("usage", out var usageObj))
        {
            return false;
        }

        var usage = usageObj?.ToString() ?? string.Empty;
        bool isTouch = usage.Equals(VIBRATION_USAGE_TOUCH, StringComparison.OrdinalIgnoreCase);

        if (!isTouch)
        {
            _logger.LogTrace(
                "[TelegramStrategy] VIBRATION_EVENT 제외: usage={Usage} (TOUCH 아님)",
                usage);
            return false;
        }

        // 2. 패키지 확인 (세션 패키지만 허용)
        if (!evidence.Attributes.TryGetValue("package", out var pkgObj))
        {
            _logger.LogTrace(
                "[TelegramStrategy] VIBRATION_EVENT 제외: 패키지 정보 없음");
            return false;
        }

        var eventPackage = pkgObj?.ToString() ?? string.Empty;
        if (!eventPackage.Equals(sessionPackageName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogTrace(
                "[TelegramStrategy] VIBRATION_EVENT 제외: 패키지 불일치 (세션={Session}, 이벤트={Event})",
                sessionPackageName, eventPackage);
            return false;
        }

        _logger.LogTrace(
            "[TelegramStrategy] VIBRATION_EVENT 승인: usage=TOUCH, package={Package}, Timestamp={Timestamp:HH:mm:ss.fff}",
            eventPackage, evidence.Timestamp);
        return true;
    }

    /// <summary>
    /// 보조 증거 수집
    /// </summary>
    private List<NormalizedLogEvent> CollectSupportingEvidences(
        NormalizedLogEvent primaryEvidence,
        List<NormalizedLogEvent> allEvents,
        TimeSpan correlationWindow)
    {
        var windowStart = primaryEvidence.Timestamp - correlationWindow;
        var windowEnd = primaryEvidence.Timestamp + correlationWindow;

        var supportingEvidences = allEvents
            .Where(e => 
                e.EventId != primaryEvidence.EventId &&
                SupportingEvidenceTypes.Contains(e.EventType) &&
                e.Timestamp >= windowStart &&
                e.Timestamp <= windowEnd)
            .ToList();

        // PLAYER_EVENT 제외 (전송 시 발생, 촬영과 무관)
        // 일반적으로 촬영 후 5초 이상 지연되어 발생
        var filteredEvidences = supportingEvidences
            .Where(e => e.EventType != LogEventTypes.PLAYER_EVENT)
            .ToList();

        _logger.LogTrace(
            "[TelegramStrategy] 보조 증거: 전체 {Total}개 → PLAYER_EVENT 제외 후 {Filtered}개",
            supportingEvidences.Count, filteredEvidences.Count);

        return filteredEvidences;
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
            ["detection_strategy"] = "TelegramStrategy",
            ["primary_evidence_type"] = "VIBRATION_EVENT"
        };
        
        foreach (var attr in primaryEvidence.Attributes)
        {
            if (attr.Value != null)
                metadata[attr.Key] = attr.Value.ToString() ?? string.Empty;
        }

        return new CameraCaptureEvent
        {
            CaptureId = Guid.NewGuid(),
            ParentSessionId = session.SessionId,
            CaptureTime = primaryEvidence.Timestamp,
            PackageName = session.PackageName,
            FilePath = null, // Telegram은 파일 경로 정보 없음
            FileUri = null,
            PrimaryEvidenceId = primaryEvidence.EventId,
            SupportingEvidenceIds = supportingEvidences.Select(e => e.EventId).ToList(),
            IsEstimated = false, // VIBRATION_EVENT가 주 증거이므로 false
            ConfidenceScore = confidence,
            EvidenceTypes = evidenceTypes,
            SourceEventIds = allEvidences.Select(e => e.EventId).ToList(),
            Metadata = metadata
        };
    }
}
