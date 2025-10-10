namespace AndroidAdbAnalyze.Parser.Core.Models;

/// <summary>
/// 파싱 에러 정보
/// </summary>
public sealed class ParsingError
{
    /// <summary>
    /// 에러 발생 라인 번호
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// 에러가 발생한 원본 라인
    /// </summary>
    public string Line { get; init; } = string.Empty;

    /// <summary>
    /// 에러 메시지
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// 예외 정보 (선택사항)
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 에러 심각도
    /// </summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;

    /// <summary>
    /// 에러 발생 섹션 ID (선택사항)
    /// </summary>
    public string? SectionId { get; init; }
}

/// <summary>
/// 에러 심각도
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// 경고 (파싱 계속 진행)
    /// </summary>
    Warning,

    /// <summary>
    /// 에러 (해당 라인 스킵)
    /// </summary>
    Error,

    /// <summary>
    /// 치명적 (파싱 중단 고려)
    /// </summary>
    Critical
}

