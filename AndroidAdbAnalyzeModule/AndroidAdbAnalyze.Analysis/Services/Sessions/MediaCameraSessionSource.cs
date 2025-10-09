using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Options;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Sessions;

/// <summary>
/// media_camera.log 기반 세션 소스 (CAMERA_CONNECT/DISCONNECT)
/// </summary>
/// <remarks>
/// media_camera 로그에서 CAMERA_CONNECT → CAMERA_DISCONNECT 패턴을 분석하여 세션을 추출합니다.
/// 
/// 장점:
/// - Telegram, Instagram 등 자체 카메라 앱 탐지 가능
/// 
/// 단점:
/// - 휘발성 로그 (재부팅 시 소실)
/// - taskRootPackage 정보 없음 (카카오톡 등 구분 불가)
/// </remarks>
public sealed class MediaCameraSessionSource : ISessionSource
{
    private readonly ILogger<MediaCameraSessionSource> _logger;
    private readonly IConfidenceCalculator _confidenceCalculator;
    
    // 세션 시작을 나타내는 이벤트 타입
    private static readonly HashSet<string> SessionStartTypes = new()
    {
        LogEventTypes.CAMERA_CONNECT
    };
    
    // 세션 종료를 나타내는 이벤트 타입
    private static readonly HashSet<string> SessionEndTypes = new()
    {
        LogEventTypes.CAMERA_DISCONNECT
    };

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
        
        // 패키지별로 그룹화
        var eventsByPackage = events
            .Where(e => e.Attributes.ContainsKey("package"))
            .GroupBy(e => e.Attributes["package"]?.ToString() ?? string.Empty)
            .ToList();

        foreach (var packageGroup in eventsByPackage)
        {
            var packageName = packageGroup.Key;
            var packageEvents = packageGroup.OrderBy(e => e.Timestamp).ToList();

            _logger.LogDebug(
                "[{Source}] 패키지 '{Package}' 처리 중: {Count}개 이벤트",
                SourceName, packageName, packageEvents.Count);

            var packageSessions = ExtractSessionsFromEventSequence(
                packageEvents, packageName);
            
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
        List<NormalizedLogEvent> evidenceEvents,
        SessionIncompleteReason? incompleteReason)
    {
        var confidence = _confidenceCalculator.CalculateConfidence(evidenceEvents);
        
        // ProcessId 추출 시도
        int? processId = null;
        if (startEvent.Attributes.TryGetValue("pid", out var pidObj))
        {
            if (int.TryParse(pidObj?.ToString(), out var pid))
                processId = pid;
        }

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
            ConfidenceScore = confidence,
            SourceEventIds = evidenceEvents.Select(e => e.EventId).ToList()
        };
    }
}

