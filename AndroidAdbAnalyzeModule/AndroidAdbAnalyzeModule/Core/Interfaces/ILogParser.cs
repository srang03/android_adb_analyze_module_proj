using AndroidAdbAnalyzeModule.Core.Models;

namespace AndroidAdbAnalyzeModule.Core.Interfaces;

/// <summary>
/// 로그 파서 인터페이스
/// </summary>
public interface ILogParser
{
    /// <summary>
    /// 로그 파일을 비동기로 파싱
    /// </summary>
    /// <param name="logFilePath">로그 파일 경로</param>
    /// <param name="options">파싱 옵션</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>파싱 결과</returns>
    Task<ParsingResult> ParseAsync(
        string logFilePath,
        LogParsingOptions options,
        CancellationToken cancellationToken = default);
}

