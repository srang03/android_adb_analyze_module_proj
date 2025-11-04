using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;

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
    /// <para>
    /// 파일 생성, MediaStore 동기화 등 비동기 처리로 인해 지연되는 이벤트를 포함하기 위해 
    /// 세션 종료 후 N초까지 확장합니다.
    /// </para>
    /// <para>
    /// <b>설정 근거:</b> 경험적 값 (실측 데이터 수집 권장)
    /// - DATABASE_INSERT: 파일 쓰기 완료까지 최대 3~5초 지연 가능
    /// - CAMERA_DISCONNECT: HAL 레벨 해제까지 1~2초 지연 가능
    /// - 안전 마진 고려하여 10초 설정
    /// </para>
    /// <para>
    /// <b>주의:</b> 과도한 확장은 다음 세션의 이벤트를 포함하여 오탐을 유발할 수 있습니다.
    /// 실측 데이터 수집을 통한 과학적 근거 확보가 필요합니다.
    /// (참고: Paper/06_doc/분석 보고서/세션_확장_실측_분석_스크립트.md)
    /// </para>
    /// </remarks>
    private const int SESSION_TIME_EXTENSION_SECONDS = 10;
    
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
        
        // usagestats 기반 Foreground Service 정보 추출
        var foregroundServices = ExtractForegroundServices(sessionEvents, session.PackageName);
        
        return new SessionContext
        {
            Session = session,
            AllEvents = sessionEvents,
            ForegroundServices = foregroundServices
        };
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
}
