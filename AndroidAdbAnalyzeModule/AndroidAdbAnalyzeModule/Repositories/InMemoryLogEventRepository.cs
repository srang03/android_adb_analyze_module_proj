using AndroidAdbAnalyzeModule.Core.Interfaces;
using AndroidAdbAnalyzeModule.Core.Models;

namespace AndroidAdbAnalyzeModule.Repositories;

/// <summary>
/// 메모리 기반 로그 이벤트 저장소
/// </summary>
public sealed class InMemoryLogEventRepository : ILogEventRepository, IDisposable
{
    private readonly List<NormalizedLogEvent> _events = new();
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// 단일 이벤트 저장
    /// </summary>
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
    /// 여러 이벤트 일괄 저장
    /// </summary>
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
    /// 시간 범위로 이벤트 조회
    /// </summary>
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
    /// 관련 이벤트 조회 (시간 윈도우 기반)
    /// </summary>
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
    /// 모든 이벤트 삭제
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
    /// 저장된 이벤트 개수 조회
    /// </summary>
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
    /// Dispose 패턴 구현
    /// </summary>
    public void Dispose()
    {
        _lock?.Dispose();
    }
}

