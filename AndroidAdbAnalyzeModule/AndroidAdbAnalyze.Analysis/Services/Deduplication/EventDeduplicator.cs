using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;
using Microsoft.Extensions.Logging;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Services.Deduplication;

/// <summary>
/// 중복 이벤트 제거 서비스 구현
/// </summary>
/// <remarks>
/// 이벤트 타입별로 적합한 중복 판정 전략을 적용합니다.
/// - 카메라 이벤트: 배타적 이벤트 전략 (package + cameraId + 1초 윈도우)
/// - 기타 이벤트: 시간 기반 전략 (시간 + 속성 유사도)
/// </remarks>
public sealed class EventDeduplicator : IEventDeduplicator
{
    private readonly ILogger<EventDeduplicator> _logger;
    private readonly Dictionary<string, IDeduplicationStrategy> _strategies;
    private readonly IDeduplicationStrategy _defaultStrategy;

    /// <summary>
    /// 이벤트 타입별 시간 임계값 (밀리초) - 중앙 관리
    /// </summary>
    /// <remarks>
    /// 임계값 조정 시 이 딕셔너리만 수정하면 모든 전략에 자동 반영됩니다.
    /// </remarks>
    private static readonly Dictionary<string, int> TimeThresholds = new()
    {
        // 카메라 이벤트: 로그 저장 시점 차이, 백그라운드 동작 고려하여 1초
        { LogEventTypes.CAMERA_CONNECT, 1000 },
        { LogEventTypes.CAMERA_DISCONNECT, 1000 },
        
        // 데이터베이스 이벤트: 비교적 정확한 타이밍
        { LogEventTypes.DATABASE_INSERT, 500 },
        { LogEventTypes.DATABASE_EVENT, 500 },
        { LogEventTypes.MEDIA_INSERT_END, 500 },
        
        // 오디오 플레이어 이벤트: 정밀한 타이밍 필요 (셔터 음 관련)
        { LogEventTypes.PLAYER_CREATED, 100 },
        { LogEventTypes.PLAYER_EVENT, 100 },
        { LogEventTypes.PLAYER_RELEASED, 100 },
        
        // 오디오/미디어 이벤트: 정밀한 타이밍 필요
        { LogEventTypes.MEDIA_EXTRACTOR, 100 },
        
        // URI 권한 이벤트
        { LogEventTypes.URI_PERMISSION_GRANT, 200 },
        { LogEventTypes.URI_PERMISSION_REVOKE, 200 },
    };

    /// <summary>
    /// 기본 시간 임계값 (이벤트 타입별 설정이 없는 경우)
    /// </summary>
    private const int DefaultTimeThreshold = 200;

    public EventDeduplicator(ILogger<EventDeduplicator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // 전략 초기화 - 딕셔너리에서 임계값 조회
        var cameraThreshold = TimeThresholds[LogEventTypes.CAMERA_CONNECT];
        var cameraStrategy = new CameraEventDeduplicationStrategy(cameraThreshold);
        
        _strategies = new Dictionary<string, IDeduplicationStrategy>
        {
            [LogEventTypes.CAMERA_CONNECT] = cameraStrategy,
            [LogEventTypes.CAMERA_DISCONNECT] = cameraStrategy
        };
        
        // 기본 전략: 시간 기반
        _defaultStrategy = new TimeBasedDeduplicationStrategy(DefaultTimeThreshold);
    }

    /// <inheritdoc/>
    public IReadOnlyList<NormalizedLogEvent> Deduplicate(
        IReadOnlyList<NormalizedLogEvent> events,
        out IReadOnlyList<DeduplicationInfo> deduplicationDetails)
    {
        if (events == null || events.Count == 0)
        {
            deduplicationDetails = Array.Empty<DeduplicationInfo>();
            return Array.Empty<NormalizedLogEvent>();
        }

        _logger.LogInformation("중복 제거 시작: {EventCount}개 이벤트", events.Count);

        var deduplicationList = new List<DeduplicationInfo>();
        var deduplicated = new List<NormalizedLogEvent>();

        // EventType별로 그룹화
        var eventsByType = events
            .GroupBy(e => e.EventType)
            .ToList();

        foreach (var typeGroup in eventsByType)
        {
            var eventType = typeGroup.Key;
            var typeEvents = typeGroup.OrderBy(e => e.Timestamp).ToList();

            _logger.LogDebug("EventType '{EventType}' 처리 중: {Count}개", eventType, typeEvents.Count);

            // 전략 기반 그룹화
            var strategy = GetStrategy(eventType);
            var timeGroups = GroupByStrategy(typeEvents, strategy);

            foreach (var timeGroup in timeGroups)
            {
                if (timeGroup.Count == 1)
                {
                    // 중복 없음
                    deduplicated.Add(timeGroup[0]);
                    continue;
                }

                // 중복 발견: 대표 이벤트 선정
                var representative = SelectRepresentative(timeGroup);
                var duplicates = timeGroup.Where(e => e.EventId != representative.EventId).ToList();

                if (duplicates.Count > 0)
                {
                    var similarity = CalculateSimilarity(timeGroup);
                    var reason = $"시간 차이 {GetMaxTimeDiff(timeGroup)}ms, 속성 일치율 {similarity:P0}";

                    deduplicationList.Add(new DeduplicationInfo
                    {
                        RepresentativeEventId = representative.EventId,
                        DuplicateEventIds = duplicates.Select(e => e.EventId).ToList(),
                        Reason = reason,
                        Similarity = similarity
                    });

                    _logger.LogDebug(
                        "중복 제거: EventType={EventType}, 대표={RepId}, 중복={DupCount}개",
                        eventType, representative.EventId, duplicates.Count);
                }

                deduplicated.Add(representative);
            }
        }

        deduplicationDetails = deduplicationList;
        
        _logger.LogInformation(
            "중복 제거 완료: {Original}개 → {Deduplicated}개 (제거: {Removed}개, 중복 그룹: {Groups}개)",
            events.Count, deduplicated.Count, events.Count - deduplicated.Count, deduplicationList.Count);

        return deduplicated;
    }

    /// <summary>
    /// 이벤트 타입에 맞는 중복 판정 전략 반환
    /// </summary>
    /// <remarks>
    /// 1. _strategies 딕셔너리에서 조회 (카메라 이벤트 등 특수 전략)
    /// 2. 없으면 TimeThresholds에서 이벤트 타입별 임계값 조회하여 TimeBasedDeduplicationStrategy 생성
    /// 3. TimeThresholds에도 없으면 기본 전략 반환
    /// 
    /// 전략 인스턴스는 _strategies에 캐시되어 재사용됩니다.
    /// </remarks>
    private IDeduplicationStrategy GetStrategy(string eventType)
    {
        // 1. 이미 생성된 전략 반환 (카메라 이벤트 등)
        if (_strategies.TryGetValue(eventType, out var strategy))
            return strategy;
        
        // 2. TimeThresholds에서 이벤트 타입별 임계값 조회
        if (TimeThresholds.TryGetValue(eventType, out var threshold))
        {
            // 새 전략 생성하고 캐시
            var newStrategy = new TimeBasedDeduplicationStrategy(threshold);
            _strategies[eventType] = newStrategy;
            return newStrategy;
        }
        
        // 3. 기본 전략 반환
        return _defaultStrategy;
    }

    /// <summary>
    /// 전략 기반 그룹화 (Sliding Window)
    /// </summary>
    /// <remarks>
    /// Sliding Window 방식: 현재 그룹의 마지막 이벤트와 새 이벤트를 비교
    /// - Fixed Window (기존): currentGroup[0]와 비교
    /// - Sliding Window (개선): currentGroup.Last()와 비교
    /// 이를 통해 연속된 이벤트 간 시간 차이가 각각 임계값 이내면 같은 그룹으로 판정
    /// </remarks>
    private List<List<NormalizedLogEvent>> GroupByStrategy(
        List<NormalizedLogEvent> events,
        IDeduplicationStrategy strategy)
    {
        if (events.Count == 0)
            return new List<List<NormalizedLogEvent>>();

        var groups = new List<List<NormalizedLogEvent>>();
        var currentGroup = new List<NormalizedLogEvent> { events[0] };

        for (int i = 1; i < events.Count; i++)
        {
            var lastEventInGroup = currentGroup.Last(); // Sliding Window: 마지막 이벤트와 비교
            
            if (strategy.IsDuplicate(lastEventInGroup, events[i]))
            {
                // 같은 그룹 (중복)
                currentGroup.Add(events[i]);
            }
            else
            {
                // 새 그룹 시작
                groups.Add(currentGroup);
                currentGroup = new List<NormalizedLogEvent> { events[i] };
            }
        }

        groups.Add(currentGroup); // 마지막 그룹 추가
        return groups;
    }

    /// <summary>
    /// 대표 이벤트 선정 (가장 많은 정보를 가진 이벤트)
    /// </summary>
    private NormalizedLogEvent SelectRepresentative(List<NormalizedLogEvent> group)
    {
        // 1순위: Attributes 개수가 가장 많은 이벤트
        // 2순위: 시간상 가운데 이벤트 (중간값)
        return group
            .OrderByDescending(e => e.Attributes.Count)
            .ThenBy(e => Math.Abs((e.Timestamp - GetMedianTimestamp(group)).Ticks))
            .First();
    }

    /// <summary>
    /// 그룹 내 이벤트 간 유사도 계산 (Jaccard 유사도)
    /// </summary>
    private double CalculateSimilarity(List<NormalizedLogEvent> group)
    {
        if (group.Count < 2)
            return 1.0;

        var similarities = new List<double>();

        for (int i = 0; i < group.Count - 1; i++)
        {
            for (int j = i + 1; j < group.Count; j++)
            {
                var similarity = CalculateJaccardSimilarity(
                    group[i].Attributes,
                    group[j].Attributes);
                similarities.Add(similarity);
            }
        }

        return similarities.Average();
    }

    /// <summary>
    /// Jaccard 유사도 계산
    /// </summary>
    private double CalculateJaccardSimilarity(
        IReadOnlyDictionary<string, object> attrs1,
        IReadOnlyDictionary<string, object> attrs2)
    {
        if (attrs1.Count == 0 && attrs2.Count == 0)
            return 1.0;

        var keys1 = attrs1.Keys.ToHashSet();
        var keys2 = attrs2.Keys.ToHashSet();

        var intersection = keys1.Intersect(keys2).Count(key => 
            attrs1[key]?.ToString() == attrs2[key]?.ToString());
        var union = keys1.Union(keys2).Count();

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    /// <summary>
    /// 그룹 내 최대 시간 차이 (밀리초)
    /// </summary>
    private long GetMaxTimeDiff(List<NormalizedLogEvent> group)
    {
        if (group.Count < 2)
            return 0;

        var min = group.Min(e => e.Timestamp);
        var max = group.Max(e => e.Timestamp);
        return (long)(max - min).TotalMilliseconds;
    }

    /// <summary>
    /// 중간값 타임스탬프
    /// </summary>
    private DateTime GetMedianTimestamp(List<NormalizedLogEvent> group)
    {
        var ordered = group.OrderBy(e => e.Timestamp).ToList();
        var middleIndex = ordered.Count / 2;
        return ordered[middleIndex].Timestamp;
    }
}
