namespace AndroidAdbAnalyze.Console.Executor.Services.Adb;

/// <summary>
/// ADB 명령 실행 결과
/// </summary>
public sealed record AdbCommandResult
{
    /// <summary>
    /// 명령 실행 성공 여부
    /// </summary>
    public bool Success { get; init; }
    
    /// <summary>
    /// 프로세스 종료 코드
    /// </summary>
    public int ExitCode { get; init; }
    
    /// <summary>
    /// 표준 출력 (stdout)
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;
    
    /// <summary>
    /// 표준 에러 (stderr)
    /// </summary>
    public string StandardError { get; init; } = string.Empty;
    
    /// <summary>
    /// 실행 시간
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }
    
    /// <summary>
    /// 재시도 횟수 (첫 시도는 0)
    /// </summary>
    public int RetryCount { get; init; }
    
    /// <summary>
    /// 예외 정보 (있는 경우)
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 성공한 결과 생성
    /// </summary>
    public static AdbCommandResult CreateSuccess(
        string stdout, 
        string stderr, 
        TimeSpan executionTime, 
        int retryCount = 0)
    {
        return new AdbCommandResult
        {
            Success = true,
            ExitCode = 0,
            StandardOutput = stdout,
            StandardError = stderr,
            ExecutionTime = executionTime,
            RetryCount = retryCount
        };
    }

    /// <summary>
    /// 실패한 결과 생성
    /// </summary>
    public static AdbCommandResult CreateFailure(
        int exitCode, 
        string stdout, 
        string stderr, 
        TimeSpan executionTime,
        int retryCount = 0,
        Exception? exception = null)
    {
        return new AdbCommandResult
        {
            Success = false,
            ExitCode = exitCode,
            StandardOutput = stdout,
            StandardError = stderr,
            ExecutionTime = executionTime,
            RetryCount = retryCount,
            Exception = exception
        };
    }
}

