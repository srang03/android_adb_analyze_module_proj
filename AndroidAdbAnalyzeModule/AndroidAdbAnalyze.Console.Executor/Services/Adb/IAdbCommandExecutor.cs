namespace AndroidAdbAnalyze.Console.Executor.Services.Adb;

/// <summary>
/// ADB 명령 실행 인터페이스
/// </summary>
public interface IAdbCommandExecutor
{
    /// <summary>
    /// ADB 실행 파일 경로
    /// </summary>
    string AdbPath { get; }
    
    /// <summary>
    /// ADB 명령을 비동기로 실행합니다
    /// </summary>
    /// <param name="arguments">ADB 명령 인자 (예: "devices", "shell dumpsys activity")</param>
    /// <param name="timeout">타임아웃 (null이면 기본값 사용)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>명령 실행 결과</returns>
    Task<AdbCommandResult> ExecuteAsync(
        string arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ADB 명령을 비동기로 실행하고 재시도합니다
    /// </summary>
    /// <param name="arguments">ADB 명령 인자</param>
    /// <param name="retryCount">재시도 횟수 (null이면 설정값 사용)</param>
    /// <param name="retryDelay">재시도 간격 (null이면 설정값 사용)</param>
    /// <param name="timeout">타임아웃</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>명령 실행 결과</returns>
    Task<AdbCommandResult> ExecuteWithRetryAsync(
        string arguments,
        int? retryCount = null,
        TimeSpan? retryDelay = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// ADB가 설치되어 있고 접근 가능한지 확인합니다
    /// </summary>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>ADB 사용 가능 여부</returns>
    Task<bool> IsAdbAvailableAsync(CancellationToken cancellationToken = default);
}

