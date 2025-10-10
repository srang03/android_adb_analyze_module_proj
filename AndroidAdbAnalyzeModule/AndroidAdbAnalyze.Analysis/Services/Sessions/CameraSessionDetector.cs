using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Options;
using Microsoft.Extensions.Logging;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Services.Sessions;

/// <summary>
/// 카메라 세션 감지 서비스 구현
/// </summary>
/// <remarks>
/// 여러 SessionSource(usagestats, media_camera 등)에서 세션을 수집하고,
/// 우선순위에 따라 병합하여 최종 세션 목록을 생성합니다.
/// </remarks>
public sealed class CameraSessionDetector : ISessionDetector
{
    private readonly ILogger<CameraSessionDetector> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    private readonly IReadOnlyList<ISessionSource> _sessionSources;
    
    // 시스템 패키지 (세션에서 제외)
    private static readonly HashSet<string> SystemPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "android.system",
        "com.android.systemui"
    };
    
    private const double MinOverlapRatio = 0.8; // 세션 병합 최소 겹침 비율 (80%)

    public CameraSessionDetector(
        ILogger<CameraSessionDetector> logger,
        IConfidenceCalculator confidenceCalculator,
        IEnumerable<ISessionSource> sessionSources)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
        
        if (sessionSources == null)
            throw new ArgumentNullException(nameof(sessionSources));
        
        // SessionSources를 우선순위 순으로 정렬 (높은 순)
        _sessionSources = sessionSources
            .OrderByDescending(s => s.Priority)
            .ToList();
        
        if (_sessionSources.Count == 0)
        {
            throw new InvalidOperationException(
                "최소 하나 이상의 ISessionSource가 등록되어야 합니다. " +
                "ServiceCollectionExtensions에서 SessionSource를 등록했는지 확인하세요.");
        }
        
        _logger.LogInformation(
            "SessionSources 등록 완료: {Sources}",
            string.Join(", ", _sessionSources.Select(s => $"{s.SourceName}(Priority={s.Priority})")));
    }

    /// <inheritdoc/>
    public IReadOnlyList<CameraSession> DetectSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        if (events == null || events.Count == 0)
        {
            _logger.LogDebug("이벤트가 없으므로 빈 세션 목록 반환");
            return Array.Empty<CameraSession>();
        }

        _logger.LogInformation("세션 감지 시작: {EventCount}개 이벤트", events.Count);

        // 1단계: 패키지 필터링
        var filteredEvents = ApplyPackageFilters(events, options);
        
        // 2단계: 원시 세션 추출
        var rawSessions = ExtractRawSessions(filteredEvents, options);
        
        // 3단계: 세션 병합
        var mergedSessions = MergeSessions(rawSessions);
        
        // 4단계: 불완전 세션 처리
        var completedSessions = options.EnableIncompleteSessionHandling
            ? HandleIncompleteSessions(mergedSessions, filteredEvents, options)
            : mergedSessions;
        
        // 5단계: 시스템 패키지 필터링
        var nonSystemSessions = completedSessions
            .Where(s => !SystemPackages.Contains(s.PackageName))
            .ToList();
        
        // 6단계: 신뢰도 필터링
        var finalSessions = nonSystemSessions
            .Where(s => s.ConfidenceScore >= options.MinConfidenceThreshold)
            .ToList();

        _logger.LogInformation(
            "세션 감지 완료: {Count}개 세션 (시스템 패키지 필터링 전: {Before}개)",
            finalSessions.Count, nonSystemSessions.Count);

        return finalSessions;
    }

    /// <summary>
    /// 패키지 필터링 적용
    /// </summary>
    private List<NormalizedLogEvent> ApplyPackageFilters(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        var filtered = events.AsEnumerable();

        // 화이트리스트 필터링
        if (options.PackageWhitelist != null && options.PackageWhitelist.Count > 0)
        {
            filtered = filtered.Where(e =>
            {
                if (!e.Attributes.TryGetValue("package", out var pkg))
                    return false;
                
                var packageName = pkg?.ToString() ?? string.Empty;
                return options.PackageWhitelist.Any(w => packageName.Contains(w));
            });
        }

        // 블랙리스트 필터링
        if (options.PackageBlacklist.Count > 0)
        {
            filtered = filtered.Where(e =>
            {
                if (!e.Attributes.TryGetValue("package", out var pkg))
                    return true; // package 정보 없으면 유지
                
                var packageName = pkg?.ToString() ?? string.Empty;
                return !options.PackageBlacklist.Any(b => packageName.Contains(b));
            });
        }

        return filtered.ToList();
    }

    /// <summary>
    /// 원시 세션 추출 (SessionSources 위임)
    /// </summary>
    /// <remarks>
    /// 각 SessionSource에서 세션을 추출하고, 우선순위 순으로 수집합니다.
    /// 병합은 MergeSessions 메서드에서 처리됩니다.
    /// </remarks>
    private List<CameraSession> ExtractRawSessions(
        List<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        var allRawSessions = new List<CameraSession>();
        
        // 각 SessionSource에서 세션 추출 (우선순위 순)
        foreach (var source in _sessionSources)
        {
            _logger.LogInformation(
                "세션 추출 시작: {Source} (우선순위={Priority})",
                source.SourceName, source.Priority);

            var sourceSessions = source.ExtractSessions(events, options);
            
            _logger.LogInformation(
                "세션 추출 완료: {Source} - {Count}개",
                source.SourceName, sourceSessions.Count);
            
            allRawSessions.AddRange(sourceSessions);
        }

        _logger.LogInformation(
            "전체 원시 세션 추출 완료: {Total}개 (Sources: {SourceCount}개)",
            allRawSessions.Count, _sessionSources.Count);
        
        // 디버깅: 각 SessionSource별 세션 패키지 출력
        _logger.LogDebug("=== 원시 세션 상세 ===");
        foreach (var session in allRawSessions.OrderBy(s => s.StartTime))
        {
            _logger.LogDebug(
                "[Raw] Package={Package}, Time={StartTime:HH:mm:ss}~{EndTime:HH:mm:ss}, Source={Sources}, Confidence={Confidence:F2}",
                session.PackageName, session.StartTime, session.EndTime,
                string.Join(",", session.SourceLogTypes), session.ConfidenceScore);
        }
        
        return allRawSessions;
    }

    /// <summary>
    /// 세션의 우선순위 계산 (SourceLogTypes 기반)
    /// </summary>
    /// <remarks>
    /// usagestats: 100 (Primary, taskRootPackage 정보 포함)
    /// media_camera: 50 (Secondary, 보완용)
    /// 기타: 0
    /// </remarks>
    private int GetSessionPriority(CameraSession session)
    {
        if (session.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase)))
            return 100;
        if (session.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase)))
            return 50;
        return 0;
    }

    /// <summary>
    /// 세션 병합 (시간 겹침 기반)
    /// </summary>
    private List<CameraSession> MergeSessions(List<CameraSession> sessions)
    {
        if (sessions.Count <= 1)
            return sessions;

        _logger.LogDebug("세션 병합 시작: {Count}개 세션", sessions.Count);

        // 시간순으로 정렬 (PackageName 그룹화 제거 → 다른 패키지도 병합 가능)
        var sortedSessions = sessions.OrderBy(s => s.StartTime).ToList();
        
        var mergedSessions = new List<CameraSession>();
        CameraSession? current = null;

        foreach (var session in sortedSessions)
        {
            if (current == null)
            {
                current = session;
                continue;
            }

            // 겹침 비율 계산
            var overlapRatio = CalculateOverlapRatio(current, session);

            if (overlapRatio >= MinOverlapRatio)
            {
                // 병합
                _logger.LogDebug(
                    "세션 병합: Package1={Package1} (Priority={Priority1}, Conf={Conf1:F2}, Sources={Sources1}) + Package2={Package2} (Priority={Priority2}, Conf={Conf2:F2}, Sources={Sources2}), 겹침={Overlap:P0}",
                    current.PackageName, GetSessionPriority(current), current.ConfidenceScore, string.Join(",", current.SourceLogTypes),
                    session.PackageName, GetSessionPriority(session), session.ConfidenceScore, string.Join(",", session.SourceLogTypes),
                    overlapRatio);

                current = MergeTwoSessions(current, session);
                
                _logger.LogDebug(
                    "병합 결과: Package={Package} (Priority={Priority}, Conf={Conf:F2}, Sources={Sources})",
                    current.PackageName, GetSessionPriority(current), current.ConfidenceScore, string.Join(",", current.SourceLogTypes));
            }
            else
            {
                // 별도 세션으로 유지
                mergedSessions.Add(current);
                current = session;
            }
        }

        if (current != null)
            mergedSessions.Add(current);

        _logger.LogDebug(
            "세션 병합 완료: {Before}개 → {After}개",
            sessions.Count, mergedSessions.Count);

        // 디버깅: 최종 병합 결과 출력
        _logger.LogDebug("=== 병합된 세션 상세 ===");
        foreach (var session in mergedSessions.OrderBy(s => s.StartTime))
        {
            _logger.LogDebug(
                "[Merged] Package={Package}, Time={StartTime:HH:mm:ss}~{EndTime:HH:mm:ss}, Sources={Sources}, Confidence={Confidence:F2}",
                session.PackageName, session.StartTime, session.EndTime, 
                string.Join(",", session.SourceLogTypes), session.ConfidenceScore);
        }

        return mergedSessions;
    }

    /// <summary>
    /// 두 세션의 겹침 비율 계산
    /// </summary>
    private double CalculateOverlapRatio(CameraSession session1, CameraSession session2)
    {
        // 불완전 세션은 겹침 계산 불가
        if (!session1.EndTime.HasValue || !session2.EndTime.HasValue)
            return 0.0;

        var start1 = session1.StartTime;
        var end1 = session1.EndTime.Value;
        var start2 = session2.StartTime;
        var end2 = session2.EndTime.Value;

        // 겹침 구간 계산
        var overlapStart = start1 > start2 ? start1 : start2;
        var overlapEnd = end1 < end2 ? end1 : end2;

        if (overlapStart >= overlapEnd)
            return 0.0; // 겹침 없음

        var overlapDuration = (overlapEnd - overlapStart).TotalSeconds;
        var minDuration = Math.Min(
            (end1 - start1).TotalSeconds,
            (end2 - start2).TotalSeconds);

        return overlapDuration / minDuration;
    }

    /// <summary>
    /// 두 세션을 하나로 병합
    /// </summary>
    private CameraSession MergeTwoSessions(CameraSession session1, CameraSession session2)
    {
        // Priority 우선, 동일하면 Confidence 높은 세션의 정보 우선
        var priority1 = GetSessionPriority(session1);
        var priority2 = GetSessionPriority(session2);
        
        CameraSession primary, secondary;
        if (priority1 != priority2)
        {
            // Priority가 다르면 높은 쪽 선택
            primary = priority1 > priority2 ? session1 : session2;
            secondary = primary == session1 ? session2 : session1;
        }
        else
        {
            // Priority 동일하면 Confidence 높은 쪽 선택
            primary = session1.ConfidenceScore >= session2.ConfidenceScore ? session1 : session2;
            secondary = primary == session1 ? session2 : session1;
        }

        // 시작/종료 시간은 가장 넓은 범위 선택
        var startTime = session1.StartTime < session2.StartTime ? session1.StartTime : session2.StartTime;
        var endTime = session1.EndTime.HasValue && session2.EndTime.HasValue
            ? (session1.EndTime.Value > session2.EndTime.Value ? session1.EndTime.Value : session2.EndTime.Value)
            : session1.EndTime ?? session2.EndTime;

        // 소스 로그 타입 병합
        var mergedSources = primary.SourceLogTypes
            .Concat(secondary.SourceLogTypes)
            .Distinct()
            .ToList();

        // 이벤트 ID 병합
        var mergedEventIds = primary.SourceEventIds
            .Concat(secondary.SourceEventIds)
            .Distinct()
            .ToList();

        // 신뢰도 재계산 (더 많은 증거 = 더 높은 신뢰도)
        var mergedConfidence = Math.Min(
            primary.ConfidenceScore + secondary.ConfidenceScore * 0.3,
            1.0);

        return new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = startTime,
            EndTime = endTime,
            PackageName = primary.PackageName,
            ProcessId = primary.ProcessId ?? secondary.ProcessId,
            SourceLogTypes = mergedSources,
            StartEventId = primary.StartEventId ?? secondary.StartEventId,
            EndEventId = primary.EndEventId ?? secondary.EndEventId,
            IncompleteReason = endTime.HasValue ? null : primary.IncompleteReason ?? secondary.IncompleteReason,
            ConfidenceScore = mergedConfidence,
            SourceEventIds = mergedEventIds
        };
    }

    /// <summary>
    /// 불완전 세션 처리
    /// </summary>
    private List<CameraSession> HandleIncompleteSessions(
        List<CameraSession> sessions,
        List<NormalizedLogEvent> allEvents,
        AnalysisOptions options)
    {
        var incompleteSessions = sessions.Where(s => s.IsIncomplete).ToList();
        
        if (incompleteSessions.Count == 0)
        {
            _logger.LogDebug("불완전 세션 없음");
            return sessions;
        }

        _logger.LogInformation("불완전 세션 처리 시작: {Count}개", incompleteSessions.Count);

        var completeSessions = sessions.Where(s => !s.IsIncomplete).ToList();
        var processedSessions = new List<CameraSession>(completeSessions);

        // 평균 세션 지속 시간 계산
        var averageDuration = completeSessions.Any()
            ? TimeSpan.FromSeconds(completeSessions.Average(s => s.Duration!.Value.TotalSeconds))
            : TimeSpan.FromMinutes(5); // 기본값

        foreach (var session in incompleteSessions)
        {
            var processed = TryCompleteSession(session, sessions, allEvents, averageDuration, options);
            processedSessions.Add(processed);
        }

        _logger.LogInformation(
            "불완전 세션 처리 완료: {Completed}개 완료됨",
            processedSessions.Count(s => !s.IsIncomplete));

        return processedSessions.OrderBy(s => s.StartTime).ToList();
    }

    /// <summary>
    /// 불완전 세션 완료 시도
    /// </summary>
    private CameraSession TryCompleteSession(
        CameraSession session,
        List<CameraSession> allSessions,
        List<NormalizedLogEvent> allEvents,
        TimeSpan averageDuration,
        AnalysisOptions options)
    {
        // 1순위: 다음 세션 시작 시각으로 종료
        var nextSession = allSessions
            .Where(s => s.PackageName == session.PackageName && s.StartTime > session.StartTime)
            .OrderBy(s => s.StartTime)
            .FirstOrDefault();

        if (nextSession != null)
        {
            var gap = nextSession.StartTime - session.StartTime;
            if (gap <= options.MaxSessionGap)
            {
                _logger.LogDebug(
                    "세션 {Id} 종료 추정: 다음 세션 시작 전 ({Gap:F1}초 전)",
                    session.SessionId, gap.TotalSeconds);

                return new CameraSession
                {
                    SessionId = session.SessionId,
                    StartTime = session.StartTime,
                    EndTime = nextSession.StartTime.AddSeconds(-1),
                    PackageName = session.PackageName,
                    ProcessId = session.ProcessId,
                    SourceLogTypes = session.SourceLogTypes,
                    CaptureEventIds = session.CaptureEventIds,
                    StartEventId = session.StartEventId,
                    EndEventId = session.EndEventId,
                    IncompleteReason = null, // 완료됨
                    ConfidenceScore = session.ConfidenceScore,
                    SourceEventIds = session.SourceEventIds
                };
            }
        }

        // 2순위: 재부팅 또는 타임스탬프 역행 감지
        var rebootEvent = allEvents
            .Where(e => e.Timestamp > session.StartTime)
            .Where(e => e.EventType == LogEventTypes.SYSTEM_BOOT || e.EventType == LogEventTypes.DEVICE_REBOOT)
            .OrderBy(e => e.Timestamp)
            .FirstOrDefault();

        if (rebootEvent != null)
        {
            _logger.LogDebug(
                "세션 {Id} 종료 추정: 재부팅 감지",
                session.SessionId);

            return new CameraSession
            {
                SessionId = session.SessionId,
                StartTime = session.StartTime,
                EndTime = rebootEvent.Timestamp,
                PackageName = session.PackageName,
                ProcessId = session.ProcessId,
                SourceLogTypes = session.SourceLogTypes,
                CaptureEventIds = session.CaptureEventIds,
                StartEventId = session.StartEventId,
                EndEventId = session.EndEventId,
                IncompleteReason = SessionIncompleteReason.DeviceReboot,
                ConfidenceScore = session.ConfidenceScore,
                SourceEventIds = session.SourceEventIds
            };
        }

        // 3순위: 평균 사용 시간 기반 추정
        var estimatedEnd = session.StartTime + averageDuration;
        
        _logger.LogDebug(
            "세션 {Id} 종료 추정: 평균 사용 시간 기반 ({Duration:F1}분)",
            session.SessionId, averageDuration.TotalMinutes);

        return new CameraSession
        {
            SessionId = session.SessionId,
            StartTime = session.StartTime,
            EndTime = estimatedEnd,
            PackageName = session.PackageName,
            ProcessId = session.ProcessId,
            SourceLogTypes = session.SourceLogTypes,
            CaptureEventIds = session.CaptureEventIds,
            StartEventId = session.StartEventId,
            EndEventId = session.EndEventId,
            IncompleteReason = SessionIncompleteReason.LogTruncated,
            ConfidenceScore = session.ConfidenceScore,
            SourceEventIds = session.SourceEventIds
        };
    }
}
