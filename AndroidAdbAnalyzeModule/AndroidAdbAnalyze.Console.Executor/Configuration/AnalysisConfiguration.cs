namespace AndroidAdbAnalyze.Console.Executor.Configuration;

/// <summary>
/// 분석 설정 (appsettings.json의 "Analysis" 섹션)
/// </summary>
public sealed class AnalysisConfiguration
{
    /// <summary>
    /// 분석 설정 YAML 파일 경로 (null이면 기본 설정 사용)
    /// </summary>
    public string? ConfigFile { get; set; }
    
    /// <summary>
    /// 최소 신뢰도 임계값 (0.0 ~ 1.0)
    /// </summary>
    public double MinConfidenceThreshold { get; set; } = 0.3;
    
    /// <summary>
    /// 이벤트 상관관계 윈도우 (초)
    /// </summary>
    public int EventCorrelationWindowSeconds { get; set; } = 30;
    
    /// <summary>
    /// 세션 간 최대 간격 (분)
    /// </summary>
    public int MaxSessionGapMinutes { get; set; } = 5;
    
    /// <summary>
    /// 중복 제거 유사도 임계값 (0.0 ~ 1.0)
    /// </summary>
    public double DeduplicationSimilarityThreshold { get; set; } = 0.8;
}

