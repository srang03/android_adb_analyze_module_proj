using AndroidAdbAnalyze.Console.Executor.Models;
using AndroidAdbAnalyze.Console.Executor.Services.Output.Models;

namespace AndroidAdbAnalyze.Console.Executor.Services.Output;

/// <summary>
/// 분석 결과 저장 서비스 인터페이스
/// </summary>
/// <remarks>
/// 이 서비스는 완전히 캡슐화되어 있으며, 외부 비즈니스 로직에 영향을 주지 않습니다.
/// - PipelineResult를 받아 JSON 및 HTML 보고서로 저장
/// - 날짜/시간 기반 폴더 자동 생성
/// - 원본 로그를 raw_logs 하위 폴더로 이동
/// - IReportGenerator를 활용한 HTML 보고서 생성 (이미 구현됨)
/// </remarks>
public interface IResultOutputService
{
    /// <summary>
    /// 분석 결과를 파일로 저장합니다
    /// </summary>
    /// <param name="result">파이프라인 실행 결과</param>
    /// <param name="baseOutputDirectory">기본 출력 디렉토리 (null이면 ./logs)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>저장 결과 요약</returns>
    Task<OutputSummary> SaveResultsAsync(
        PipelineResult result,
        string? baseOutputDirectory = null,
        CancellationToken cancellationToken = default);
}

