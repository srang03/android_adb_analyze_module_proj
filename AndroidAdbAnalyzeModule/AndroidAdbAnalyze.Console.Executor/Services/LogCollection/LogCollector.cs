using System.Diagnostics;
using AndroidAdbAnalyze.Console.Executor.Configuration;
using AndroidAdbAnalyze.Console.Executor.Core.Exceptions;
using AndroidAdbAnalyze.Console.Executor.Models;
using AndroidAdbAnalyze.Console.Executor.Services.Adb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AndroidAdbAnalyze.Console.Executor.Services.LogCollection;

/// <summary>
/// ADB 로그 수집 서비스 구현체
/// </summary>
public sealed class LogCollector : ILogCollector
{
    private readonly IAdbCommandExecutor _adbExecutor;
    private readonly LogCollectionConfiguration _config;
    private readonly ILogger<LogCollector> _logger;

    public LogCollector(
        IAdbCommandExecutor adbExecutor,
        IOptions<LogCollectionConfiguration> config,
        ILogger<LogCollector> logger)
    {
        _adbExecutor = adbExecutor ?? throw new ArgumentNullException(nameof(adbExecutor));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LogCollectionSummary> CollectAllLogsAsync(
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveOutputDir = outputDirectory ?? _config.OutputDirectory;
        
        _logger.LogInformation(
            "로그 수집 시작: {LogCount}개 로그 → {OutputDirectory}",
            _config.Logs.Count, effectiveOutputDir);
        
        // 출력 디렉토리 생성
        Directory.CreateDirectory(effectiveOutputDir);
        
        var stopwatch = Stopwatch.StartNew();
        var results = new List<LogCollectionResult>();
        var failedRequiredLogs = new List<string>();
        
        // 순차 실행 (사용자 요청)
        foreach (var logDef in _config.Logs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogInformation(
                "로그 수집 중: {LogName} ({Required})",
                logDef.Name, logDef.Required ? "필수" : "선택");
            
            var result = await CollectLogAsync(logDef, effectiveOutputDir, cancellationToken);
            results.Add(result);
            
            if (!result.Success)
            {
                if (logDef.Required)
                {
                    failedRequiredLogs.Add(logDef.Name);
                    _logger.LogError(
                        "필수 로그 수집 실패: {LogName} - {Error}",
                        logDef.Name, result.ErrorMessage);
                }
                else
                {
                    _logger.LogWarning(
                        "선택 로그 수집 실패 (계속 진행): {LogName} - {Error}",
                        logDef.Name, result.ErrorMessage);
                }
            }
            else
            {
                _logger.LogInformation(
                    "로그 수집 성공: {LogName} ({FileSize} bytes, {ExecutionTime:F2}초)",
                    logDef.Name, result.FileSizeBytes, result.ExecutionTime.TotalSeconds);
            }
        }
        
        stopwatch.Stop();
        
        var summary = new LogCollectionSummary
        {
            TotalLogs = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results.AsReadOnly(),
            TotalExecutionTime = stopwatch.Elapsed,
            OutputDirectory = effectiveOutputDir
        };
        
        _logger.LogInformation(
            "로그 수집 완료: {SuccessCount}/{TotalLogs} 성공 ({TotalTime:F2}초)",
            summary.SuccessCount, summary.TotalLogs, summary.TotalExecutionTime.TotalSeconds);
        
        // 필수 로그가 하나라도 실패하면 예외 발생
        if (failedRequiredLogs.Any())
        {
            var firstFailedLog = failedRequiredLogs.First();
            var failedResult = results.First(r => r.LogDefinition.Name == firstFailedLog);
            
            throw new LogCollectionException(
                firstFailedLog,
                isRequired: true,
                stdError: failedResult.ErrorMessage,
                innerException: failedResult.Exception);
        }
        
        return summary;
    }

    public async Task<LogCollectionResult> CollectLogAsync(
        LogDefinition logDefinition,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // dumpsys 명령 실행
            var timeout = logDefinition.Timeout.HasValue
                ? TimeSpan.FromSeconds(logDefinition.Timeout.Value)
                : (TimeSpan?)null;
            
            var dumpsysCommand = $"shell dumpsys {logDefinition.DumpsysService}";
            
            _logger.LogDebug(
                "ADB 명령 실행: {Command} (timeout: {Timeout}초)",
                dumpsysCommand, 
                timeout?.TotalSeconds.ToString() ?? "기본값");
            
            var result = await _adbExecutor.ExecuteAsync(
                dumpsysCommand,
                timeout: timeout,
                cancellationToken: cancellationToken);
            
            if (!result.Success)
            {
                stopwatch.Stop();
                
                return new LogCollectionResult
                {
                    LogDefinition = logDefinition,
                    Success = false,
                    ExecutionTime = stopwatch.Elapsed,
                    ErrorMessage = $"dumpsys 명령 실패 (ExitCode: {result.ExitCode}): {result.StandardError}",
                    Exception = result.Exception
                };
            }
            
            // 파일 저장
            var filePath = Path.Combine(outputDirectory, logDefinition.OutputFileName);
            await File.WriteAllTextAsync(filePath, result.StandardOutput, cancellationToken);
            
            var fileInfo = new FileInfo(filePath);
            
            stopwatch.Stop();
            
            _logger.LogDebug(
                "로그 파일 저장 완료: {FilePath} ({FileSize} bytes)",
                filePath, fileInfo.Length);
            
            return new LogCollectionResult
            {
                LogDefinition = logDefinition,
                Success = true,
                FilePath = filePath,
                FileSizeBytes = fileInfo.Length,
                ExecutionTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, "로그 수집 중 예외 발생: {LogName}", logDefinition.Name);
            
            return new LogCollectionResult
            {
                LogDefinition = logDefinition,
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
    }
}

