using AndroidAdbAnalyze.Console.Executor.Configuration;
using AndroidAdbAnalyze.Console.Executor.Models;

namespace AndroidAdbAnalyze.Console.Executor.Services.LogCollection;

/// <summary>
/// ADB 로그 수집 서비스 인터페이스
/// </summary>
public interface ILogCollector
{
    /// <summary>
    /// 설정 파일에 정의된 모든 로그 수집
    /// </summary>
    /// <param name="outputDirectory">출력 디렉토리 (null이면 설정 파일 값 사용)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>수집 요약</returns>
    /// <exception cref="Core.Exceptions.LogCollectionException">필수 로그 수집 실패 시</exception>
    Task<LogCollectionSummary> CollectAllLogsAsync(
        string? outputDirectory = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 개별 로그 수집
    /// </summary>
    /// <param name="logDefinition">로그 정의</param>
    /// <param name="outputDirectory">출력 디렉토리</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>수집 결과</returns>
    Task<LogCollectionResult> CollectLogAsync(
        LogDefinition logDefinition,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}

