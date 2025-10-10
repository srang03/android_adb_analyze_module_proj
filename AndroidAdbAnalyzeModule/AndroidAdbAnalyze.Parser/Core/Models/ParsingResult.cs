namespace AndroidAdbAnalyze.Parser.Core.Models;

/// <summary>
/// 로그 파일 파싱 작업의 전체 결과를 캡슐화합니다.
/// 이 클래스는 불변(immutable)으로 설계되었습니다.
/// </summary>
public sealed class ParsingResult
{
    /// <summary>
    /// 파싱 작업이 성공적으로 완료되었는지 여부를 나타냅니다.
    /// 치명적인 오류가 발생하지 않고 하나 이상의 이벤트를 성공적으로 파싱한 경우 true입니다.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 로그 파일에서 성공적으로 파싱된 <see cref="NormalizedLogEvent"/>의 읽기 전용 목록입니다.
    /// </summary>
    public IReadOnlyList<NormalizedLogEvent> Events { get; init; } =
        Array.Empty<NormalizedLogEvent>();

    /// <summary>
    /// 파싱 작업 중 수집된 통계 정보입니다.
    /// <see cref="ParsingStatistics"/>를 참조하세요.
    /// </summary>
    public ParsingStatistics Statistics { get; init; } = new();

    /// <summary>
    /// 파싱 작업 중 발생한 개별 라인 오류의 읽기 전용 목록입니다.
    /// </summary>
    public IReadOnlyList<ParsingError> Errors { get; init; } =
        Array.Empty<ParsingError>();

    /// <summary>
    /// 파싱 작업이 실패한 경우(<see cref="Success"/>가 false)의 주된 이유를 설명하는 메시지입니다.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 파싱 작업 중 발생한 치명적인 예외입니다. (선택 사항)
    /// 파일 읽기 실패 또는 설정 오류와 같은 복구 불가능한 오류가 발생했을 때 설정됩니다.
    /// </summary>
    public Exception? Exception { get; init; }
}

