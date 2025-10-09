namespace AndroidAdbAnalyze.Analysis.Models.Deduplication;

/// <summary>
/// 중복 제거 정보 (중복으로 판단된 이벤트 그룹)
/// </summary>
public sealed class DeduplicationInfo
{
    /// <summary>
    /// 대표 이벤트 ID (가장 정보가 많은 이벤트)
    /// </summary>
    public Guid RepresentativeEventId { get; init; }
    
    /// <summary>
    /// 중복으로 판단되어 제거된 이벤트 ID 목록
    /// </summary>
    public IReadOnlyList<Guid> DuplicateEventIds { get; init; } = Array.Empty<Guid>();
    
    /// <summary>
    /// 중복 판단 이유 (예: "시간 차이 50ms, 속성 일치율 95%")
    /// </summary>
    public string Reason { get; init; } = string.Empty;
    
    /// <summary>
    /// 유사도 점수 (0.0 ~ 1.0)
    /// </summary>
    public double Similarity { get; init; }
}
