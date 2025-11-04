using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Options;
using Microsoft.Extensions.Logging;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;

/// <summary>
/// media_camera.log 기반 세션 소스 (CAMERA_CONNECT/DISCONNECT)
/// </summary>
/// <remarks>
/// media_camera 로그에서 CAMERA_CONNECT → CAMERA_DISCONNECT 패턴을 분석하여 세션을 추출합니다.
/// 
/// 장점:
/// - HAL 레벨 정확성 (실제 카메라 디바이스 사용 여부)
/// - 전면/후면 카메라 구분 가능 (device 0/20/21)
/// - Telegram, Instagram 등 자체 카메라 앱 탐지 가능
/// 
/// 단점:
/// - 최대 1시간 보존 (오래된 데이터 분석 불가)
/// </remarks>
public sealed class MediaCameraSessionSource : ISessionSource
{
    private readonly ILogger<MediaCameraSessionSource> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    
    // 세션 시작/종료 이벤트 타입
    private static readonly HashSet<string> SessionStartTypes = new()
    {
        LogEventTypes.CAMERA_CONNECT
    };
    
    private static readonly HashSet<string> SessionEndTypes = new()
    {
        LogEventTypes.CAMERA_DISCONNECT
    };

    /// <summary>
    /// MediaCameraSessionSource 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="confidenceCalculator">신뢰도 계산기</param>
    public MediaCameraSessionSource(
        ILogger<MediaCameraSessionSource> logger,
        IConfidenceCalculator confidenceCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
    }

    /// <inheritdoc/>
    public int Priority => 50; // Secondary (usagestats 보완)

    /// <inheritdoc/>
    public string SourceName => "media_camera";

    /// <inheritdoc/>
    public IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        var sessions = new List<CameraSession>();
        
        // 패키지별로 그룹화 (모든 이벤트 보존 - 세션 내 중간 이벤트 포함)
        var sessionsByPackage = new Dictionary<string, List<NormalizedLogEvent>>();
        
        foreach (var evt in events)  // 전체 이벤트 순회 (필터링하지 않음)
        {
            if (!evt.Attributes.TryGetValue("package", out var pkgObj))
                continue;

            var packageName = pkgObj?.ToString();
            if (string.IsNullOrEmpty(packageName))
                continue;

            if (!sessionsByPackage.ContainsKey(packageName))
            {
                sessionsByPackage[packageName] = new List<NormalizedLogEvent>();
            }
            
            sessionsByPackage[packageName].Add(evt);
        }

        _logger.LogInformation(
            "[{Source}] 이벤트 그룹화 완료: {PackageCount}개 패키지, {TotalEvents}개 이벤트",
            SourceName, sessionsByPackage.Count, events.Count);

        // 패키지별로 세션 추출
        foreach (var (packageName, packageEvents) in sessionsByPackage)
        {
            // 시간순 정렬
            var sortedEvents = packageEvents.OrderBy(e => e.Timestamp).ToList();
            
            _logger.LogDebug(
                "[{Source}] 패키지 '{Package}' 처리 중: {Count}개 이벤트",
                SourceName, packageName, sortedEvents.Count);

            var packageSessions = ExtractSessionsFromEventSequence(
                sortedEvents, packageName);
            
            sessions.AddRange(packageSessions);
        }

        _logger.LogInformation(
            "[{Source}] 세션 추출 완료: {Count}개",
            SourceName, sessions.Count);
        
        return sessions;
    }

    /// <summary>
    /// 이벤트 시퀀스에서 세션 추출 (CAMERA_CONNECT → CAMERA_DISCONNECT)
    /// </summary>
    private List<CameraSession> ExtractSessionsFromEventSequence(
        List<NormalizedLogEvent> events,
        string packageName)
    {
        var sessions = new List<CameraSession>();
        NormalizedLogEvent? currentStart = null;
        var sessionEvents = new List<NormalizedLogEvent>();

        foreach (var evt in events)
        {
            if (SessionStartTypes.Contains(evt.EventType))
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
            else if (SessionEndTypes.Contains(evt.EventType))
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
                else
                {
                    // 시작 없이 종료 (불완전)
                    sessions.Add(CreateSession(
                        evt, evt, packageName, new List<NormalizedLogEvent> { evt },
                        SessionIncompleteReason.MissingStart));
                }
            }
            else
            {
                // 세션 내 이벤트
                if (currentStart != null)
                {
                    sessionEvents.Add(evt);
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
        List<NormalizedLogEvent> artifactEvents,
        SessionIncompleteReason? incompleteReason)
    {
        var confidence = _confidenceCalculator.CalculateConfidence(artifactEvents);
        
        // ProcessId 추출 시도
        int? processId = null;
        if (startEvent.Attributes.TryGetValue("pid", out var pidObj))
        {
            if (int.TryParse(pidObj?.ToString(), out var pid))
                processId = pid;
        }

        // CameraDeviceIds 추출 (CAMERA_CONNECT 이벤트에서만)
        var deviceIds = artifactEvents
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT)
            .Select(e =>
            {
                _logger.LogDebug(
                    "[MediaCamera] CAMERA_CONNECT 이벤트 검사: EventId={EventId}, Attributes={Attributes}",
                    e.EventId, string.Join(", ", e.Attributes.Select(kv => $"{kv.Key}={kv.Value}")));
                
                if (e.Attributes.TryGetValue("deviceId", out var deviceIdObj) &&
                    int.TryParse(deviceIdObj?.ToString(), out var deviceId))
                {
                    _logger.LogDebug(
                        "[MediaCamera] ✅ deviceId 추출 성공: {DeviceId} from {EventId}",
                        deviceId, e.EventId);
                    return (int?)deviceId;
                }
                
                _logger.LogWarning(
                    "[MediaCamera] ❌ deviceId 추출 실패: EventId={EventId}, Available keys={Keys}",
                    e.EventId, string.Join(", ", e.Attributes.Keys));
                return null;
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        
        _logger.LogInformation(
            "[MediaCamera] DeviceIds 추출 완료: Package={Package}, Count={Count}, Values=[{Values}]",
            packageName, deviceIds.Count, string.Join(", ", deviceIds));

        // SourceLogTypes에 source name 사용 (priority 계산을 위해)
        var sourceLogTypes = new List<string> { SourceName };

        return new CameraSession
        {
            SessionId = Guid.NewGuid(),
            StartTime = startEvent.Timestamp,
            EndTime = endEvent?.Timestamp,
            PackageName = packageName,
            ProcessId = processId,
            SourceLogTypes = sourceLogTypes,
            StartEventId = startEvent.EventId,
            EndEventId = endEvent?.EventId,
            IncompleteReason = incompleteReason,
            SessionCompletenessScore = confidence,
            SourceEventIds = artifactEvents.Select(e => e.EventId).ToList(),
            CameraDeviceIds = deviceIds.Count > 0 ? deviceIds : null
        };
    }
}

