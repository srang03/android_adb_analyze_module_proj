using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Parser.Core.Interfaces;

/// <summary>
/// 라인 파서 인터페이스
/// </summary>
public interface ILineParser
{
    /// <summary>
    /// 해당 라인을 파싱할 수 있는지 확인
    /// </summary>
    bool CanParse(string line, ParsingContext context);

    /// <summary>
    /// 라인 파싱
    /// </summary>
    ParsedLogEntry? Parse(string line, ParsingContext context);
}

/// <summary>
/// 파싱 컨텍스트
/// </summary>
public sealed class ParsingContext
{
    /// <summary>
    /// 현재 섹션 ID
    /// </summary>
    public string SectionId { get; init; } = string.Empty;

    /// <summary>
    /// 현재 라인 번호
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// 마지막 파싱된 타임스탬프
    /// </summary>
    public DateTime? LastTimestamp { get; init; }

    /// <summary>
    /// 공유 상태 (섹션 내 상태 유지용)
    /// 읽기 전용 뷰를 제공하며, 내부 상태는 불변
    /// </summary>
    public IReadOnlyDictionary<string, object> SharedState { get; init; } =
        new Dictionary<string, object>();
}

