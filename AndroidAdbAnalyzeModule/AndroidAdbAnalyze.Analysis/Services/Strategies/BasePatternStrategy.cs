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
/// 기본 패턴 기반 촬영 탐지 전략
/// </summary>
/// <remarks>
/// 대부분의 앱에 적용 가능한 범용 전략입니다:
/// 
/// 확정 주 증거:
/// - DATABASE_INSERT, DATABASE_EVENT, MEDIA_INSERT_END (미디어 저장 확정)
/// 
/// 조건부 주 증거:
/// - PLAYER_EVENT (셔터 음) + PostProcessService 필터링
/// - URI_PERMISSION_GRANT (임시 파일 경로만)
/// - SILENT_CAMERA_CAPTURE (무음 카메라)
/// 
/// 적용 대상:
/// - 기본 카메라 (com.sec.android.app.camera)
/// - 카카오톡 카메라 (com.kakao.talk)
/// - 무음 카메라 (com.peace.SilentCamera)
/// - 기타 표준 카메라 API 사용 앱
/// </remarks>
public sealed class BasePatternStrategy : ICaptureDetectionStrategy
{
    private readonly ILogger<BasePatternStrategy> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    
    // 확정 주 증거 (촬영 100% 확정)
    private static readonly HashSet<string> PrimaryEvidenceTypes = new()
    {
        LogEventTypes.DATABASE_INSERT,
        LogEventTypes.DATABASE_EVENT,
        LogEventTypes.MEDIA_INSERT_END
    };
    
    // 조건부 주 증거 (특정 조건 만족 시 촬영 확정)
    private static readonly HashSet<string> ConditionalPrimaryEvidenceTypes = new()
    {
        LogEventTypes.VIBRATION_EVENT,      // 촬영 버튼 진동 (hapticType=50061)
        LogEventTypes.PLAYER_EVENT,         // 셔터 음 (PostProcessService 필터링 필요)
        LogEventTypes.URI_PERMISSION_GRANT, // URI 권한 (임시 파일만)
        LogEventTypes.SILENT_CAMERA_CAPTURE // 무음 카메라
    };
    
    // 보조 증거
    private static readonly HashSet<string> SupportingEvidenceTypes = new()
    {
        LogEventTypes.PLAYER_CREATED,
        LogEventTypes.PLAYER_EVENT,
        LogEventTypes.PLAYER_RELEASED,
        LogEventTypes.MEDIA_EXTRACTOR,
        LogEventTypes.SHUTTER_SOUND,
        LogEventTypes.VIBRATION,
        LogEventTypes.VIBRATION_EVENT,
        LogEventTypes.URI_PERMISSION_GRANT,
        LogEventTypes.CAMERA_ACTIVITY_REFRESH
    };
    
    // PLAYER_EVENT 검증용 문자열 상수
    private const string PLAYER_EVENT_STATE_STARTED = "started";
    private const string PLAYER_TAG_CAMERA = "CAMERA";
    
    // Foreground Service 검증용 문자열 상수
    private const string SERVICE_CLASS_POST_PROCESS = "PostProcessService";
    
    // VIBRATION_EVENT 검증용 상수
    private const int HAPTIC_TYPE_CAMERA_SHUTTER = 50061;

    public BasePatternStrategy(
        ILogger<BasePatternStrategy> logger,
        IConfidenceCalculator confidenceCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
    }

    /// <inheritdoc/>
    public string? PackageNamePattern => null; // 모든 앱에 기본 적용

    /// <inheritdoc/>
    public int Priority => 0; // 가장 낮은 우선순위 (fallback)

    /// <inheritdoc/>
    public IReadOnlyList<CameraCaptureEvent> DetectCaptures(
        SessionContext context,
        AnalysisOptions options)
    {
        var captures = new List<CameraCaptureEvent>();
        var processedEvidenceIds = new HashSet<Guid>();

        // 1단계: 확정 주 증거 조회
        var primaryEvidences = context.AllEvents
            .Where(e => PrimaryEvidenceTypes.Contains(e.EventType))
            .ToList();

        _logger.LogDebug(
            "[BaseStrategy] Session {SessionId} ({Package}): 확정 주 증거 {Count}개",
            context.Session.SessionId, context.Session.PackageName, primaryEvidences.Count);

        // 2단계: 조건부 주 증거 조회 (확정 주 증거가 없을 때만)
        List<NormalizedLogEvent> conditionalPrimaryEvidences;
        if (primaryEvidences.Count == 0)
        {
            conditionalPrimaryEvidences = context.AllEvents
                .Where(e => ConditionalPrimaryEvidenceTypes.Contains(e.EventType))
                .Where(e => ValidateConditionalPrimaryEvidence(e, context))
                .ToList();
            
            _logger.LogDebug(
                "[BaseStrategy] Session {SessionId}: 조건부 주 증거 {Count}개",
                context.Session.SessionId, conditionalPrimaryEvidences.Count);
        }
        else
        {
            conditionalPrimaryEvidences = new List<NormalizedLogEvent>();
        }

        // 모든 주 증거 통합
        var allPrimaryEvidences = primaryEvidences.Concat(conditionalPrimaryEvidences).ToList();

        _logger.LogInformation(
            "[BaseStrategy] Session {SessionId} ({Package}): 총 주 증거 {Count}개 (확정: {Primary}, 조건부: {Conditional})",
            context.Session.SessionId, context.Session.PackageName, 
            allPrimaryEvidences.Count, primaryEvidences.Count, conditionalPrimaryEvidences.Count);

        foreach (var primaryEvidence in allPrimaryEvidences)
        {
            if (processedEvidenceIds.Contains(primaryEvidence.EventId))
                continue;

            // 경로 패턴 검증
            if (IsExcludedByPathPattern(primaryEvidence, options))
            {
                _logger.LogDebug(
                    "[BaseStrategy] 경로 패턴 제외: EventId={EventId}",
                    primaryEvidence.EventId);
                continue;
            }

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
                    "[BaseStrategy] 신뢰도 미달: EventId={EventId}, Confidence={Confidence:F2}",
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
                "[BaseStrategy] 촬영 감지: CaptureId={CaptureId}, Time={Time:HH:mm:ss.fff}, Confidence={Confidence:F2}",
                capture.CaptureId, capture.CaptureTime, capture.ConfidenceScore);
        }

        // 중복 제거 (시간 윈도우 기반)
        var deduplicated = DeduplicateCapturesByTimeWindow(captures, options.EventCorrelationWindow);
        
        _logger.LogInformation(
            "[BaseStrategy] Session {SessionId}: 중복 제거 완료 ({Before}개 → {After}개)",
            context.Session.SessionId, captures.Count, deduplicated.Count);

        return deduplicated;
    }

    /// <summary>
    /// 조건부 주 증거 검증
    /// </summary>
    private bool ValidateConditionalPrimaryEvidence(
        NormalizedLogEvent evidence,
        SessionContext context)
    {
        switch (evidence.EventType)
        {
            case var type when type == LogEventTypes.VIBRATION_EVENT:
                return ValidateVibrationEventAsShutter(evidence);
                
            case var type when type == LogEventTypes.PLAYER_EVENT:
                return ValidatePlayerEvent(evidence, context);
                
            case var type when type == LogEventTypes.URI_PERMISSION_GRANT:
                return ValidateUriPermission(evidence);
                
            case var type when type == LogEventTypes.SILENT_CAMERA_CAPTURE:
                return true; // SilentCameraCaptureParser에서 이미 검증됨
                
            default:
                return false;
        }
    }
    
    /// <summary>
    /// VIBRATION_EVENT를 촬영 버튼 진동으로 검증 (hapticType=50061)
    /// </summary>
    /// <remarks>
    /// 기본 카메라의 촬영 버튼을 누를 때 발생하는 진동 이벤트를 감지합니다.
    /// hapticType=50061: 촬영 버튼 터치 (실제 촬영)
    /// hapticType=50072: 일반 UI 터치 (확대/설정 등) - 제외
    /// status=finished: 정상 완료된 진동만 인정
    /// status=cancelled_superseded: 취소된 진동 제외
    /// </remarks>
    private bool ValidateVibrationEventAsShutter(NormalizedLogEvent evidence)
    {
        if (!evidence.Attributes.TryGetValue("hapticType", out var hapticTypeObj))
        {
            _logger.LogTrace(
                "[BaseStrategy] VIBRATION_EVENT 제외: hapticType 정보 없음");
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
                "[BaseStrategy] VIBRATION_EVENT 제외: hapticType 파싱 실패 (value={Value})",
                hapticTypeObj);
            return false;
        }

        if (hapticType != HAPTIC_TYPE_CAMERA_SHUTTER)
        {
            _logger.LogTrace(
                "[BaseStrategy] VIBRATION_EVENT 제외: hapticType={HapticType} (50061 아님)",
                hapticType);
            return false;
        }

        // status 확인: "finished"만 유효
        if (evidence.Attributes.TryGetValue("status", out var statusObj))
        {
            var status = statusObj?.ToString() ?? string.Empty;
            if (!status.Equals("finished", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogTrace(
                    "[BaseStrategy] VIBRATION_EVENT 제외: status={Status} (취소됨), Time={Time:HH:mm:ss.fff}",
                    status, evidence.Timestamp);
                return false;
            }
        }

        _logger.LogDebug(
            "[BaseStrategy] ✅ VIBRATION_EVENT 승인: hapticType=50061, status=finished, Time={Time:HH:mm:ss.fff}",
            evidence.Timestamp);
        return true;
    }

    /// <summary>
    /// PLAYER_EVENT 검증 (셔터 음 + PostProcessService)
    /// </summary>
    /// <remarks>
    /// 기본 카메라 PLAYER_EVENT는 PostProcessService와 함께 사용되어야 함
    /// </remarks>
    private bool ValidatePlayerEvent(
        NormalizedLogEvent playerEvent,
        SessionContext context)
    {
            // 1. event: started 확인
            if (!playerEvent.Attributes.TryGetValue("event", out var eventObj) ||
                eventObj?.ToString() != PLAYER_EVENT_STATE_STARTED)
            {
                return false;
            }

        // 2. piid 추출
        if (!playerEvent.Attributes.TryGetValue("piid", out var piidObj))
        {
            return false;
        }

        // 3. PLAYER_CREATED에서 tags: CAMERA 확인
        var relatedPlayerCreated = context.AllEvents
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED)
            .Where(e => e.Attributes.TryGetValue("piid", out var otherPiid) && 
                       otherPiid?.ToString() == piidObj.ToString())
            .Where(e => e.Timestamp <= playerEvent.Timestamp)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        if (relatedPlayerCreated == null)
        {
            return false;
        }

        if (!relatedPlayerCreated.Attributes.TryGetValue("tags", out var tagsObj))
        {
            return false;
        }

            var tags = tagsObj?.ToString() ?? string.Empty;
            if (!tags.Contains(PLAYER_TAG_CAMERA, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

        // 4. PostProcessService 확인 (기본 카메라만)
        bool hasPostProcessService = context.ForegroundServices.Any(fs =>
            fs.ServiceClass.Contains(SERVICE_CLASS_POST_PROCESS, StringComparison.OrdinalIgnoreCase) &&
            playerEvent.Timestamp >= fs.StartTime &&
            playerEvent.Timestamp <= (fs.StopTime ?? DateTime.MaxValue));

        if (!hasPostProcessService)
        {
            _logger.LogTrace(
                "[BaseStrategy] PLAYER_EVENT 제외: PostProcessService 없음 (piid={Piid})",
                piidObj);
            return false;
        }

        _logger.LogTrace(
            "[BaseStrategy] PLAYER_EVENT 승인: piid={Piid}, PostProcessService 존재",
            piidObj);
        return true;
    }

    /// <summary>
    /// URI_PERMISSION_GRANT 검증 (임시 파일만)
    /// </summary>
    private bool ValidateUriPermission(NormalizedLogEvent evidence)
    {
        if (!evidence.Attributes.TryGetValue("uri", out var uriObj))
        {
            return false;
        }

        var uri = uriObj?.ToString() ?? string.Empty;
        
        // 앨범 경로 제외
        if (IsAlbumPath(uri))
        {
            _logger.LogTrace(
                "[BaseStrategy] URI_PERMISSION_GRANT 제외: 앨범 경로 (uri={Uri})",
                uri);
            return false;
        }

        // 임시 파일 경로만 허용
        return IsCapturePath(uri);
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

        var supportingEvents = allEvents
            .Where(e => 
                e.EventId != primaryEvidence.EventId &&
                SupportingEvidenceTypes.Contains(e.EventType) &&
                e.Timestamp >= windowStart &&
                e.Timestamp <= windowEnd)
            .ToList();
        
        // VIBRATION_EVENT 필터링 (hapticType=50061만 허용)
        return FilterVibrationEventsByHapticType(supportingEvents);
    }
    
    /// <summary>
    /// VIBRATION_EVENT의 hapticType 속성으로 실제 촬영 필터링
    /// </summary>
    /// <param name="evidences">보조 증거 목록</param>
    /// <returns>필터링된 보조 증거 목록</returns>
    /// <remarks>
    /// 보조 증거로 수집할 때는 VIBRATION_EVENT를 모두 포함합니다.
    /// (주 증거에서 이미 hapticType=50061로 필터링됨)
    /// </remarks>
    private List<NormalizedLogEvent> FilterVibrationEventsByHapticType(List<NormalizedLogEvent> evidences)
    {
        // VIBRATION_EVENT는 이미 주 증거로 승격되었으므로,
        // 보조 증거에서는 모든 VIBRATION_EVENT를 그대로 포함
        return evidences;
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
        // FilePath 추출
        string? filePath = null;
        if (primaryEvidence.Attributes.TryGetValue("file_path", out var fpObj))
            filePath = fpObj?.ToString();

        // FileUri 추출
        string? fileUri = null;
        if (primaryEvidence.Attributes.TryGetValue("uri", out var uriObj))
            fileUri = uriObj?.ToString();

        // 증거 타입 목록
        var evidenceTypes = allEvidences
            .Select(e => e.EventType)
            .Distinct()
            .ToList();

        // 메타데이터 수집
        var metadata = new Dictionary<string, string>();
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
            FilePath = filePath,
            FileUri = fileUri,
            PrimaryEvidenceId = primaryEvidence.EventId,
            SupportingEvidenceIds = supportingEvidences.Select(e => e.EventId).ToList(),
            IsEstimated = false,
            ConfidenceScore = confidence,
            EvidenceTypes = evidenceTypes,
            SourceEventIds = allEvidences.Select(e => e.EventId).ToList(),
            Metadata = metadata
        };
    }

    /// <summary>
    /// 경로 패턴 검증
    /// </summary>
    private bool IsExcludedByPathPattern(
        NormalizedLogEvent evidence,
        AnalysisOptions options)
    {
        // FilePath 확인
        if (evidence.Attributes.TryGetValue("file_path", out var fpObj))
        {
            var filePath = fpObj?.ToString() ?? string.Empty;
            
            if (options.ScreenshotPathPatterns.Any(pattern => 
                filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            
            if (options.DownloadPathPatterns.Any(pattern => 
                filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // FileUri 확인
        if (evidence.Attributes.TryGetValue("uri", out var uriObj))
        {
            var fileUri = uriObj?.ToString() ?? string.Empty;
            
            // DATABASE 이벤트는 MediaStore URI여도 신규 촬영으로 간주
            bool isDatabaseEvent = evidence.EventType == LogEventTypes.DATABASE_INSERT ||
                                   evidence.EventType == LogEventTypes.DATABASE_EVENT ||
                                   evidence.EventType == LogEventTypes.MEDIA_INSERT_END;
            
            if (!isDatabaseEvent && IsAlbumPath(fileUri))
            {
                return true;
            }
            
            if (options.ScreenshotPathPatterns.Any(pattern => 
                fileUri.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            
            if (options.DownloadPathPatterns.Any(pattern => 
                fileUri.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 임시 파일 경로 확인
    /// </summary>
    private static bool IsCapturePath(string uri)
    {
        return uri.Contains("/tmp/", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("/cache/", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("temp_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 앨범 경로 확인
    /// </summary>
    private static bool IsAlbumPath(string uri)
    {
        return uri.Contains("content://media/external/images", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("content://media/external/video", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("com.google.android.providers.media", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 시간 윈도우 기반 중복 제거
    /// </summary>
    /// <remarks>
    /// 동일한 촬영에 대해 여러 증거(VIBRATION_EVENT, PLAYER_EVENT 등)가 시간대별로 발생할 수 있음.
    /// 시간 윈도우(기본 1초) 내의 여러 캡처 이벤트를 하나로 통합.
    /// 우선순위: VIBRATION_EVENT > PLAYER_EVENT > URI_PERMISSION_GRANT > SILENT_CAMERA_CAPTURE
    /// </remarks>
    private List<CameraCaptureEvent> DeduplicateCapturesByTimeWindow(
        List<CameraCaptureEvent> captures,
        TimeSpan timeWindow)
    {
        if (captures.Count <= 1)
            return captures;

        var deduplicated = new List<CameraCaptureEvent>();
        var sorted = captures.OrderBy(c => c.CaptureTime).ToList();

        var processed = new HashSet<Guid>();

        for (int i = 0; i < sorted.Count; i++)
        {
            if (processed.Contains(sorted[i].CaptureId))
                continue;

            var current = sorted[i];
            var group = new List<CameraCaptureEvent> { current };
            processed.Add(current.CaptureId);

            // 시간 윈도우 내의 다른 캡처들을 그룹에 추가
            for (int j = i + 1; j < sorted.Count; j++)
            {
                var next = sorted[j];
                if ((next.CaptureTime - current.CaptureTime) <= timeWindow)
                {
                    group.Add(next);
                    processed.Add(next.CaptureId);
                }
                else
                {
                    break; // 시간 차이가 윈도우를 벗어나면 중단
                }
            }

            // 그룹에서 우선순위가 가장 높은 캡처 선택
            var best = SelectBestCaptureFromGroup(group);
            deduplicated.Add(best);

            if (group.Count > 1)
            {
                var evidenceTypesSummary = string.Join(", ", best.EvidenceTypes);
                _logger.LogDebug(
                    "[BaseStrategy] 중복 그룹 통합: {Count}개 → 1개 (Time={Time:HH:mm:ss.fff}, EvidenceTypes=[{EvidenceTypes}])",
                    group.Count, best.CaptureTime, evidenceTypesSummary);
            }
        }

        return deduplicated;
    }

    /// <summary>
    /// 그룹에서 가장 신뢰도 높은 캡처 선택
    /// </summary>
    private CameraCaptureEvent SelectBestCaptureFromGroup(List<CameraCaptureEvent> group)
    {
        // 우선순위:
        // 1. VIBRATION_EVENT (mType=50061, status=finished) - 가장 신뢰도 높음
        // 2. PLAYER_EVENT (PostProcessService 검증 완료)
        // 3. URI_PERMISSION_GRANT
        // 4. SILENT_CAMERA_CAPTURE

        var priorities = new Dictionary<string, int>
        {
            { LogEventTypes.VIBRATION_EVENT, 100 },
            { LogEventTypes.PLAYER_EVENT, 80 },
            { LogEventTypes.URI_PERMISSION_GRANT, 60 },
            { LogEventTypes.SILENT_CAMERA_CAPTURE, 50 }
        };

        // EvidenceTypes에서 가장 우선순위가 높은 타입을 기준으로 정렬
        return group
            .OrderByDescending(c => c.EvidenceTypes.Max(et => priorities.GetValueOrDefault(et, 0)))
            .ThenByDescending(c => c.ConfidenceScore)
            .First();
    }
}
