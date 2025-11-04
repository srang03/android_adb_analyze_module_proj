using System.CommandLine;
using System.CommandLine.Invocation;
using AndroidAdbAnalyze.Console.Executor.Core;
using AndroidAdbAnalyze.Console.Executor.Core.Exceptions;
using AndroidAdbAnalyze.Console.Executor.Services.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Console.Executor.Commands;

/// <summary>
/// analyze 명령: ADB 로그 수집 및 분석
/// </summary>
public static class AnalyzeCommand
{
    /// <summary>
    /// analyze 명령 생성
    /// </summary>
    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new Command("analyze", "ADB 로그를 수집하고 분석합니다.\n\n" +
            "이 명령은 연결된 Android 디바이스에서 dumpsys 로그를 수집하고,\n" +
            "Parser 및 Analysis 모듈을 사용하여 카메라 세션과 촬영 이벤트를 탐지합니다.");
        
        // ===== Options =====
        
        var outputDirOption = new Option<string?>(
            aliases: new[] { "--output-dir", "-o" },
            description: "로그 출력 디렉토리 (기본값: ./logs)",
            getDefaultValue: () => null);
        
        var startTimeOption = new Option<DateTime?>(
            aliases: new[] { "--start-time", "-s" },
            description: "분석 시작 시간 (ISO 8601 형식, 예: 2025-10-18T10:00:00)",
            getDefaultValue: () => null);
        
        var endTimeOption = new Option<DateTime?>(
            aliases: new[] { "--end-time", "-e" },
            description: "분석 종료 시간 (ISO 8601 형식, 예: 2025-10-18T12:00:00)",
            getDefaultValue: () => null);
        
        var noHtmlOption = new Option<bool>(
            aliases: new[] { "--no-html-report" },
            description: "HTML 보고서 생성 안 함",
            getDefaultValue: () => false);
        
        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "상세 로그 출력",
            getDefaultValue: () => false);
        
        var quietOption = new Option<bool>(
            aliases: new[] { "--quiet", "-q" },
            description: "최소 로그 출력",
            getDefaultValue: () => false);
        
        command.AddOption(outputDirOption);
        command.AddOption(startTimeOption);
        command.AddOption(endTimeOption);
        command.AddOption(noHtmlOption);
        command.AddOption(verboseOption);
        command.AddOption(quietOption);
        
        // ===== Handler =====
        
        command.SetHandler(async (context) =>
        {
            var outputDir = context.ParseResult.GetValueForOption(outputDirOption);
            var startTime = context.ParseResult.GetValueForOption(startTimeOption);
            var endTime = context.ParseResult.GetValueForOption(endTimeOption);
            var noHtml = context.ParseResult.GetValueForOption(noHtmlOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var quiet = context.ParseResult.GetValueForOption(quietOption);
            
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            
            try
            {
                logger.LogInformation("=== analyze 명령 시작 ===");
                logger.LogInformation("출력 디렉토리: {OutputDir}", outputDir ?? "(기본값)");
                
                if (startTime.HasValue)
                    logger.LogInformation("시작 시간: {StartTime}", startTime.Value);
                if (endTime.HasValue)
                    logger.LogInformation("종료 시간: {EndTime}", endTime.Value);
                
                // PipelineService 실행
                using var scope = serviceProvider.CreateScope();
                var pipelineService = scope.ServiceProvider.GetRequiredService<IPipelineService>();
                
                var progress = new Progress<string>(message =>
                {
                    if (!quiet)
                        System.Console.WriteLine($"[진행] {message}");
                });
                
                var result = await pipelineService.ExecuteAsync(
                    outputDirectory: outputDir,
                    startTime: startTime,
                    endTime: endTime,
                    progress: progress,
                    cancellationToken: context.GetCancellationToken());
                
                if (result.Success)
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine("=== 분석 완료 ===");
                    System.Console.WriteLine($"세션: {result.AnalysisResult?.Sessions.Count ?? 0}개");
                    System.Console.WriteLine($"촬영 이벤트: {result.AnalysisResult?.CaptureEvents.Count ?? 0}개");
                    System.Console.WriteLine($"실행 시간: {result.TotalExecutionTime.TotalSeconds:F2}초");
                    System.Console.WriteLine($"출력 디렉토리: {result.CollectionSummary?.OutputDirectory ?? outputDir ?? "./logs"}");
                    
                    context.ExitCode = (int)ExitCode.Success;
                }
                else
                {
                    System.Console.Error.WriteLine();
                    System.Console.Error.WriteLine($"=== 분석 실패 ===");
                    System.Console.Error.WriteLine($"오류: {result.ErrorMessage}");
                    
                    context.ExitCode = (int)ExitCode.AnalysisFailed;
                }
            }
            catch (AndroidAdbAnalyzeException ex)
            {
                logger.LogError(ex, "분석 중 예외 발생");
                
                System.Console.Error.WriteLine();
                System.Console.Error.WriteLine($"오류: {ex.Message}");
                
                if (!string.IsNullOrEmpty(ex.UserFriendlyHelp))
                {
                    System.Console.Error.WriteLine(ex.UserFriendlyHelp);
                }
                
                context.ExitCode = (int)ex.ExitCode;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("사용자가 작업을 취소했습니다");
                System.Console.WriteLine("\n작업이 취소되었습니다.");
                context.ExitCode = (int)ExitCode.Success;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "예기치 않은 오류 발생");
                System.Console.Error.WriteLine($"\n예기치 않은 오류: {ex.Message}");
                context.ExitCode = (int)ExitCode.UnknownError;
            }
        });
        
        return command;
    }
}

