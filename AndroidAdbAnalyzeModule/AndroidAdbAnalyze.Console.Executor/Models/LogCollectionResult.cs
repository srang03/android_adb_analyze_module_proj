using AndroidAdbAnalyze.Console.Executor.Configuration;

namespace AndroidAdbAnalyze.Console.Executor.Models;

/// <summary>
/// 로그 수집 결과
/// </summary>
public sealed record LogCollectionResult
{
    /// <summary>
    /// 로그 정의
    /// </summary>
    public required LogDefinition LogDefinition { get; init; }
    
    /// <summary>
    /// 수집 성공 여부
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// 수집된 로그 파일 경로 (성공 시)
    /// </summary>
    public string? FilePath { get; init; }
    
    /// <summary>
    /// 로그 파일 크기 (바이트, 성공 시)
    /// </summary>
    public long? FileSizeBytes { get; init; }
    
    /// <summary>
    /// 실행 시간
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }
    
    /// <summary>
    /// 에러 메시지 (실패 시)
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// 예외 (실패 시)
    /// </summary>
    public Exception? Exception { get; init; }
}

/// <summary>
/// 전체 로그 수집 요약
/// </summary>
public sealed record LogCollectionSummary
{
    /// <summary>
    /// 총 로그 수
    /// </summary>
    public int TotalLogs { get; init; }
    
    /// <summary>
    /// 성공한 로그 수
    /// </summary>
    public int SuccessCount { get; init; }
    
    /// <summary>
    /// 실패한 로그 수
    /// </summary>
    public int FailureCount { get; init; }
    
    /// <summary>
    /// 개별 로그 수집 결과
    /// </summary>
    public required IReadOnlyList<LogCollectionResult> Results { get; init; }
    
    /// <summary>
    /// 전체 실행 시간
    /// </summary>
    public TimeSpan TotalExecutionTime { get; init; }
    
    /// <summary>
    /// 출력 디렉토리
    /// </summary>
    public required string OutputDirectory { get; init; }
}

