using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Sessions;

namespace AndroidAdbAnalyze.Analysis.Services.Context;

/// <summary>
/// 세션 컨텍스트 제공자 구현
/// </summary>
/// <remarks>
/// usagestats.log를 베이스로 세션 내 모든 로그의 상관관계를 구축합니다.
/// </remarks>
public sealed class SessionContextProvider : ISessionContextProvider
{
    /// <summary>
    /// 세션 시간 범위 확장 (초 단위)
    /// </summary>
    /// <remarks>
    /// 파일 생성 등 지연되는 이벤트를 포함하기 위해 세션 종료 후 N초까지 확장합니다.
    /// </remarks>
    private const int SESSION_TIME_EXTENSION_SECONDS = 10;
    
    // Activity 상태 문자열 상수
    private const string ACTIVITY_STATE_RESUMED = "ACTIVITY_RESUMED";
    private const string ACTIVITY_STATE_PAUSED = "ACTIVITY_PAUSED";
    
    // Foreground Service 상태 문자열 상수
    private const string SERVICE_STATE_START = "FOREGROUND_SERVICE_START";
    private const string SERVICE_STATE_STOP = "FOREGROUND_SERVICE_STOP";
    
        /// <inheritdoc />
        public SessionContext CreateContext(
            CameraSession session, 
            IReadOnlyList<NormalizedLogEvent> allEvents)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            
            if (allEvents == null)
                throw new ArgumentNullException(nameof(allEvents));
            
            // 세션 시간 범위 계산 (확장 포함)
            var sessionStart = session.StartTime;
        var sessionEnd = session.EndTime?.AddSeconds(SESSION_TIME_EXTENSION_SECONDS) 
                        ?? DateTime.MaxValue;
        
        // 세션 범위 내 이벤트 필터링
        var sessionEvents = allEvents
            .Where(e => e.Timestamp >= sessionStart && e.Timestamp <= sessionEnd)
            .OrderBy(e => e.Timestamp)
            .ToList();
        
        // usagestats 기반 정보 추출
        var activityResumed = ExtractActivityResumedTime(sessionEvents, session.PackageName);
        var activityPaused = ExtractActivityPausedTime(sessionEvents, session.PackageName);
        var foregroundServices = ExtractForegroundServices(sessionEvents, session.PackageName);
        
        // 시간대별 이벤트 그룹화 (1초 단위)
        var timelineEvents = GroupEventsByTimeline(sessionEvents);
        
        return new SessionContext
        {
            Session = session,
            AllEvents = sessionEvents,
            ActivityResumedTime = activityResumed,
            ActivityPausedTime = activityPaused,
            ForegroundServices = foregroundServices,
            TimelineEvents = timelineEvents
        };
    }
    
    /// <summary>
    /// ACTIVITY_RESUMED 시간 추출
    /// </summary>
    private static DateTime? ExtractActivityResumedTime(
        List<NormalizedLogEvent> events, 
        string packageName)
    {
        return events
            .Where(e => e.EventType == LogEventTypes.ACTIVITY_LIFECYCLE)
            .Where(e => e.PackageName == packageName)
            .Where(e => e.Attributes.GetValueOrDefault("activityState")?.ToString() == ACTIVITY_STATE_RESUMED)
            .Select(e => e.Timestamp as DateTime?)
            .FirstOrDefault();
    }
    
    /// <summary>
    /// ACTIVITY_PAUSED 시간 추출
    /// </summary>
    private static DateTime? ExtractActivityPausedTime(
        List<NormalizedLogEvent> events, 
        string packageName)
    {
        return events
            .Where(e => e.EventType == LogEventTypes.ACTIVITY_LIFECYCLE)
            .Where(e => e.PackageName == packageName)
            .Where(e => e.Attributes.GetValueOrDefault("activityState")?.ToString() == ACTIVITY_STATE_PAUSED)
            .Select(e => e.Timestamp as DateTime?)
            .FirstOrDefault();
    }
    
    /// <summary>
    /// FOREGROUND_SERVICE 목록 추출
    /// </summary>
    private static IReadOnlyList<ForegroundServiceInfo> ExtractForegroundServices(
        List<NormalizedLogEvent> events, 
        string packageName)
    {
        // FOREGROUND_SERVICE_START 이벤트 그룹화
        var serviceStarts = events
            .Where(e => e.EventType == LogEventTypes.FOREGROUND_SERVICE)
            .Where(e => e.PackageName == packageName)
            .Where(e => e.Attributes.GetValueOrDefault("serviceState")?.ToString() == SERVICE_STATE_START)
            .ToList();
        
        // FOREGROUND_SERVICE_STOP 이벤트 그룹화
        var serviceStops = events
            .Where(e => e.EventType == LogEventTypes.FOREGROUND_SERVICE)
            .Where(e => e.PackageName == packageName)
            .Where(e => e.Attributes.GetValueOrDefault("serviceState")?.ToString() == SERVICE_STATE_STOP)
            .ToList();
        
        var services = new List<ForegroundServiceInfo>();
        
        foreach (var startEvent in serviceStarts)
        {
            var className = startEvent.Attributes.GetValueOrDefault("className")?.ToString() 
                           ?? string.Empty;
            
            // 매칭되는 STOP 이벤트 찾기 (같은 className, START 이후 시간)
            var stopEvent = serviceStops
                .Where(e => e.Attributes.GetValueOrDefault("className")?.ToString() == className)
                .Where(e => e.Timestamp > startEvent.Timestamp)
                .OrderBy(e => e.Timestamp)
                .FirstOrDefault();
            
            services.Add(new ForegroundServiceInfo
            {
                ServiceClass = className,
                StartTime = startEvent.Timestamp,
                StopTime = stopEvent?.Timestamp
            });
        }
        
        return services;
    }
    
    /// <summary>
    /// 시간대별 이벤트 그룹화 (1초 단위)
    /// </summary>
    private static IReadOnlyDictionary<DateTime, List<NormalizedLogEvent>> GroupEventsByTimeline(
        List<NormalizedLogEvent> events)
    {
        return events
            .GroupBy(e => new DateTime(
                e.Timestamp.Year,
                e.Timestamp.Month,
                e.Timestamp.Day,
                e.Timestamp.Hour,
                e.Timestamp.Minute,
                e.Timestamp.Second))
            .ToDictionary(
                g => g.Key,
                g => g.ToList());
    }
}
