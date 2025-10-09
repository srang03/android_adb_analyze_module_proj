using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Options;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Sessions;

/// <summary>
/// usagestats.log 기반 세션 소스 (ACTIVITY_RESUMED/PAUSED)
/// </summary>
/// <remarks>
/// usagestats 로그에서 ACTIVITY_RESUMED → ACTIVITY_PAUSED/STOPPED 패턴을 분석하여 세션을 추출합니다.
/// 
/// 장점:
/// - 24시간 보존 (재부팅 후에도 분석 가능)
/// - taskRootPackage 기반 정확한 앱 구분 (카카오톡, 텔레그램 등)
/// 
/// 단점:
/// - Telegram, Instagram 등 자체 카메라 앱은 ACTIVITY 없음 (media_camera로 보완 필요)
/// </remarks>
public sealed class UsagestatsSessionSource : ISessionSource
{
    private readonly ILogger<UsagestatsSessionSource> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    
    // 카메라 앱 패키지 목록
    private static readonly HashSet<string> CameraPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.sec.android.app.camera",    // 기본 카메라
        "com.peace.SilentCamera",        // 무음 카메라
    };
    
    // 카메라 사용 앱 목록 (taskRootPackage 기반)
    private static readonly HashSet<string> CameraUsingApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "com.kakao.talk",                // 카카오톡
        "com.samsung.android.messaging", // 메시지
    };
    
    // Activity 이벤트 타입
    private static readonly HashSet<string> ActivityResumedTypes = new()
    {
        LogEventTypes.ACTIVITY_RESUMED
    };
    
    private static readonly HashSet<string> ActivityEndTypes = new()
    {
        LogEventTypes.ACTIVITY_PAUSED,
        LogEventTypes.ACTIVITY_STOPPED
    };

    public UsagestatsSessionSource(
        ILogger<UsagestatsSessionSource> logger,
        IConfidenceCalculator confidenceCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
    }

    /// <inheritdoc/>
    public int Priority => 100; // Primary (usagestats 우선)

    /// <inheritdoc/>
    public string SourceName => "usagestats";

    /// <inheritdoc/>
    public IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        var sessions = new List<CameraSession>();
        
        // Activity 이벤트만 필터링
        var activityEvents = events
            .Where(e => ActivityResumedTypes.Contains(e.EventType) || ActivityEndTypes.Contains(e.EventType))
            .OrderBy(e => e.Timestamp)
            .ToList();

        _logger.LogInformation(
            "[{Source}] Activity 이벤트 {Count}개 추출 (전체: {Total}개)",
            SourceName, activityEvents.Count, events.Count);

        // 패키지별로 그룹화 (taskRootPackage 우선)
        var sessionsByPackage = new Dictionary<string, List<NormalizedLogEvent>>();
        
        foreach (var evt in activityEvents)
        {
            // taskRootPackage 추출 (없으면 package 사용)
            string? taskRootPackage = null;
            if (evt.Attributes.TryGetValue("taskRootPackage", out var taskRootObj))
            {
                taskRootPackage = taskRootObj?.ToString();
            }

            string? package = null;
            if (evt.Attributes.TryGetValue("package", out var pkgObj))
            {
                package = pkgObj?.ToString();
            }

            // 카메라 관련 Activity인지 확인
            // package가 카메라 앱인지 확인 (기본 조건)
            // 세션 패키지 결정(카카오톡 등 구분)은 아래에서 taskRootPackage 우선 처리
            bool isCameraActivity = !string.IsNullOrEmpty(package) && CameraPackages.Contains(package);

            if (!isCameraActivity)
                continue;

            // 세션 패키지 결정: taskRootPackage 우선 (카카오톡 등 구분)
            var sessionPackage = !string.IsNullOrEmpty(taskRootPackage) && CameraUsingApps.Contains(taskRootPackage)
                ? taskRootPackage
                : package ?? string.Empty;

            _logger.LogInformation(
                "[{Source}] 세션 패키지 결정: package={Package}, taskRootPackage={TaskRootPackage}, sessionPackage={SessionPackage}, timestamp={Timestamp:HH:mm:ss}, isCameraApp={IsCameraApp}, isCameraUsingApp={IsCameraUsingApp}",
                SourceName, package, taskRootPackage, sessionPackage, evt.Timestamp, 
                !string.IsNullOrEmpty(package) && CameraPackages.Contains(package), 
                !string.IsNullOrEmpty(taskRootPackage) && CameraUsingApps.Contains(taskRootPackage));

            if (string.IsNullOrEmpty(sessionPackage))
                continue;

            if (!sessionsByPackage.ContainsKey(sessionPackage))
            {
                sessionsByPackage[sessionPackage] = new List<NormalizedLogEvent>();
            }
            
            sessionsByPackage[sessionPackage].Add(evt);
        }

        // 패키지별로 세션 추출
        foreach (var (packageName, packageEvents) in sessionsByPackage)
        {
            _logger.LogDebug(
                "[{Source}] 패키지 '{Package}' 처리 중: {Count}개 이벤트",
                SourceName, packageName, packageEvents.Count);

            var packageSessions = ExtractSessionsFromActivitySequence(
                packageEvents, packageName);
            
            sessions.AddRange(packageSessions);
        }

        _logger.LogInformation(
            "[{Source}] 세션 추출 완료: {Count}개",
            SourceName, sessions.Count);
        
        return sessions;
    }

    /// <summary>
    /// Activity 이벤트 시퀀스에서 세션 추출 (ACTIVITY_RESUMED → PAUSED/STOPPED)
    /// </summary>
    private List<CameraSession> ExtractSessionsFromActivitySequence(
        List<NormalizedLogEvent> events,
        string packageName)
    {
        var sessions = new List<CameraSession>();
        NormalizedLogEvent? currentStart = null;
        var sessionEvents = new List<NormalizedLogEvent>();

        foreach (var evt in events)
        {
            if (ActivityResumedTypes.Contains(evt.EventType))
            {
                // 새 세션 시작
                if (currentStart != null)
                {
                    // 이전 세션 종료 (불완전)
                    sessions.Add(CreateSession(
                        currentStart, null, packageName, sessionEvents, 
                        SessionIncompleteReason.MissingEnd));
                }

                currentStart = evt;
                sessionEvents = new List<NormalizedLogEvent> { evt };
            }
            else if (ActivityEndTypes.Contains(evt.EventType))
            {
                // 세션 종료
                if (currentStart != null)
                {
                    sessionEvents.Add(evt);
                    sessions.Add(CreateSession(
                        currentStart, evt, packageName, sessionEvents, null));
                    
                    currentStart = null;
                    sessionEvents = new List<NormalizedLogEvent>();
                }
            }
        }

        // 마지막 세션이 닫히지 않은 경우
        if (currentStart != null)
        {
            sessions.Add(CreateSession(
                currentStart, null, packageName, sessionEvents,
                SessionIncompleteReason.MissingEnd));
        }

        return sessions;
    }

    /// <summary>
    /// CameraSession 인스턴스 생성
    /// </summary>
    private CameraSession CreateSession(
        NormalizedLogEvent startEvent,
        NormalizedLogEvent? endEvent,
        string packageName,
        List<NormalizedLogEvent> evidenceEvents,
        SessionIncompleteReason? incompleteReason)
    {
        var confidence = _confidenceCalculator.CalculateConfidence(evidenceEvents);
        
        // SourceLogTypes에 source name 사용 (priority 계산을 위해)
        var sourceLogTypes = new List<string> { SourceName };

        return new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = startEvent.Timestamp,
            EndTime = endEvent?.Timestamp,
            PackageName = packageName,
            ProcessId = null, // usagestats에는 pid 정보 없음
            SourceLogTypes = sourceLogTypes,
            StartEventId = startEvent.EventId,
            EndEventId = endEvent?.EventId,
            IncompleteReason = incompleteReason,
            ConfidenceScore = confidence,
            SourceEventIds = evidenceEvents.Select(e => e.EventId).ToList()
        };
    }
}

