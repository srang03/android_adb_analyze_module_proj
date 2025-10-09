using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 전체 분석 파이프라인 오케스트레이션 서비스 인터페이스
/// </summary>
public interface IAnalysisOrchestrator
{
    /// <summary>
    /// 로그 이벤트를 분석하여 결과를 반환합니다.
    /// </summary>
    /// <param name="events">파싱된 로그 이벤트 목록</param>
    /// <param name="options">분석 옵션</param>
    /// <param name="progress">진행 상태 보고용 (선택적)</param>
    /// <param name="cancellationToken">취소 토큰 (선택적)</param>
    /// <returns>분석 결과</returns>
    Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions? options = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
