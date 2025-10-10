using AndroidAdbAnalyze.Parser.Core.Interfaces;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Parser.Parsing.Interfaces;

/// <summary>
/// 복잡한 패턴(2줄 이상)을 파싱하는 인터페이스
/// 반복되는 패턴 발견 시 플러그인 시스템으로 전환 예정
/// </summary>
public interface IMultilinePatternParser
{
    /// <summary>
    /// 파서 고유 ID
    /// </summary>
    string ParserId { get; }

    /// <summary>
    /// 대상 섹션 ID
    /// </summary>
    string TargetSectionId { get; }

    /// <summary>
    /// 우선순위 (낮을수록 먼저 시도)
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// 현재 위치에서 multiline 패턴을 파싱할 수 있는지 확인
    /// </summary>
    /// <param name="section">로그 섹션</param>
    /// <param name="currentIndex">현재 라인 인덱스</param>
    /// <returns>파싱 가능 여부</returns>
    bool CanParse(LogSection section, int currentIndex);

    /// <summary>
    /// Multiline 패턴 파싱 시도
    /// </summary>
    /// <param name="section">로그 섹션</param>
    /// <param name="currentIndex">현재 라인 인덱스</param>
    /// <param name="entry">파싱 결과 (성공 시)</param>
    /// <param name="linesToSkip">파싱 후 스킵할 라인 수 (성공 시)</param>
    /// <returns>파싱 성공 여부</returns>
    bool TryParse(
        LogSection section,
        int currentIndex,
        out ParsedLogEntry? entry,
        out int linesToSkip);
}

