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
    /// 감지된 재부팅 이벤트 수
    /// </summary>
    /// <remarks>
    /// 분석 시간 범위 내 발생한 재부팅 횟수입니다.
    /// 일반적으로 0 또는 1입니다.
    /// CocktailBarService.log가 없으면 0을 반환합니다.
    /// </remarks>
    public int TotalRebootEvents { get; init; }
    
    /// <summary>
    /// 중복 제거된 이벤트 수
    /// </summary>
    public int DeduplicatedEvents { get; init; }
    
    /// <summary>
    /// 분석 소요 시간 (중복 제거, 세션 탐지, 촬영 탐지)
    /// </summary>
    /// <remarks>
    /// 이 시간은 순수 분석 로직 실행 시간만 포함합니다.
    /// 로그 파싱 시간은 포함되지 않습니다.
    /// </remarks>
    public TimeSpan ProcessingTime { get; init; }
    
    /// <summary>
    /// 로그 파싱 소요 시간 (선택적, PipelineService에서 설정)
    /// </summary>
    /// <remarks>
    /// 이 값은 Console.Executor의 PipelineService에서 측정하여 설정됩니다.
    /// Analysis 모듈 단독 사용 시에는 null입니다.
    /// </remarks>
    public TimeSpan? ParsingTime { get; init; }
    
    /// <summary>
    /// 전체 파이프라인 소요 시간 (선택적, PipelineService에서 설정)
    /// </summary>
    /// <remarks>
    /// 디바이스 확인 + 로그 수집 + 파싱 + 분석의 전체 시간입니다.
    /// Console.Executor의 PipelineService에서 측정하여 설정됩니다.
    /// Analysis 모듈 단독 사용 시에는 null입니다.
    /// </remarks>
    public TimeSpan? TotalPipelineTime { get; init; }
    
    /// <summary>
    /// 분석 시작 시각 (UTC)
    /// </summary>
    public DateTime AnalysisStartTime { get; init; }
    
    /// <summary>
    /// 분석 종료 시각 (UTC)
    /// </summary>
    public DateTime AnalysisEndTime { get; init; }
}
