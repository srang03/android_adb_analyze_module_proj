using AndroidAdbAnalyze.Parser.Core.Interfaces;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Parser.Repositories;

/// <summary>
/// <see cref="NormalizedLogEvent"/>를 메모리에 저장하고 조회하는 스레드 안전(thread-safe) 리포지토리입니다.
/// <see cref="ILogEventRepository"/>의 구현체입니다.
/// </summary>
public sealed class InMemoryLogEventRepository : ILogEventRepository, IDisposable
{
    private readonly List<NormalizedLogEvent> _events = new();
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// 단일 <see cref="NormalizedLogEvent"/>를 저장소에 비동기적으로 추가합니다.
    /// </summary>
    /// <param name="logEvent">저장할 로그 이벤트입니다.</param>
    /// <returns>작업이 성공적으로 완료되면 true를 반환하는 Task입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logEvent"/>가 null인 경우 발생합니다.</exception>
    public Task<bool> SaveEventAsync(NormalizedLogEvent logEvent)
    {
        if (logEvent == null)
            throw new ArgumentNullException(nameof(logEvent));

        _lock.EnterWriteLock();
        try
        {
            _events.Add(logEvent);
            return Task.FromResult(true);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 여러 <see cref="NormalizedLogEvent"/>를 저장소에 비동기적으로 일괄 추가합니다.
    /// </summary>
    /// <param name="events">저장할 로그 이벤트의 컬렉션입니다.</param>
    /// <returns>성공적으로 추가된 이벤트의 수를 반환하는 Task입니다.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/>가 null인 경우 발생합니다.</exception>
    public Task<int> SaveEventsAsync(IEnumerable<NormalizedLogEvent> events)
    {
        if (events == null)
            throw new ArgumentNullException(nameof(events));

        var eventList = events.ToList();

        _lock.EnterWriteLock();
        try
        {
            _events.AddRange(eventList);
            return Task.FromResult(eventList.Count);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 지정된 시간 범위 내의 로그 이벤트를 비동기적으로 조회합니다.
    /// </summary>
    /// <param name="start">조회할 시간 범위의 시작 시간입니다. (포함)</param>
    /// <param name="end">조회할 시간 범위의 종료 시간입니다. (포함)</param>
    /// <param name="eventType">필터링할 이벤트 유형입니다. (선택 사항) null인 경우 모든 이벤트 유형을 포함합니다.</param>
    /// <returns>조회 조건과 일치하는 <see cref="NormalizedLogEvent"/>의 열거형을 반환하는 Task입니다.</returns>
    public Task<IEnumerable<NormalizedLogEvent>> GetEventsByTimeRangeAsync(
        DateTime start,
        DateTime end,
        string? eventType = null)
    {
        _lock.EnterReadLock();
        try
        {
            var query = _events
                .Where(e => e.Timestamp >= start && e.Timestamp <= end);

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                query = query.Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult(query.ToList().AsEnumerable());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 지정된 이벤트 ID를 기준으로 특정 시간 창 내의 관련 이벤트를 비동기적으로 조회합니다.
    /// </summary>
    /// <param name="eventId">기준이 되는 이벤트의 고유 ID입니다.</param>
    /// <param name="timeWindow">기준 이벤트의 타임스탬프를 중심으로 앞뒤로 검색할 시간 범위입니다.</param>
    /// <returns>조회 조건과 일치하는 관련 이벤트의 열거형을 반환하는 Task입니다. 기준 이벤트 자체는 결과에 포함되지 않습니다.</returns>
    public Task<IEnumerable<NormalizedLogEvent>> GetRelatedEventsAsync(
        Guid eventId,
        TimeSpan timeWindow)
    {
        _lock.EnterReadLock();
        try
        {
            // 기준 이벤트 찾기
            var targetEvent = _events.FirstOrDefault(e => e.EventId == eventId);
            if (targetEvent == null)
                return Task.FromResult(Enumerable.Empty<NormalizedLogEvent>());

            // 시간 윈도우 내 이벤트 조회
            var startTime = targetEvent.Timestamp.AddTicks(-timeWindow.Ticks);
            var endTime = targetEvent.Timestamp.AddTicks(timeWindow.Ticks);

            var relatedEvents = _events
                .Where(e => e.EventId != eventId)
                .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
                .OrderBy(e => e.Timestamp)
                .ToList();

            return Task.FromResult(relatedEvents.AsEnumerable());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// 저장소의 모든 이벤트를 비동기적으로 삭제합니다.
    /// </summary>
    public Task ClearAsync()
    {
        _lock.EnterWriteLock();
        try
        {
            _events.Clear();
            return Task.CompletedTask;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 저장소에 있는 총 이벤트 수를 비동기적으로 조회합니다.
    /// </summary>
    /// <returns>총 이벤트 수를 반환하는 Task입니다.</returns>
    public Task<int> GetCountAsync()
    {
        _lock.EnterReadLock();
        try
        {
            return Task.FromResult(_events.Count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// <see cref="ReaderWriterLockSlim"/>에서 사용하는 리소스를 해제합니다.
    /// </summary>
    public void Dispose()
    {
        _lock?.Dispose();
    }
}

