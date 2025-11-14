namespace AndroidAdbAnalyze.Console.Executor.Services.Output.Models;

/// <summary>
/// 결과 저장 요약 정보
/// </summary>
public sealed record OutputSummary
{
    /// <summary>
    /// 최종 출력 디렉토리 (날짜/시간 기반 폴더)
    /// </summary>
    public required string OutputDirectory { get; init; }
    
    /// <summary>
    /// HTML 보고서 파일 경로 (생성된 경우)
    /// </summary>
    public string? HtmlReportPath { get; init; }
    
    /// <summary>
    /// 저장된 JSON 파일 경로 목록
    /// </summary>
    public IReadOnlyList<string> JsonFilePaths { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// 원본 로그 디렉토리 경로
    /// </summary>
    public string? RawLogsDirectory { get; init; }
    
    /// <summary>
    /// 저장 성공 여부
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// 에러 메시지 (실패 시)
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// 저장된 총 파일 수
    /// </summary>
    public int TotalFilesCreated { get; init; }
}

