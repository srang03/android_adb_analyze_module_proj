using System.Diagnostics;
using AndroidAdbAnalyze.Console.Executor.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Console.Executor.Services.Adb;

/// <summary>
/// ADB 명령 실행 구현체
/// </summary>
public sealed class AdbCommandExecutor : IAdbCommandExecutor
{
    private readonly ILogger<AdbCommandExecutor> _logger;
    private readonly TimeSpan _defaultTimeout;
    private readonly int _defaultRetryCount;
    private readonly TimeSpan _defaultRetryDelay;

    public string AdbPath { get; }

    /// <summary>
    /// AdbCommandExecutor 생성자
    /// </summary>
    /// <param name="adbPath">ADB 실행 파일 경로 (null이면 PATH에서 찾기)</param>
    /// <param name="defaultTimeout">기본 타임아웃</param>
    /// <param name="defaultRetryCount">기본 재시도 횟수</param>
    /// <param name="defaultRetryDelay">기본 재시도 간격</param>
    /// <param name="logger">로거</param>
    public AdbCommandExecutor(
        string? adbPath,
        TimeSpan defaultTimeout,
        int defaultRetryCount,
        TimeSpan defaultRetryDelay,
        ILogger<AdbCommandExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultTimeout = defaultTimeout;
        _defaultRetryCount = defaultRetryCount;
        _defaultRetryDelay = defaultRetryDelay;

        // ADB 경로 결정
        if (!string.IsNullOrWhiteSpace(adbPath))
        {
            if (!File.Exists(adbPath))
            {
                throw new AdbNotFoundException(adbPath);
            }
            AdbPath = adbPath;
        }
        else
        {
            var foundPath = FindAdbInPath();
            if (foundPath == null)
            {
                throw new AdbNotFoundException();
            }
            AdbPath = foundPath;
        }

        _logger.LogInformation("ADB 경로: {AdbPath}", AdbPath);
    }

    public async Task<AdbCommandResult> ExecuteAsync(
        string arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            throw new ArgumentException("ADB 명령 인자가 비어있습니다.", nameof(arguments));
        }

        var effectiveTimeout = timeout ?? _defaultTimeout;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("ADB 명령 실행: adb {Arguments} (타임아웃: {Timeout}초)", 
            arguments, effectiveTimeout.TotalSeconds);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = AdbPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            
            // 출력 버퍼 (메모리 버퍼 방식)
            var stdoutBuilder = new System.Text.StringBuilder();
            var stderrBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stdoutBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    stderrBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // 타임아웃과 취소 토큰 처리
            using var timeoutCts = new CancellationTokenSource(effectiveTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // 프로세스 강제 종료
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }

                stopwatch.Stop();

                if (timeoutCts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("ADB 명령 타임아웃: {Arguments} ({Elapsed}초)", 
                        arguments, stopwatch.Elapsed.TotalSeconds);
                    
                    return AdbCommandResult.CreateFailure(
                        exitCode: -1,
                        stdout: stdoutBuilder.ToString(),
                        stderr: $"타임아웃 ({effectiveTimeout.TotalSeconds}초 초과)",
                        executionTime: stopwatch.Elapsed,
                        exception: new TimeoutException($"ADB 명령 타임아웃: {arguments}"));
                }
                else
                {
                    throw;
                }
            }

            stopwatch.Stop();

            var stdout = stdoutBuilder.ToString();
            var stderr = stderrBuilder.ToString();
            var exitCode = process.ExitCode;

            if (exitCode == 0)
            {
                _logger.LogDebug("ADB 명령 성공: {Arguments} ({Elapsed}초)", 
                    arguments, stopwatch.Elapsed.TotalSeconds);
                
                return AdbCommandResult.CreateSuccess(
                    stdout: stdout,
                    stderr: stderr,
                    executionTime: stopwatch.Elapsed);
            }
            else
            {
                _logger.LogWarning("ADB 명령 실패: {Arguments} (ExitCode: {ExitCode}, Stderr: {Stderr})", 
                    arguments, exitCode, stderr.Length > 100 ? stderr.Substring(0, 100) + "..." : stderr);
                
                return AdbCommandResult.CreateFailure(
                    exitCode: exitCode,
                    stdout: stdout,
                    stderr: stderr,
                    executionTime: stopwatch.Elapsed);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "ADB 명령 실행 중 예외 발생: {Arguments}", arguments);
            
            return AdbCommandResult.CreateFailure(
                exitCode: -1,
                stdout: string.Empty,
                stderr: ex.Message,
                executionTime: stopwatch.Elapsed,
                exception: ex);
        }
    }

    public async Task<AdbCommandResult> ExecuteWithRetryAsync(
        string arguments,
        int? retryCount = null,
        TimeSpan? retryDelay = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveRetryCount = retryCount ?? _defaultRetryCount;
        var effectiveRetryDelay = retryDelay ?? _defaultRetryDelay;

        AdbCommandResult? lastResult = null;

        for (int attempt = 0; attempt <= effectiveRetryCount; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogInformation("ADB 명령 재시도 {Attempt}/{Total}: {Arguments}", 
                    attempt, effectiveRetryCount, arguments);
                
                await Task.Delay(effectiveRetryDelay, cancellationToken);
            }

            lastResult = await ExecuteAsync(arguments, timeout, cancellationToken);

            if (lastResult.Success)
            {
                // 재시도 횟수 기록
                return lastResult with { RetryCount = attempt };
            }

            // 취소된 경우 즉시 반환
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        // 모든 재시도 실패
        _logger.LogError("ADB 명령 실패 (재시도 {RetryCount}회): {Arguments}", 
            effectiveRetryCount, arguments);

        return lastResult! with { RetryCount = effectiveRetryCount };
    }

    public async Task<bool> IsAdbAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteAsync("version", TimeSpan.FromSeconds(5), cancellationToken);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// PATH 환경 변수에서 ADB 실행 파일을 찾습니다
    /// </summary>
    /// <returns>ADB 경로 (찾지 못하면 null)</returns>
    private static string? FindAdbInPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        var pathSeparator = OperatingSystem.IsWindows() ? ';' : ':';
        var adbFileName = OperatingSystem.IsWindows() ? "adb.exe" : "adb";

        var paths = pathEnv.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var path in paths)
        {
            try
            {
                var adbPath = Path.Combine(path.Trim(), adbFileName);
                if (File.Exists(adbPath))
                {
                    return adbPath;
                }
            }
            catch
            {
                // 잘못된 경로 무시
                continue;
            }
        }

        return null;
    }
}

