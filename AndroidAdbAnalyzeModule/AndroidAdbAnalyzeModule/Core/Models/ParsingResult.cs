namespace AndroidAdbAnalyzeModule.Core.Models;

/// <summary>
/// 로그 파싱 결과
/// </summary>
public sealed class ParsingResult
{
    /// <summary>
    /// 파싱 성공 여부
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 파싱된 이벤트 목록
    /// 하나의 로그 파일에서 여러 타입의 이벤트가 포함될 수 있음
    /// </summary>
    public IReadOnlyList<NormalizedLogEvent> Events { get; init; } =
        Array.Empty<NormalizedLogEvent>();

    /// <summary>
    /// 파싱 통계
    /// </summary>
    public ParsingStatistics Statistics { get; init; } = new();

    /// <summary>
    /// 발생한 에러 목록
    /// </summary>
    public IReadOnlyList<ParsingError> Errors { get; init; } =
        Array.Empty<ParsingError>();

    /// <summary>
    /// 에러 메시지 (파싱 실패 시)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 예외 정보 (파싱 실패 시)
    /// </summary>
    public Exception? Exception { get; init; }
}

