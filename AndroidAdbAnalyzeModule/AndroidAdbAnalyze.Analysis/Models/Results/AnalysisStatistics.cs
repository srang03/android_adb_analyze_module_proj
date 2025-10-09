namespace AndroidAdbAnalyze.Analysis.Models.Results;

/// <summary>
/// 분석 통계 정보
/// </summary>
public sealed class AnalysisStatistics
{
    /// <summary>
    /// 처리된 원본 이벤트 수
    /// </summary>
    public int TotalSourceEvents { get; init; }
    
    /// <summary>
    /// 감지된 세션 수
    /// </summary>
    public int TotalSessions { get; init; }
    
    /// <summary>
    /// 완전한 세션 수
    /// </summary>
    public int CompleteSessions { get; init; }
    
    /// <summary>
    /// 불완전한 세션 수 (시작 또는 종료 누락)
    /// </summary>
    public int IncompleteSessions { get; init; }
    
    /// <summary>
    /// 감지된 촬영 이벤트 수
    /// </summary>
    public int TotalCaptureEvents { get; init; }
    
    /// <summary>
    /// 중복 제거된 이벤트 수
    /// </summary>
    public int DeduplicatedEvents { get; init; }
    
    /// <summary>
    /// 분석 소요 시간
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }
    
    /// <summary>
    /// 분석 시작 시각 (UTC)
    /// </summary>
    public DateTime AnalysisStartTime { get; init; }
    
    /// <summary>
    /// 분석 종료 시각 (UTC)
    /// </summary>
    public DateTime AnalysisEndTime { get; init; }
}
