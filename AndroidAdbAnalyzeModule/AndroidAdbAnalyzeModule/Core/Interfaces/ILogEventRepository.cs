using AndroidAdbAnalyzeModule.Core.Models;

namespace AndroidAdbAnalyzeModule.Core.Interfaces;

/// <summary>
/// 로그 이벤트 저장소 인터페이스
/// </summary>
public interface ILogEventRepository
{
    /// <summary>
    /// 단일 이벤트 저장
    /// </summary>
    Task<bool> SaveEventAsync(NormalizedLogEvent logEvent);

    /// <summary>
    /// 여러 이벤트 배치 저장
    /// </summary>
    Task<int> SaveEventsAsync(IEnumerable<NormalizedLogEvent> events);

    /// <summary>
    /// 시간 범위로 이벤트 조회
    /// </summary>
    Task<IEnumerable<NormalizedLogEvent>> GetEventsByTimeRangeAsync(
        DateTime start,
        DateTime end,
        string? eventType = null);

    /// <summary>
    /// 특정 이벤트와 관련된 이벤트 조회 (시간 윈도우 기반)
    /// </summary>
    Task<IEnumerable<NormalizedLogEvent>> GetRelatedEventsAsync(
        Guid eventId,
        TimeSpan timeWindow);

    /// <summary>
    /// 저장소 비우기
    /// </summary>
    Task ClearAsync();

    /// <summary>
    /// 저장된 전체 이벤트 수
    /// </summary>
    Task<int> GetCountAsync();
}

