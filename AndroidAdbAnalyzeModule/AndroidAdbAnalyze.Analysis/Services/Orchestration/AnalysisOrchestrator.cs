using System.Diagnostics;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Orchestration;

/// <summary>
/// 전체 분석 파이프라인 오케스트레이션 서비스 구현
/// </summary>
public sealed class AnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly IEventDeduplicator _eventDeduplicator;
    private readonly ISessionDetector _sessionDetector;
    private readonly ICaptureDetector _captureDetector;
    private readonly ILogger<AnalysisOrchestrator> _logger;

    /// <summary>
    /// AnalysisOrchestrator 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="eventDeduplicator">이벤트 중복 제거 서비스</param>
    /// <param name="sessionDetector">세션 탐지 서비스</param>
    /// <param name="captureDetector">촬영 탐지 서비스</param>
    /// <param name="logger">로거</param>
    public AnalysisOrchestrator(
        IEventDeduplicator eventDeduplicator,
        ISessionDetector sessionDetector,
        ICaptureDetector captureDetector,
        ILogger<AnalysisOrchestrator> logger)
    {
        _eventDeduplicator = eventDeduplicator ?? throw new ArgumentNullException(nameof(eventDeduplicator));
        _sessionDetector = sessionDetector ?? throw new ArgumentNullException(nameof(sessionDetector));
        _captureDetector = captureDetector ?? throw new ArgumentNullException(nameof(captureDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions? options = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var startTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();
        var warnings = new List<string>();

        _logger.LogInformation("분석 시작: 총 {Count}개 이벤트", events.Count);

        try
        {
            // 옵션 기본값 설정
            options ??= new AnalysisOptions();

            // Phase 1: 중복 제거 (0-20%)
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0);
            _logger.LogDebug("Phase 1/4: 중복 제거 시작");

            var (uniqueEvents, deduplicationInfo) = await Task.Run(() =>
            {
                var result = _eventDeduplicator.Deduplicate(events, out var details);
                return (result, details);
            }, cancellationToken);

            _logger.LogInformation("중복 제거 완료: {Original}개 → {Unique}개 (제거: {Removed}개)",
                events.Count, uniqueEvents.Count, events.Count - uniqueEvents.Count);
            progress?.Report(20);

            // Phase 2: 세션 감지 (20-50%)
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Phase 2/4: 세션 감지 시작");

            var sessions = await Task.Run(() =>
                _sessionDetector.DetectSessions(uniqueEvents, options),
                cancellationToken);

            _logger.LogInformation("세션 감지 완료: {Count}개 세션", sessions.Count);
            progress?.Report(50);

            // Phase 3: 촬영 감지 (50-80%)
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Phase 3/4: 촬영 감지 시작");

            var allCaptures = new List<AndroidAdbAnalyze.Analysis.Models.Events.CameraCaptureEvent>();
            var updatedSessions = new List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>();
            var sessionCount = sessions.Count;

            for (int i = 0; i < sessionCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var session = sessions[i];
                var captures = await Task.Run(() =>
                    _captureDetector.DetectCaptures(session, uniqueEvents, options),
                    cancellationToken);

                allCaptures.AddRange(captures);
                
                // 세션에 촬영 ID 할당 (immutable이므로 새 객체 생성)
                var updatedSession = new AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession
                { 
                    SessionId = session.SessionId,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    PackageName = session.PackageName,
                    ProcessId = session.ProcessId,
                    SourceLogTypes = session.SourceLogTypes,
                    CaptureEventIds = captures.Select(c => c.CaptureId).ToList(),
                    StartEventId = session.StartEventId,
                    EndEventId = session.EndEventId,
                    IncompleteReason = session.IncompleteReason,
                    SessionCompletenessScore = session.SessionCompletenessScore,
                    SourceEventIds = session.SourceEventIds
                };
                updatedSessions.Add(updatedSession);
                
                _logger.LogDebug(
                    "세션 {SessionId} ({Package}): {CaptureCount}개 촬영 할당",
                    session.SessionId, session.PackageName, captures.Count);

                // 세션별 진행률 보고 (50% + 30% * (i+1)/sessionCount)
                var sessionProgress = 50 + (int)((30.0 * (i + 1)) / sessionCount);
                progress?.Report(sessionProgress);
            }

            _logger.LogInformation("촬영 감지 완료: {Count}개 촬영 이벤트", allCaptures.Count);
            
            // Phase 3.1: 세션 간 중복 제거 (세션별 탐지 후 전체 촬영 목록에서 중복 제거)
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Phase 3.1/5: 세션 간 중복 제거 시작");
            
            var (deduplicatedCaptures, removedCaptureIds) = DeduplicateCrossSessionCaptures(allCaptures, updatedSessions, options);
            var originalCaptureCount = allCaptures.Count;
            allCaptures = deduplicatedCaptures;
            
            // 중복 제거된 촬영 ID를 세션에서도 제거
            if (removedCaptureIds.Count > 0)
            {
                updatedSessions = updatedSessions.Select(session =>
                {
                    var remainingCaptureIds = session.CaptureEventIds
                        .Where(id => !removedCaptureIds.Contains(id))
                        .ToList();
                    
                    return new CameraSession
                    {
                        SessionId = session.SessionId,
                        StartTime = session.StartTime,
                        EndTime = session.EndTime,
                        PackageName = session.PackageName,
                        ProcessId = session.ProcessId,
                        SourceLogTypes = session.SourceLogTypes,
                        CaptureEventIds = remainingCaptureIds,
                        StartEventId = session.StartEventId,
                        EndEventId = session.EndEventId,
                        IncompleteReason = session.IncompleteReason,
                        SessionCompletenessScore = session.SessionCompletenessScore,
                        SourceEventIds = session.SourceEventIds
                    };
                }).ToList();
            }
            
            _logger.LogInformation("세션 간 중복 제거 완료: {Original}개 → {Deduplicated}개 (제거: {Removed}개)",
                originalCaptureCount, allCaptures.Count, originalCaptureCount - allCaptures.Count);
            
            progress?.Report(80);

            // Phase 3.5: 재부팅 이벤트 추출 (80-85%)
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Phase 3.5/5: 재부팅 이벤트 추출 시작");

            var rebootEvents = uniqueEvents
                .Where(e => e.EventType == "DEVICE_BOOT_COMPLETED")
                .OrderBy(e => e.Timestamp)
                .Take(1)  // 첫 번째 bootCompleted만 추출
                .ToList();

            _logger.LogInformation("재부팅 이벤트 추출 완료: {Count}개", rebootEvents.Count);
            progress?.Report(85);

            // Phase 4: 통계 계산 (85-100%)
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Phase 4/5: 통계 계산 시작");

            stopwatch.Stop();
            var endTime = DateTime.UtcNow;

            var statistics = new AnalysisStatistics
            {
                TotalSourceEvents = events.Count,
                TotalSessions = updatedSessions.Count,
                CompleteSessions = updatedSessions.Count(s => !s.IsIncomplete),
                IncompleteSessions = updatedSessions.Count(s => s.IsIncomplete),
                TotalCaptureEvents = allCaptures.Count,
                TotalRebootEvents = rebootEvents.Count,
                DeduplicatedEvents = events.Count - uniqueEvents.Count,
                AnalysisStartTime = startTime,
                AnalysisEndTime = endTime,
                ProcessingTime = stopwatch.Elapsed
            };

            _logger.LogInformation("통계 계산 완료: 처리 시간 {Time}ms", stopwatch.ElapsedMilliseconds);
            progress?.Report(100);

            // 최종 결과 생성
            var result = new AnalysisResult
            {
                Success = true,
                Sessions = updatedSessions,
                CaptureEvents = allCaptures,
                RebootEvents = rebootEvents,
                SourceEvents = events,
                DeduplicationDetails = deduplicationInfo,
                DeviceInfo = events.FirstOrDefault()?.DeviceInfo,
                Statistics = statistics,
                Errors = errors,
                Warnings = warnings
            };

            _logger.LogInformation("분석 완료: 성공 (소요 시간: {Time}ms)", stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("분석 취소됨 (소요 시간: {Time}ms)", stopwatch.ElapsedMilliseconds);

            // 취소된 경우 현재까지 결과 반환
            return new AnalysisResult
            {
                Success = false,
                SourceEvents = events,
                Statistics = new AnalysisStatistics
                {
                    TotalSourceEvents = events.Count,
                    AnalysisStartTime = startTime,
                    AnalysisEndTime = DateTime.UtcNow,
                    ProcessingTime = stopwatch.Elapsed
                },
                Errors = new[] { "분석이 취소되었습니다." },
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "분석 중 예외 발생 (소요 시간: {Time}ms)", stopwatch.ElapsedMilliseconds);

            errors.Add($"분석 중 예외 발생: {ex.Message}");

            return new AnalysisResult
            {
                Success = false,
                SourceEvents = events,
                Statistics = new AnalysisStatistics
                {
                    TotalSourceEvents = events.Count,
                    AnalysisStartTime = startTime,
                    AnalysisEndTime = DateTime.UtcNow,
                    ProcessingTime = stopwatch.Elapsed
                },
                Errors = errors,
                Warnings = warnings
            };
        }
    }
    
    /// <summary>
    /// 세션 간 촬영 중복 제거 (전체 촬영 목록에서 중복 제거)
    /// </summary>
    /// <remarks>
    /// 같은 촬영이 여러 세션에서 탐지된 경우를 처리합니다.
    /// 예: 
    /// - usagestats 세션과 media_camera 세션에서 같은 촬영이 탐지된 경우
    /// - Intent 위임 방식: usagestats 세션(com.kakao.talk)과 media.camera 세션(com.sec.android.app.camera)에서 같은 촬영이 탐지된 경우
    /// 
    /// 중복 판정 조건:
    /// 1. 시간 차이 ≤ CaptureDeduplicationWindow (기본 500ms)
    /// 2. PackageName 일치 또는 Intent 위임 방식 감지
    ///    - PackageName이 같으면: 시간 윈도우 내에서 중복 판정
    ///    - PackageName이 다르면: Intent 위임 방식인 경우만 중복 판정
    ///      → usagestats 세션과 media.camera 세션의 쌍
    ///      → 시간 차이가 0ms (같은 시각)인 경우만 중복 판정
    /// 
    /// 우선순위 (SelectBestCaptureFromGroup):
    /// 1. 세션 우선순위: usagestats 세션 > media.camera 세션 (세션 병합과 동일)
    /// 2. 아티팩트 우선순위: VIBRATION_EVENT > PLAYER_EVENT > URI_PERMISSION_GRANT > SILENT_CAMERA_CAPTURE > FOREGROUND_SERVICE
    /// 
    /// 참고: 제4장 제3절 세션 병합 규칙 2와 동일한 Intent 위임 방식 처리 적용
    /// </remarks>
    private (List<CameraCaptureEvent> DeduplicatedCaptures, HashSet<Guid> RemovedCaptureIds) DeduplicateCrossSessionCaptures(
        List<CameraCaptureEvent> captures,
        List<CameraSession> sessions,
        AnalysisOptions options)
    {
        if (captures.Count <= 1)
        {
            return (captures, new HashSet<Guid>());
        }

        // 세션 딕셔너리 생성 (ParentSessionId로 빠른 조회)
        var sessionDict = sessions.ToDictionary(s => s.SessionId, s => s);
        
        var deduplicated = new List<CameraCaptureEvent>();
        var sorted = captures.OrderBy(c => c.CaptureTime).ToList();
        var processed = new HashSet<Guid>();
        var removedCaptureIds = new HashSet<Guid>();
        var timeWindow = options.CaptureDeduplicationWindow;

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
                var timeDiff = next.CaptureTime - current.CaptureTime;
                
                // 조건 1: 시간 차이 체크 (>= 사용으로 경계값 포함)
                if (timeDiff >= timeWindow)
                {
                    break; // 시간 차이가 윈도우 이상이면 중단
                }
                
                // 조건 2: PackageName 일치 또는 Intent 위임 방식 감지
                bool shouldMerge = false;
                
                // 2-1: PackageName 일치 (동일 앱의 촬영)
                if (next.PackageName?.Equals(current.PackageName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    shouldMerge = true;
                }
                // 2-2: Intent 위임 방식 감지 (usagestats 세션과 media.camera 세션의 쌍)
                else if (IsIntentDelegationPair(current, next, sessionDict) && timeDiff.TotalMilliseconds == 0)
                {
                    // Intent 위임 방식: 시간 차이가 0ms (같은 시각)인 경우만 병합
                    shouldMerge = true;
                    
                    _logger.LogDebug(
                        "[CrossSessionDeduplication] Intent 위임 방식 감지: Current={CurrentPackage} (Session={CurrentSessionId}), Next={NextPackage} (Session={NextSessionId}), TimeDiff={TimeDiff}ms",
                        current.PackageName, current.ParentSessionId, next.PackageName, next.ParentSessionId, timeDiff.TotalMilliseconds);
                }
                
                if (shouldMerge)
                {
                    group.Add(next);
                    processed.Add(next.CaptureId);
                    
                    _logger.LogDebug(
                        "[CrossSessionDeduplication] 중복 후보 추가: Time={Time:HH:mm:ss.fff}, Package={Package}, TimeDiff={TimeDiff}ms",
                        next.CaptureTime, next.PackageName, timeDiff.TotalMilliseconds);
                }
                else
                {
                    _logger.LogDebug(
                        "[CrossSessionDeduplication] 중복 제외 (다른 앱): Current={CurrentPackage}, Next={NextPackage}, TimeDiff={TimeDiff}ms",
                        current.PackageName, next.PackageName, timeDiff.TotalMilliseconds);
                }
            }

            // 그룹에서 우선순위가 가장 높은 캡처 선택 (세션 우선순위 고려)
            var best = SelectBestCaptureFromGroup(group, sessionDict);
            deduplicated.Add(best);
            
            // 제거된 촬영 ID 수집
            foreach (var capture in group)
            {
                if (capture.CaptureId != best.CaptureId)
                {
                    removedCaptureIds.Add(capture.CaptureId);
                }
            }

            if (group.Count > 1)
            {
                var artifactTypesSummary = string.Join(", ", best.ArtifactTypes);
                _logger.LogDebug(
                    "[CrossSessionDeduplication] 중복 그룹 통합: {Count}개 → 1개 (Time={Time:HH:mm:ss.fff}, Package={Package}, ArtifactTypes=[{ArtifactTypes}])",
                    group.Count, best.CaptureTime, best.PackageName, artifactTypesSummary);
            }
        }

        return (deduplicated, removedCaptureIds);
    }
    
    /// <summary>
    /// 그룹에서 가장 신뢰도 높은 캡처 선택
    /// </summary>
    /// <remarks>
    /// 우선순위:
    /// 1. 세션 우선순위: usagestats 세션 > media.camera 세션 (세션 병합과 동일)
    /// 2. 아티팩트 우선순위: VIBRATION_EVENT > PLAYER_EVENT > URI_PERMISSION_GRANT > SILENT_CAMERA_CAPTURE > FOREGROUND_SERVICE
    /// 
    /// 세션 병합과 동일하게 usagestats 세션의 촬영을 우선 선택하여, 병합 후 PackageName이 usagestats 기준으로 유지되도록 함
    /// </remarks>
    private CameraCaptureEvent SelectBestCaptureFromGroup(
        List<CameraCaptureEvent> group,
        Dictionary<Guid, CameraSession> sessionDict)
    {
        // 아티팩트 우선순위
        var artifactPriorities = new Dictionary<string, int>
        {
            { "VIBRATION_EVENT", 100 },
            { "PLAYER_EVENT", 80 },
            { "URI_PERMISSION_GRANT", 60 },
            { "SILENT_CAMERA_CAPTURE", 50 },
            { "FOREGROUND_SERVICE", 40 }
        };
        
        // 세션 우선순위 계산 함수
        int GetSessionPriority(CameraCaptureEvent capture)
        {
            if (!sessionDict.TryGetValue(capture.ParentSessionId, out var session))
                return 0;
            
            // 세션 병합과 동일한 우선순위: usagestats = 100, media.camera = 50
            if (session.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase)))
                return 100;
            if (session.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase)))
                return 50;
            return 0;
        }

        // 1순위: 세션 우선순위 (usagestats > media.camera)
        // 2순위: 아티팩트 우선순위
        // 3순위: 촬영 탐지 점수
        return group
            .OrderByDescending(c => GetSessionPriority(c))
            .ThenByDescending(c => c.ArtifactTypes.Max(et => artifactPriorities.GetValueOrDefault(et, 0)))
            .ThenByDescending(c => c.CaptureDetectionScore)
            .First();
    }
    
    /// <summary>
    /// Intent 위임 방식 쌍인지 확인 (usagestats 세션과 media.camera 세션의 쌍)
    /// </summary>
    /// <remarks>
    /// Intent 위임 방식 앱이 시스템 카메라를 호출하는 경우:
    /// - usagestats 세션: taskRootPackage 기반으로 실제 앱 패키지명 (예: com.kakao.talk)
    /// - media.camera 세션: package 속성 기반으로 시스템 카메라 패키지명 (예: com.sec.android.app.camera)
    /// 
    /// 판정 기준:
    /// - 한 촬영의 세션이 usagestats를 포함하고, 다른 촬영의 세션이 media.camera를 포함
    /// - 패키지명이 다름
    /// 
    /// 참고: 제4장 제3절 세션 병합 규칙 2의 Intent 위임 방식 처리와 동일한 로직
    /// </remarks>
    private bool IsIntentDelegationPair(
        CameraCaptureEvent capture1,
        CameraCaptureEvent capture2,
        Dictionary<Guid, CameraSession> sessionDict)
    {
        if (!sessionDict.TryGetValue(capture1.ParentSessionId, out var session1) ||
            !sessionDict.TryGetValue(capture2.ParentSessionId, out var session2))
        {
            return false;
        }
        
        // 세션의 SourceLogTypes 확인
        var hasUsagestats1 = session1.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase));
        var hasMediaCamera1 = session1.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase));
        var hasUsagestats2 = session2.SourceLogTypes.Any(s => s.Contains("usagestats", StringComparison.OrdinalIgnoreCase));
        var hasMediaCamera2 = session2.SourceLogTypes.Any(s => s.Contains("media_camera", StringComparison.OrdinalIgnoreCase));
        
        // usagestats 세션과 media.camera 세션의 쌍인지 확인
        bool isUsagestatsMediaCameraPair = (hasUsagestats1 && hasMediaCamera2) || (hasMediaCamera1 && hasUsagestats2);
        
        // 패키지명이 다른 경우만 Intent 위임 방식으로 판정
        bool hasDifferentPackage = !string.Equals(capture1.PackageName, capture2.PackageName, StringComparison.OrdinalIgnoreCase);
        
        return isUsagestatsMediaCameraPair && hasDifferentPackage;
    }
}
