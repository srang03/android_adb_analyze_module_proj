namespace AndroidAdbAnalyzeModule.Core.Models;

/// <summary>
/// 파싱 통계 정보
/// </summary>
public sealed class ParsingStatistics
{
    /// <summary>
    /// 전체 라인 수
    /// </summary>
    public int TotalLines { get; init; }

    /// <summary>
    /// 성공적으로 파싱된 라인 수
    /// </summary>
    public int ParsedLines { get; init; }

    /// <summary>
    /// 스킵된 라인 수 (빈 라인, 주석 등)
    /// </summary>
    public int SkippedLines { get; init; }

    /// <summary>
    /// 에러 발생 라인 수
    /// </summary>
    public int ErrorLines { get; init; }

    /// <summary>
    /// 파싱 소요 시간
    /// </summary>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// 이벤트 타입별 개수
    /// </summary>
    public IReadOnlyDictionary<string, int> EventTypeCounts { get; init; } =
        new Dictionary<string, int>();

    /// <summary>
    /// 섹션별 파싱 라인 수
    /// </summary>
    public IReadOnlyDictionary<string, int> SectionLineCounts { get; init; } =
        new Dictionary<string, int>();

    /// <summary>
    /// 파싱 성공률 (0.0 ~ 1.0)
    /// </summary>
    public double SuccessRate => TotalLines > 0
        ? (double)ParsedLines / TotalLines
        : 0.0;
}

