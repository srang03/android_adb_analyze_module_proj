using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Visualization;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 타임라인 생성 서비스 인터페이스
/// </summary>
public interface ITimelineBuilder
{
    /// <summary>
    /// 분석 결과를 기반으로 타임라인 항목 목록을 생성합니다.
    /// </summary>
    /// <param name="result">분석 결과</param>
    /// <returns>타임라인 항목 목록 (시간순 정렬)</returns>
    IReadOnlyList<TimelineItem> BuildTimeline(AnalysisResult result);
}
