namespace AndroidAdbAnalyze.Parser.Core.Models;

/// <summary>
/// 파싱된 로그 항목 (중간 결과)
/// </summary>
public sealed class ParsedLogEntry
{
    /// <summary>
    /// 이벤트 타입 (예: "PLAYER_CREATED", "PLAYER_EVENT", "PLAYER_RELEASED")
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// 타임스탬프 (파싱된 원본 타임스탬프, 아직 정규화 전)
    /// </summary>
    public DateTime? Timestamp { get; init; }

    /// <summary>
    /// <see cref="Timestamp"/>을 파싱할 때 사용한 포맷 (예: "MM-dd HH:mm:ss.fff").
    /// 연도가 없는 포맷이면 정규화 단계에서 연도를 추론합니다. 알 수 없으면 null입니다.
    /// </summary>
    public string? TimestampFormat { get; init; }

    /// <summary>
    /// 파싱된 필드 (예: piid, uid, pid, package 등)
    /// </summary>
    public IReadOnlyDictionary<string, object> Fields { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// 원본 로그 라인
    /// </summary>
    public string RawLine { get; init; } = string.Empty;

    /// <summary>
    /// 라인 번호
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// 소속 섹션 ID
    /// </summary>
    public string SectionId { get; init; } = string.Empty;
}

