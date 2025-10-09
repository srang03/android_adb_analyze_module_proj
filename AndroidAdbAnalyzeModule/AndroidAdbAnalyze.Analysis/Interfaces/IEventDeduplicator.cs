using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 중복 이벤트 제거 서비스 인터페이스
/// </summary>
public interface IEventDeduplicator
{
    /// <summary>
    /// 중복 이벤트를 제거하고 대표 이벤트 목록을 반환합니다.
    /// </summary>
    /// <param name="events">원본 이벤트 목록</param>
    /// <param name="deduplicationDetails">중복 제거 상세 정보 (출력)</param>
    /// <returns>중복 제거된 이벤트 목록</returns>
    IReadOnlyList<NormalizedLogEvent> Deduplicate(
        IReadOnlyList<NormalizedLogEvent> events,
        out IReadOnlyList<DeduplicationInfo> deduplicationDetails);
}
