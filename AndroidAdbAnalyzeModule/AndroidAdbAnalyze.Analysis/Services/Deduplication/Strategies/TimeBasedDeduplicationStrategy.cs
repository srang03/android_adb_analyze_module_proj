using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Interfaces;

namespace AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;

/// <summary>
/// 시간 기반 중복 판정 전략 (기본 전략)
/// </summary>
/// <remarks>
/// 시간 근접성과 속성 유사도를 기반으로 중복을 판정합니다.
/// 특정 전략이 없는 모든 이벤트 타입에 적용됩니다.
/// </remarks>
public sealed class TimeBasedDeduplicationStrategy : IDeduplicationStrategy
{
    private readonly int _timeThresholdMs;
    private const double SimilarityThreshold = 0.8; // 80% 유사도

    /// <summary>
    /// TimeBasedDeduplicationStrategy 생성자
    /// </summary>
    /// <param name="timeThresholdMs">시간 임계값 (밀리초)</param>
    public TimeBasedDeduplicationStrategy(int timeThresholdMs = 200)
    {
        _timeThresholdMs = timeThresholdMs;
    }

    /// <inheritdoc/>
    public bool IsDuplicate(NormalizedLogEvent event1, NormalizedLogEvent event2)
    {
        if (event1 == null || event2 == null)
            return false;

        // 1. 시간 근접성 확인
        var timeDiff = Math.Abs((event1.Timestamp - event2.Timestamp).TotalMilliseconds);
        if (timeDiff > _timeThresholdMs)
            return false;

        // 2. 속성 유사도 확인 (Jaccard Similarity)
        var similarity = CalculateJaccardSimilarity(event1.Attributes, event2.Attributes);
        return similarity >= SimilarityThreshold;
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

        // 교집합: 같은 키에 같은 값
        var intersection = keys1.Intersect(keys2).Count(key =>
            attrs1[key]?.ToString() == attrs2[key]?.ToString());
        
        // 합집합: 모든 고유 키
        var union = keys1.Union(keys2).Count();

        return union == 0 ? 0.0 : (double)intersection / union;
    }
}

