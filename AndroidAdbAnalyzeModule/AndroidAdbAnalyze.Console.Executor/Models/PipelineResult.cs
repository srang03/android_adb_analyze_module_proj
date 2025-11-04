using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Console.Executor.Models;

/// <summary>
/// 전체 파이프라인 실행 결과
/// </summary>
public sealed record PipelineResult
{
    /// <summary>
    /// 파이프라인 실행 성공 여부
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// 디바이스 정보
    /// </summary>
    public DeviceInfo? DeviceInfo { get; init; }
    
    /// <summary>
    /// 로그 수집 요약
    /// </summary>
    public LogCollectionSummary? CollectionSummary { get; init; }
    
    /// <summary>
    /// 개별 파일 파싱 결과 (로그 파일명 → 파싱 결과)
    /// </summary>
    public IReadOnlyDictionary<string, ParsingResult> ParsingResults { get; init; } = 
        new Dictionary<string, ParsingResult>();
    
    /// <summary>
    /// 병합된 전체 이벤트 수
    /// </summary>
    public int TotalEventCount { get; init; }
    
    /// <summary>
    /// 분석 결과
    /// </summary>
    public AnalysisResult? AnalysisResult { get; init; }
    
    /// <summary>
    /// 전체 실행 시간
    /// </summary>
    public TimeSpan TotalExecutionTime { get; init; }
    
    /// <summary>
    /// 에러 메시지 (실패 시)
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// 예외 (실패 시)
    /// </summary>
    public Exception? Exception { get; init; }
}

