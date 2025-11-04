using AndroidAdbAnalyze.Console.Executor.Models;

namespace AndroidAdbAnalyze.Console.Executor.Services.Pipeline;

/// <summary>
/// 전체 파이프라인 서비스 인터페이스
/// (디바이스 연결 확인 → 로그 수집 → 파싱 → 분석)
/// </summary>
public interface IPipelineService
{
    /// <summary>
    /// 전체 파이프라인 실행
    /// </summary>
    /// <param name="outputDirectory">출력 디렉토리 (null이면 설정 파일 값 사용)</param>
    /// <param name="startTime">분석 시작 시간 필터 (null이면 전체 로그)</param>
    /// <param name="endTime">분석 종료 시간 필터 (null이면 전체 로그)</param>
    /// <param name="progress">진행 상황 보고용 (선택적)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>파이프라인 실행 결과</returns>
    Task<PipelineResult> ExecuteAsync(
        string? outputDirectory = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

