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
    
    /// <summary>
    /// 세션 병합 최소 겹침 비율
    /// </summary>
    /// <remarks>
    /// 설정 근거: 0.8 (80%)
    /// 
    /// 1. 경험적 설정:
    ///    - 너무 낮으면 (50%): 다른 세션을 잘못 병합 (오탐)
    ///    - 너무 높으면 (95%): 같은 세션을 병합 안 함 (미탐)
    ///    - 80% = 대부분 겹쳐야 병합 (보수적 접근)
    /// 
    /// 2. 계산 방식:
    ///    - 겹침 비율 = 겹침 구간 길이 / 짧은 세션 길이
    ///    - 예시 1:
    ///      세션 A: [10:00 ~ 10:15] (15분)
    ///      세션 B: [10:05 ~ 10:15] (10분)
    ///      겹침: [10:05 ~ 10:15] (10분)
    ///      → 10분 / 10분 = 100% ≥ 80% → 병합
    ///    
    ///    - 예시 2:
    ///      세션 A: [10:00 ~ 10:15] (15분)
    ///      세션 B: [10:08 ~ 10:15] (7분)
    ///      겹침: [10:08 ~ 10:15] (7분)
    ///      → 7분 / 7분 = 100% ≥ 80% → 병합
    ///    
    ///    - 예시 3:
    ///      세션 A: [10:00 ~ 10:15] (15분)
    ///      세션 B: [10:12 ~ 10:20] (8분)
    ///      겹침: [10:12 ~ 10:15] (3분)
    ///      → 3분 / 8분 = 37.5% &lt; 80% → 병합 안 함 (다른 세션)
    /// 
    /// 3. 설계 의도:
    ///    - 같은 촬영 세션이 여러 로그(usagestats, media_camera)에 기록될 때
    ///    - 시작/종료 시간이 약간 다를 수 있지만 대부분 겹침
    ///    - 80% 이상 겹침 = 같은 세션으로 간주하여 통합
    /// 
    /// 4. 실측 검증 (예비 실험 2회, Preliminary 1-2):
    ///    - 같은 세션 쌍 8개 분석 (기본 카메라 4개, 무음 카메라 4개): 평균 겹침 비율 97%, 최소 83%, 최대 100%
    ///    - 다른 앱 간 세션 쌍: 일반적으로 사용 시간이 겹치지 않으므로 겹침 비율 80% 미만으로 병합 안 됨
    ///    → 80%는 최소값(83%)보다 낮아 로그 시각 차이 허용, 보수적 접근으로 오탐 방지
    /// 
    /// 5. 향후 최적화:
    ///    - 대규모 데이터셋으로 최적값 탐색 (ROC 분석)
    ///    - 앱별/로그 소스별 최적 비율 조정
    ///    - 겹침 비율 외 추가 조건 (패키지명, 카메라ID) 고려
    /// 
    /// 결론: 80% = 대부분 겹쳐야 병합 (보수적 접근, 오탐 방지)
    /// </remarks>
    private const double MinOverlapRatio = 0.8;

    /// <summary>
    /// 불완전 세션 처리 시 동적 MaxSessionGap 계산에 사용할 가중치
    /// </summary>
    /// <remarks>
    /// 동적 MaxSessionGap = 패키지별 평균 세션 지속 시간 × SessionGapMultiplier
    /// 
    /// 설정 근거:
    /// - 기본값 1.0: 패키지의 평균 사용 시간을 그대로 사용
    /// - 논리: 해당 앱의 실제 사용 패턴을 직접 반영
    /// 
    /// 설계 의도:
    /// - 다음 세션과의 간격이 패키지 평균보다 큰 경우 → 별개 세션으로 판단
    /// - 예: 카카오톡 평균 3분, 다음 세션이 10분 후 → 평균 사용
    /// 
    /// 조정 가능:
    /// - 1.0: 평균 그대로 (현재 설정)
    /// - 1.5: 평균의 1.5배 (약간 여유)
    /// - 2.0: 평균의 2배 (넉넉한 여유)
    /// 
    /// 향후 최적화:
    /// - Sample 로그 분석으로 최적값 도출
    /// - AnalysisOptions로 외부 설정 가능하도록 확장
    /// </remarks>
    private const double SessionGapMultiplier = 1.0;

    /// <summary>
    /// CameraSessionDetector 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="confidenceCalculator">신뢰도 계산기</param>
    /// <param name="sessionSources">세션 소스 목록 (우선순위 순으로 정렬됨)</param>
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
        
        // 3단계: 불완전 세션 처리 (병합 전 처리하여 중복 세션 방지)
        var completedSessions = options.EnableIncompleteSessionHandling
            ? HandleIncompleteSessions(rawSessions, filteredEvents, options)
            : rawSessions;
        
        _logger.LogInformation(
            "[병합 전 세션] 원시: {Raw}개 → 불완전 처리 후: {Completed}개",
            rawSessions.Count, completedSessions.Count);
        
        // 3.5단계: 세션 유효성 검증 (Duration이 음수인 세션 필터링)
        var validSessions = ValidateAndFilterSessions(completedSessions);
        if (validSessions.Count < completedSessions.Count)
        {
            _logger.LogWarning(
                "[세션 유효성 검증] 유효하지 않은 세션 {InvalidCount}개 제거 (EndTime < StartTime)",
                completedSessions.Count - validSessions.Count);
        }
        
        // 4단계: 세션 병합 (모든 세션이 완전 상태에서 병합)
        var mergedSessions = MergeSessions(validSessions, options);
        
        _logger.LogInformation(
            "[병합 후 세션] 병합 전: {Before}개 → 병합 후: {After}개 (감소: {Reduced}개)",
            completedSessions.Count, mergedSessions.Count, completedSessions.Count - mergedSessions.Count);
        
        // 5단계: 세션 완전성 점수 기반 필터링 (Threshold-Based Classification)
        // 참고: 시스템 패키지 필터링은 1단계에서 이미 수행됨 (성능 최적화)
        var finalSessions = mergedSessions
            .Where(s => s.SessionCompletenessScore >= options.MinConfidenceThreshold)
            .ToList();

        _logger.LogInformation(
            "세션 감지 완료: {Count}개 세션",
            finalSessions.Count);

        return finalSessions;
    }

    /// <summary>
    /// 패키지 필터링 적용
    /// </summary>
    /// <remarks>
    /// 성능 최적화: 시스템 패키지를 사전에 제외하여 불필요한 세션 처리 방지
    /// 
    /// 1. 화이트리스트 (우선순위 높음): 사용자 정의 관심 패키지
    /// 2. 블랙리스트: 사용자 정의 제외 패키지
    /// 3. 시스템 패키지: 포렌식 무의미 패키지 (android.system, com.android.systemui)
    /// 
    /// 시스템 패키지 필터링 로직:
    /// - package와 taskRootPackage 모두 확인
    /// - 둘 중 하나라도 시스템 패키지면 제외
    /// - taskRootPackage 확인 이유: usagestats 세션 생성 시 PackageName으로 사용됨
    /// 
    /// 예시:
    ///   이벤트: package=com.sec.android.app.camera, taskRootPackage=com.android.systemui
    ///   → taskRootPackage가 시스템이므로 제외 (세션 PackageName이 systemui가 될 것이므로)
    /// </remarks>
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

        // 시스템 패키지 필터링 (성능 최적화)
        filtered = filtered.Where(e =>
        {
            var package = e.Attributes.GetValueOrDefault("package")?.ToString() ?? string.Empty;
            var taskRootPackage = e.Attributes.GetValueOrDefault("taskRootPackage")?.ToString() ?? string.Empty;
            
            // package와 taskRootPackage 모두 확인 (둘 중 하나라도 시스템이면 제외)
            return !SystemPackages.Contains(package) && 
                   !SystemPackages.Contains(taskRootPackage);
        });

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
        
        // 원시 세션 개수 상세 로깅
        _logger.LogInformation(
            "[원시 세션 통계] 총 {Total}개 - usagestats: {Usagestats}개, media_camera: {MediaCamera}개",
            allRawSessions.Count,
            allRawSessions.Count(s => s.SourceLogTypes.Any(st => st.Contains("usagestats", StringComparison.OrdinalIgnoreCase))),
            allRawSessions.Count(s => s.SourceLogTypes.Any(st => st.Contains("media_camera", StringComparison.OrdinalIgnoreCase))));
        
        // 디버깅: 각 SessionSource별 세션 패키지 출력
        _logger.LogDebug("=== 원시 세션 상세 ===");
        foreach (var session in allRawSessions.OrderBy(s => s.StartTime))
        {
            _logger.LogDebug(
                "[Raw] Package={Package}, Time={StartTime:HH:mm:ss}~{EndTime:HH:mm:ss}, Source={Sources}, Confidence={Confidence:F2}",
                session.PackageName, session.StartTime, session.EndTime,
                string.Join(",", session.SourceLogTypes), session.SessionCompletenessScore);
        }
        
        return allRawSessions;
    }

    /// <summary>
    /// 세션 유효성 검증 및 필터링 (Duration이 음수인 세션 제거)
    /// </summary>
    /// <remarks>
    /// EndTime이 StartTime보다 작은 잘못된 세션을 필터링합니다.
    /// 이는 SessionSource에서 세션 생성 시 발생할 수 있는 문제를 방지하기 위함입니다.
    /// 
    /// 객체지향 설계 원칙:
    /// - 단일 책임 원칙: 세션 유효성 검증만 담당
    /// - 기존 비즈니스 로직에 영향 없음: 필터링만 수행
    /// </remarks>
    private List<CameraSession> ValidateAndFilterSessions(List<CameraSession> sessions)
    {
        var validSessions = new List<CameraSession>();
        var invalidCount = 0;
        
        foreach (var session in sessions)
        {
            // EndTime이 있고, EndTime < StartTime인 경우 필터링
            if (session.EndTime.HasValue && session.EndTime.Value < session.StartTime)
            {
                invalidCount++;
                _logger.LogWarning(
                    "[세션 유효성 검증 실패] SessionId={SessionId}, Package={Package}, StartTime={StartTime:HH:mm:ss.fff}, EndTime={EndTime:HH:mm:ss.fff}, Duration={Duration}ms (음수)",
                    session.SessionId, session.PackageName, session.StartTime, session.EndTime.Value,
                    (session.EndTime.Value - session.StartTime).TotalMilliseconds);
                continue;
            }
            
            validSessions.Add(session);
        }
        
        if (invalidCount > 0)
        {
            _logger.LogInformation(
                "[세션 유효성 검증] 총 {Total}개 중 {Invalid}개 유효하지 않은 세션 제거, {Valid}개 유효한 세션 유지",
                sessions.Count, invalidCount, validSessions.Count);
        }
        
        return validSessions;
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
    private List<CameraSession> MergeSessions(List<CameraSession> sessions, AnalysisOptions options)
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

            // 우선순위 1: usagestats + media_camera 같은 카메라 사용 확인
            if (IsSameCameraUsage(current, session, options))
            {
                // 같은 카메라 사용으로 판단 → 무조건 병합
                _logger.LogDebug(
                    "세션 병합 (같은 카메라 사용): Package1={Package1} (Priority={Priority1}, Sources={Sources1}) + Package2={Package2} (Priority={Priority2}, Sources={Sources2})",
                    current.PackageName, GetSessionPriority(current), string.Join(",", current.SourceLogTypes),
                    session.PackageName, GetSessionPriority(session), string.Join(",", session.SourceLogTypes));

                current = MergeTwoSessions(current, session);
                
                _logger.LogDebug(
                    "병합 결과: Package={Package} (Priority={Priority}, Conf={Conf:F2}, Sources={Sources})",
                    current.PackageName, GetSessionPriority(current), current.SessionCompletenessScore, string.Join(",", current.SourceLogTypes));
                
                continue;
            }

            // 우선순위 2: 시간 겹침 비율 기반 병합 (규칙 2)
            // 조건: 패키지명 일치 또는 Intent 위임 방식 (usagestats + media.camera 쌍, 패키지명 다름)
            bool canMergeByOverlap = CanMergeByOverlapRatio(current, session, options);

            if (canMergeByOverlap)
            {
                var overlapRatio = CalculateOverlapRatio(current, session);
                
                // 병합
                _logger.LogDebug(
                    "세션 병합 (겹침 비율): Package1={Package1} (Priority={Priority1}, Conf={Conf1:F2}, Sources={Sources1}) + Package2={Package2} (Priority={Priority2}, Conf={Conf2:F2}, Sources={Sources2}), 겹침={Overlap:P0}",
                    current.PackageName, GetSessionPriority(current), current.SessionCompletenessScore, string.Join(",", current.SourceLogTypes),
                    session.PackageName, GetSessionPriority(session), session.SessionCompletenessScore, string.Join(",", session.SourceLogTypes),
                    overlapRatio);

                current = MergeTwoSessions(current, session);
                
                _logger.LogDebug(
                    "병합 결과: Package={Package} (Priority={Priority}, Conf={Conf:F2}, Sources={Sources})",
                    current.PackageName, GetSessionPriority(current), current.SessionCompletenessScore, string.Join(",", current.SourceLogTypes));
            }
            else
            {
                // 병합 실패 원인 상세 로깅
                LogMergeFailureReason(current, session, options);
                
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
                string.Join(",", session.SourceLogTypes), session.SessionCompletenessScore);
        }

        return mergedSessions;
    }

    /// <summary>
    /// usagestats와 media_camera가 같은 카메라 사용을 나타내는지 확인
    /// </summary>
    /// <remarks>
    /// 판단 기준:
    /// 1. usagestats + media_camera 쌍
    /// 2. 같은 패키지명 또는 Intent 위임 방식 (패키지명 다름)
    /// 3. 시작 시간 차이 ≤ SameCameraUsageTimeThreshold
    /// 4. 종료 시간 차이 ≤ SameCameraUsageTimeThreshold
    /// 
    /// 설계 의도:
    /// - usagestats는 앱 생명주기를 기록 (ACTIVITY_RESUMED/PAUSED)
    /// - media_camera는 하드웨어 연결을 기록 (CONNECT/DISCONNECT)
    /// - 같은 카메라 사용이지만 로그 소스가 달라 약 1초 시작/종료 차이 발생
    /// - 실측 데이터: 모든 샘플에서 시작 차이 1초, 종료 차이 1초
    /// - Intent 위임 방식: 카카오톡 등이 시스템 카메라를 호출하는 경우, 패키지명이 다르지만 같은 카메라 사용
    /// 
    /// 파라미터:
    /// - SameCameraUsageTimeThreshold: AnalysisOptions에서 설정 가능 (기본값 2.0초)
    /// - 실측 근거: 예비 실험에서 평균 1초, 안전 마진 2배 적용
    /// </remarks>
    private bool IsSameCameraUsage(CameraSession session1, CameraSession session2, AnalysisOptions options)
    {
        // 1. usagestats + media_camera 쌍 확인
        var hasUsagestats1 = session1.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase));
        var hasMediaCamera1 = session1.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase));
        var hasUsagestats2 = session2.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase));
        var hasMediaCamera2 = session2.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase));
        
        // usagestats + media_camera 쌍이 아니면 false
        if (!((hasUsagestats1 && hasMediaCamera2) || (hasMediaCamera1 && hasUsagestats2)))
            return false;
        
        // 2. 같은 패키지 확인 또는 Intent 위임 방식 확인
        bool packageMatch = string.Equals(session1.PackageName, session2.PackageName, StringComparison.OrdinalIgnoreCase);
        bool isIntentDelegation = IsIntentDelegationPair(session1, session2);
        
        if (!packageMatch && !isIntentDelegation)
            return false;
        
        // 3. 시작 시간 차이 확인 (설정 가능한 임계값 사용)
        var threshold = options.SameCameraUsageTimeThreshold.TotalSeconds;
        var startDiff = Math.Abs((session1.StartTime - session2.StartTime).TotalSeconds);
        if (startDiff > threshold)
            return false;
        
        // 4. 종료 시간 차이 확인 (설정 가능한 임계값 사용)
        if (!session1.EndTime.HasValue || !session2.EndTime.HasValue)
            return false;
            
        var endDiff = Math.Abs((session1.EndTime.Value - session2.EndTime.Value).TotalSeconds);
        if (endDiff > threshold)
            return false;
        
        _logger.LogDebug(
            "같은 카메라 사용 감지: Package1={Package1}, Package2={Package2}, Intent위임={IntentDelegation}, 시작차이={StartDiff:F1}초, 종료차이={EndDiff:F1}초 (임계값={Threshold:F1}초)",
            session1.PackageName, session2.PackageName, isIntentDelegation, startDiff, endDiff, threshold);
        
        return true; // 모든 조건 만족 → 같은 카메라 사용
    }
    
    /// <summary>
    /// 규칙 2: 시간 겹침 비율 기반 병합 가능 여부 확인
    /// </summary>
    /// <remarks>
    /// 병합 조건 (논문 제4장 제3절 표 7):
    /// 1. 패키지명 일치 또는 Intent 위임 방식 (usagestats + media.camera 쌍, 패키지명 다름)
    /// 2. 카메라 디바이스 ID 일치하거나, 둘 중 하나 이상이 null
    /// 3. EndTime 존재 (두 세션 모두)
    /// 4. 시간 겹침 비율 ≥ 80%
    /// 
    /// Intent 위임 방식: usagestats 세션과 media.camera 세션의 쌍이며 패키지명이 다른 경우
    /// </remarks>
    private bool CanMergeByOverlapRatio(CameraSession session1, CameraSession session2, AnalysisOptions options)
    {
        // 조건 3: EndTime 존재 확인
        if (!session1.EndTime.HasValue || !session2.EndTime.HasValue)
            return false;
        
        // 조건 1: 패키지명 일치 또는 Intent 위임 방식 확인
        bool packageMatch = string.Equals(session1.PackageName, session2.PackageName, StringComparison.OrdinalIgnoreCase);
        bool isIntentDelegation = IsIntentDelegationPair(session1, session2);
        
        if (!packageMatch && !isIntentDelegation)
        {
            // 패키지명도 다르고 Intent 위임 방식도 아님
            return false;
        }
        
        // 조건 2: 카메라 디바이스 ID 확인
        if (session1.CameraDeviceIds != null && session1.CameraDeviceIds.Count > 0 &&
            session2.CameraDeviceIds != null && session2.CameraDeviceIds.Count > 0)
        {
            // 두 세션 모두 device ID가 있는 경우: 일치해야 함
            var devices1 = session1.CameraDeviceIds.ToHashSet();
            var devices2 = session2.CameraDeviceIds.ToHashSet();
            
            if (!devices1.Overlaps(devices2))
            {
                // 서로 다른 카메라 디바이스 (전면/후면 전환) → 병합 불가
                return false;
            }
        }
        // 둘 중 하나 이상이 null이면 조건 2 통과 (usagestats 세션은 CameraDeviceIds가 null)
        
        // 조건 4: 시간 겹침 비율 확인
        var overlapRatio = CalculateOverlapRatio(session1, session2);
        return overlapRatio >= MinOverlapRatio;
    }
    
    /// <summary>
    /// 병합 실패 원인 상세 로깅
    /// </summary>
    private void LogMergeFailureReason(CameraSession session1, CameraSession session2, AnalysisOptions options)
    {
        var hasUsagestats1 = session1.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase));
        var hasMediaCamera1 = session1.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase));
        var hasUsagestats2 = session2.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase));
        var hasMediaCamera2 = session2.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase));
        
        var isUsagestatsMediaCameraPair = (hasUsagestats1 && hasMediaCamera2) || (hasMediaCamera1 && hasUsagestats2);
        var packageMatch = string.Equals(session1.PackageName, session2.PackageName, StringComparison.OrdinalIgnoreCase);
        var isIntentDelegation = IsIntentDelegationPair(session1, session2);
        
        var startDiff = Math.Abs((session1.StartTime - session2.StartTime).TotalSeconds);
        var threshold = options.SameCameraUsageTimeThreshold.TotalSeconds;
        
        var overlapRatio = 0.0;
        if (session1.EndTime.HasValue && session2.EndTime.HasValue)
        {
            overlapRatio = CalculateOverlapRatio(session1, session2);
        }
        
        _logger.LogInformation(
            "[병합 실패] Session1: Package={Package1}, Sources={Sources1}, Time={Start1:HH:mm:ss.fff}~{End1:HH:mm:ss.fff}, DeviceIds={DeviceIds1} | " +
            "Session2: Package={Package2}, Sources={Sources2}, Time={Start2:HH:mm:ss.fff}~{End2:HH:mm:ss.fff}, DeviceIds={DeviceIds2} | " +
            "시작차이={StartDiff:F2}초, 겹침비율={Overlap:P1}, 패키지일치={PackageMatch}, Intent위임={IntentDelegation}, usagestats-media쌍={IsPair}",
            session1.PackageName, string.Join(",", session1.SourceLogTypes), session1.StartTime, session1.EndTime,
            session1.CameraDeviceIds != null ? string.Join(",", session1.CameraDeviceIds) : "null",
            session2.PackageName, string.Join(",", session2.SourceLogTypes), session2.StartTime, session2.EndTime,
            session2.CameraDeviceIds != null ? string.Join(",", session2.CameraDeviceIds) : "null",
            startDiff, overlapRatio, packageMatch, isIntentDelegation, isUsagestatsMediaCameraPair);
        
        // IsSameCameraUsage 실패 원인
        if (isUsagestatsMediaCameraPair && packageMatch)
        {
            if (startDiff > threshold)
            {
                _logger.LogInformation(
                    "[IsSameCameraUsage 실패] 시작 시간 차이 초과: {StartDiff:F2}초 > {Threshold:F2}초",
                    startDiff, threshold);
            }
            else if (!session1.EndTime.HasValue || !session2.EndTime.HasValue)
            {
                _logger.LogInformation(
                    "[IsSameCameraUsage 실패] EndTime 없음: Session1.EndTime={End1}, Session2.EndTime={End2}",
                    session1.EndTime.HasValue ? session1.EndTime.Value.ToString("HH:mm:ss.fff") : "null",
                    session2.EndTime.HasValue ? session2.EndTime.Value.ToString("HH:mm:ss.fff") : "null");
            }
            else
            {
                var endDiff = Math.Abs((session1.EndTime.Value - session2.EndTime.Value).TotalSeconds);
                if (endDiff > threshold)
                {
                    _logger.LogInformation(
                        "[IsSameCameraUsage 실패] 종료 시간 차이 초과: {EndDiff:F2}초 > {Threshold:F2}초",
                        endDiff, threshold);
                }
            }
        }
        
        // CanMergeByOverlapRatio 실패 원인
        if (!session1.EndTime.HasValue || !session2.EndTime.HasValue)
        {
            _logger.LogInformation(
                "[CanMergeByOverlapRatio 실패] EndTime 없음: Session1.EndTime={End1}, Session2.EndTime={End2}",
                session1.EndTime.HasValue ? session1.EndTime.Value.ToString("HH:mm:ss.fff") : "null",
                session2.EndTime.HasValue ? session2.EndTime.Value.ToString("HH:mm:ss.fff") : "null");
        }
        else if (!packageMatch && !isIntentDelegation)
        {
            _logger.LogInformation(
                "[CanMergeByOverlapRatio 실패] 패키지명 불일치 및 Intent 위임 아님: Package1={Package1}, Package2={Package2}",
                session1.PackageName, session2.PackageName);
        }
        else if (session1.CameraDeviceIds != null && session1.CameraDeviceIds.Count > 0 &&
                 session2.CameraDeviceIds != null && session2.CameraDeviceIds.Count > 0)
        {
            var devices1 = session1.CameraDeviceIds.ToHashSet();
            var devices2 = session2.CameraDeviceIds.ToHashSet();
            if (!devices1.Overlaps(devices2))
            {
                _logger.LogInformation(
                    "[CanMergeByOverlapRatio 실패] 카메라 디바이스 ID 불일치: DeviceIds1=[{Devices1}], DeviceIds2=[{Devices2}]",
                    string.Join(",", devices1), string.Join(",", devices2));
            }
        }
        else if (overlapRatio < MinOverlapRatio)
        {
            _logger.LogInformation(
                "[CanMergeByOverlapRatio 실패] 겹침 비율 부족: {Overlap:P1} < {MinOverlap:P0}",
                overlapRatio, MinOverlapRatio);
        }
    }
    
    /// <summary>
    /// Intent 위임 방식 쌍인지 확인 (usagestats 세션과 media.camera 세션의 쌍, 패키지명 다름)
    /// </summary>
    /// <remarks>
    /// Intent 위임 방식 앱이 시스템 카메라를 호출하는 경우:
    /// - usagestats 세션: taskRootPackage 기반으로 실제 앱 패키지명 (예: com.kakao.talk)
    /// - media.camera 세션: package 속성 기반으로 시스템 카메라 패키지명 (예: com.sec.android.app.camera)
    /// 
    /// 판정 기준:
    /// - 한 세션이 usagestats를 포함하고, 다른 세션이 media.camera를 포함
    /// - 패키지명이 다름
    /// </remarks>
    private bool IsIntentDelegationPair(CameraSession session1, CameraSession session2)
    {
        // 세션의 SourceLogTypes 확인
        var hasUsagestats1 = session1.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase));
        var hasMediaCamera1 = session1.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase));
        var hasUsagestats2 = session2.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase));
        var hasMediaCamera2 = session2.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase));
        
        // usagestats 세션과 media.camera 세션의 쌍인지 확인
        bool isUsagestatsMediaCameraPair = (hasUsagestats1 && hasMediaCamera2) || (hasMediaCamera1 && hasUsagestats2);
        
        // 패키지명이 다른 경우만 Intent 위임 방식으로 판정
        bool hasDifferentPackage = !string.Equals(session1.PackageName, session2.PackageName, StringComparison.OrdinalIgnoreCase);
        
        return isUsagestatsMediaCameraPair && hasDifferentPackage;
    }
    
    /// <summary>
    /// 두 세션의 겹침 비율 계산
    /// </summary>
    /// <remarks>
    /// 특별 처리:
    /// - Duration=0 세션(MissingStart): 해당 시각이 다른 세션의 [StartTime, EndTime] 구간 내에 포함되면 1.0 반환
    /// - 일반 세션: 겹침 비율을 수식으로 계산
    /// 
    /// Device ID 조건:
    /// - 두 세션 모두 CameraDeviceIds가 존재하고 서로 다른 device ID만 있는 경우, 0.0 반환
    /// - 이는 전면/후면 카메라 전환처럼 물리적으로 다른 카메라 사용을 별도 세션으로 유지하기 위함
    /// - usagestats 세션(CameraDeviceIds=null)과의 병합은 허용됨
    /// </remarks>
    private double CalculateOverlapRatio(CameraSession session1, CameraSession session2)
    {
        // 불완전 세션은 겹침 계산 불가
        if (!session1.EndTime.HasValue || !session2.EndTime.HasValue)
            return 0.0;

        // Device ID 조건: 서로 다른 device ID는 별도 세션으로 처리
        // (전면/후면 카메라 전환은 별도 세션)
        if (session1.CameraDeviceIds != null && session1.CameraDeviceIds.Count > 0 &&
            session2.CameraDeviceIds != null && session2.CameraDeviceIds.Count > 0)
        {
            // 두 세션의 device ID 집합이 완전히 겹치지 않으면 다른 카메라
            var devices1 = session1.CameraDeviceIds.ToHashSet();
            var devices2 = session2.CameraDeviceIds.ToHashSet();
            
            if (!devices1.Overlaps(devices2))
            {
                _logger.LogDebug(
                    "서로 다른 카메라 디바이스 감지: Package={Package}, Device1=[{Devices1}], Device2=[{Devices2}]",
                    session1.PackageName,
                    string.Join(", ", devices1),
                    string.Join(", ", devices2));
                return 0.0; // 겹침 없음
            }
        }

        var start1 = session1.StartTime;
        var end1 = session1.EndTime.Value;
        var start2 = session2.StartTime;
        var end2 = session2.EndTime.Value;

        // Duration=0 세션 (MissingStart) 특별 처리
        var duration1 = (end1 - start1).TotalSeconds;
        var duration2 = (end2 - start2).TotalSeconds;
        
        // session1이 Duration=0인 경우
        if (duration1 == 0)
        {
            // session1의 시각이 session2 범위 내 포함 여부
            if (end1 >= start2 && end1 <= end2)
            {
                _logger.LogDebug(
                    "Duration=0 세션 포함 병합: Package={Package}, Time={Time:HH:mm:ss} in [{Start:HH:mm:ss}, {End:HH:mm:ss}]",
                    session1.PackageName, end1, start2, end2);
                return 1.0; // 완전 포함으로 간주
            }
            return 0.0;
        }
        
        // session2가 Duration=0인 경우
        if (duration2 == 0)
        {
            // session2의 시각이 session1 범위 내 포함 여부
            if (end2 >= start1 && end2 <= end1)
            {
                _logger.LogDebug(
                    "Duration=0 세션 포함 병합: Package={Package}, Time={Time:HH:mm:ss} in [{Start:HH:mm:ss}, {End:HH:mm:ss}]",
                    session2.PackageName, end2, start1, end1);
                return 1.0; // 완전 포함으로 간주
            }
            return 0.0;
        }

        // 일반 세션 간 겹침 비율 계산
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
            primary = session1.SessionCompletenessScore >= session2.SessionCompletenessScore ? session1 : session2;
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

        // CameraDeviceIds 병합 (전환 이력 보존, 순서 유지)
        // 전면/후면 전환 순서가 중요하므로 Distinct/OrderBy 사용하지 않음
        // 예: [20, 21, 20] → 후면 → 전면 → 후면 전환 이력
        var mergedDeviceIds = (primary.CameraDeviceIds ?? Array.Empty<int>())
            .Concat(secondary.CameraDeviceIds ?? Array.Empty<int>())
            .ToList();

        // 완전성 점수 재계산 (더 많은 증거 = 더 높은 완전성)
        var mergedConfidence = Math.Min(
            primary.SessionCompletenessScore + secondary.SessionCompletenessScore * 0.3,
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
            SessionCompletenessScore = mergedConfidence,
            SourceEventIds = mergedEventIds,
            CameraDeviceIds = mergedDeviceIds.Count > 0 ? mergedDeviceIds : null
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

        // 패키지별 평균 세션 지속 시간 계산
        var packageAverageDurations = completeSessions
            .GroupBy(s => s.PackageName)
            .ToDictionary(
                g => g.Key,
                g => TimeSpan.FromSeconds(g.Average(s => s.Duration!.Value.TotalSeconds))
            );

        // 전체 평균 (fallback용)
        var overallAverageDuration = completeSessions.Any()
            ? TimeSpan.FromSeconds(completeSessions.Average(s => s.Duration!.Value.TotalSeconds))
            : TimeSpan.FromMinutes(5); // 기본값

        _logger.LogDebug(
            "평균 지속 시간 계산 완료: 패키지별={PackageCount}개, 전체평균={OverallAvg:F1}분",
            packageAverageDurations.Count, overallAverageDuration.TotalMinutes);

        foreach (var session in incompleteSessions)
        {
            // 해당 패키지의 평균 사용, 없으면 전체 평균 사용
            var averageDuration = packageAverageDurations.TryGetValue(session.PackageName, out var pkgAvg)
                ? pkgAvg
                : overallAverageDuration;

            _logger.LogDebug(
                "세션 {Id} ({Package}) 평균 지속 시간: {Duration:F1}분 (패키지별={IsPackageSpecific})",
                session.SessionId, session.PackageName, averageDuration.TotalMinutes,
                packageAverageDurations.ContainsKey(session.PackageName));

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
    /// <remarks>
    /// 보수적 접근 (Conservative Approach):
    /// 1. 다음 세션이 있고 간격이 합리적 → 다음 세션 직전까지로 추정 (완료)
    /// 2. 다음 세션이 너무 멀거나 없음 → 패키지 평균 사용 시간 기반 추정 (불완전 유지)
    /// </remarks>
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
            
            // 동적 MaxSessionGap 계산 (패키지 평균 기반)
            var dynamicMaxGap = CalculateDynamicMaxSessionGap(averageDuration, options.MaxSessionGap);
            
            if (gap <= dynamicMaxGap)
            {
                // 다음 세션이 합리적인 거리 → 다음 세션 직전까지 사용
                _logger.LogDebug(
                    "세션 {Id} 종료 추정: 다음 세션 시작 전 (간격={Gap:F1}분, 임계값={Threshold:F1}분)",
                    session.SessionId, gap.TotalMinutes, dynamicMaxGap.TotalMinutes);

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
                    SessionCompletenessScore = session.SessionCompletenessScore,
                    SourceEventIds = session.SourceEventIds,
                    CameraDeviceIds = session.CameraDeviceIds
                };
            }
            else
            {
                // 다음 세션이 너무 멂 → 평균 사용 시간 기반 추정
                _logger.LogDebug(
                    "세션 {Id} 종료 추정: 다음 세션이 너무 멂 (간격={Gap:F1}분 > 임계값={Threshold:F1}분), 평균 사용",
                    session.SessionId, gap.TotalMinutes, dynamicMaxGap.TotalMinutes);
                
                var estimatedEnd = session.StartTime + averageDuration;
                
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
                    SessionCompletenessScore = session.SessionCompletenessScore,
                    SourceEventIds = session.SourceEventIds,
                    CameraDeviceIds = session.CameraDeviceIds
                };
            }
        }

        // 2순위: 다음 세션 없음 → 평균 사용 시간 기반 추정
        var estimatedEndTime = session.StartTime + averageDuration;
        
        _logger.LogDebug(
            "세션 {Id} 종료 추정: 다음 세션 없음, 평균 사용 ({Duration:F1}분)",
            session.SessionId, averageDuration.TotalMinutes);

        return new CameraSession
        {
            SessionId = session.SessionId,
            StartTime = session.StartTime,
            EndTime = estimatedEndTime,
            PackageName = session.PackageName,
            ProcessId = session.ProcessId,
            SourceLogTypes = session.SourceLogTypes,
            CaptureEventIds = session.CaptureEventIds,
            StartEventId = session.StartEventId,
            EndEventId = session.EndEventId,
            IncompleteReason = SessionIncompleteReason.LogTruncated,
            SessionCompletenessScore = session.SessionCompletenessScore,
            SourceEventIds = session.SourceEventIds,
            CameraDeviceIds = session.CameraDeviceIds
        };
    }

    /// <summary>
    /// 동적 MaxSessionGap 계산
    /// </summary>
    /// <param name="packageAverage">패키지별 평균 세션 지속 시간</param>
    /// <param name="configuredMax">설정된 최대값 (AnalysisOptions.MaxSessionGap)</param>
    /// <returns>계산된 동적 MaxSessionGap</returns>
    /// <remarks>
    /// 계산 로직:
    /// 1. 기본값 = 패키지 평균 × SessionGapMultiplier (현재 1.0)
    /// 2. 최소값 = 5분 (너무 짧은 경우 방지)
    /// 3. 최대값 = AnalysisOptions.MaxSessionGap (설정값 존중)
    /// 
    /// 예시:
    /// - 패키지 평균 3분 → 3분 (그대로)
    /// - 패키지 평균 1분 → 5분 (최소값 적용)
    /// - 패키지 평균 10분, 설정값 5분 → 5분 (최대값 적용)
    /// </remarks>
    private TimeSpan CalculateDynamicMaxSessionGap(TimeSpan packageAverage, TimeSpan configuredMax)
    {
        // 패키지 평균 × 가중치
        var calculated = TimeSpan.FromMinutes(packageAverage.TotalMinutes * SessionGapMultiplier);
        
        // 최소값: 5분
        var minimum = TimeSpan.FromMinutes(5);
        
        // 최소값과 설정값 사이로 제한
        var result = TimeSpan.FromMinutes(
            Math.Clamp(
                calculated.TotalMinutes,
                minimum.TotalMinutes,
                configuredMax.TotalMinutes
            )
        );
        
        return result;
    }
}
