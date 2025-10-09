namespace AndroidAdbAnalyze.Analysis.Models.Visualization;

/// <summary>
/// 타임라인 차트용 데이터 항목
/// </summary>
public sealed class TimelineItem
{
    /// <summary>
    /// 이벤트 ID
    /// </summary>
    public Guid EventId { get; init; }
    
    /// <summary>
    /// 이벤트 타입 (예: "카메라 세션", "촬영")
    /// </summary>
    public string EventType { get; init; } = string.Empty;
    
    /// <summary>
    /// 시작 시간 (UTC)
    /// </summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>
    /// 종료 시간 (UTC, null이면 순간 이벤트)
    /// </summary>
    public DateTime? EndTime { get; init; }
    
    /// <summary>
    /// 패키지명
    /// </summary>
    public string PackageName { get; init; } = string.Empty;
    
    /// <summary>
    /// 표시할 라벨 (예: "카메라 촬영 #3")
    /// </summary>
    public string Label { get; init; } = string.Empty;
    
    /// <summary>
    /// 신뢰도 점수
    /// </summary>
    public double ConfidenceScore { get; init; }
    
    /// <summary>
    /// UI 표시용 색상 힌트 (예: "green", "red")
    /// </summary>
    public string? ColorHint { get; init; }
    
    /// <summary>
    /// 추가 메타데이터
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();
}
