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

