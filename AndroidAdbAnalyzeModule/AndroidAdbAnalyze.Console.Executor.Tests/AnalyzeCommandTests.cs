using System.CommandLine;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Console.Executor.Commands;
using AndroidAdbAnalyze.Console.Executor.Core;
using AndroidAdbAnalyze.Console.Executor.Core.Exceptions;
using AndroidAdbAnalyze.Console.Executor.Models;
using AndroidAdbAnalyze.Console.Executor.Services.Output;
using AndroidAdbAnalyze.Console.Executor.Services.Output.Models;
using AndroidAdbAnalyze.Console.Executor.Services.Pipeline;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace AndroidAdbAnalyze.Console.Executor.Tests;

/// <summary>
/// analyze 명령의 종료 코드 및 옵션 처리 테스트
/// </summary>
public class AnalyzeCommandTests
{
    [Fact]
    public async Task Analyze_WhenNoDeviceConnected_ShouldReturnNoDeviceConnectedExitCode()
    {
        // Arrange: PipelineService는 도메인 예외를 PipelineResult.Exception으로 반환
        var pipeline = new FakePipelineService(new PipelineResult
        {
            Success = false,
            ErrorMessage = "연결된 ADB 디바이스가 없습니다.",
            Exception = new DeviceNotConnectedException()
        });
        var command = CreateCommand(pipeline, new FakeResultOutputService());

        // Act
        var exitCode = await command.InvokeAsync("analyze");

        // Assert
        Assert.Equal((int)ExitCode.NoDeviceConnected, exitCode);
    }

    [Fact]
    public async Task Analyze_WhenMultipleDevicesConnected_ShouldReturnMultipleDevicesExitCode()
    {
        // Arrange
        var pipeline = new FakePipelineService(new PipelineResult
        {
            Success = false,
            ErrorMessage = "2개의 디바이스가 연결되어 있습니다.",
            Exception = new MultipleDevicesException(new[] { "A", "B" })
        });
        var command = CreateCommand(pipeline, new FakeResultOutputService());

        // Act
        var exitCode = await command.InvokeAsync("analyze");

        // Assert
        Assert.Equal((int)ExitCode.MultipleDevicesConnected, exitCode);
    }

    [Fact]
    public async Task Analyze_WhenPipelineFailsWithUnknownError_ShouldReturnAnalysisFailedExitCode()
    {
        // Arrange
        var pipeline = new FakePipelineService(new PipelineResult
        {
            Success = false,
            ErrorMessage = "unexpected",
            Exception = new InvalidOperationException("unexpected")
        });
        var command = CreateCommand(pipeline, new FakeResultOutputService());

        // Act
        var exitCode = await command.InvokeAsync("analyze");

        // Assert
        Assert.Equal((int)ExitCode.AnalysisFailed, exitCode);
    }

    [Fact]
    public async Task Analyze_WhenCancelled_ShouldReturnCancelledExitCode()
    {
        // Arrange
        var pipeline = new FakePipelineService(new OperationCanceledException());
        var command = CreateCommand(pipeline, new FakeResultOutputService());

        // Act
        var exitCode = await command.InvokeAsync("analyze");

        // Assert
        Assert.Equal((int)ExitCode.Cancelled, exitCode);
    }

    [Theory]
    [InlineData("analyze --no-html-report", false)]
    [InlineData("analyze", null)]
    public async Task Analyze_NoHtmlReportOption_ShouldBePassedToOutputService(string commandLine, bool? expected)
    {
        // Arrange
        var output = new FakeResultOutputService();
        var command = CreateCommand(new FakePipelineService(SuccessResult()), output);

        // Act
        var exitCode = await command.InvokeAsync(commandLine);

        // Assert
        Assert.Equal((int)ExitCode.Success, exitCode);
        Assert.True(output.Called);
        Assert.Equal(expected, output.GenerateHtmlReport);
    }

    [Theory]
    [InlineData("analyze --verbose", LogEventLevel.Debug)]
    [InlineData("analyze", LogEventLevel.Information)]
    public async Task Analyze_VerboseOption_ShouldLowerMinimumLogLevel(string commandLine, LogEventLevel expected)
    {
        // Arrange
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var command = CreateCommand(new FakePipelineService(SuccessResult()), new FakeResultOutputService(), levelSwitch);

        // Act
        await command.InvokeAsync(commandLine);

        // Assert
        Assert.Equal(expected, levelSwitch.MinimumLevel);
    }

    private static RootCommand CreateCommand(
        IPipelineService pipeline,
        IResultOutputService output,
        LoggingLevelSwitch? levelSwitch = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSingleton(pipeline);
        services.AddSingleton(output);
        services.AddSingleton(levelSwitch ?? new LoggingLevelSwitch());

        var rootCommand = new RootCommand();
        rootCommand.AddCommand(AnalyzeCommand.Create(services.BuildServiceProvider()));
        return rootCommand;
    }

    private static PipelineResult SuccessResult() => new()
    {
        Success = true,
        DeviceInfo = new DeviceInfo(),
        AnalysisResult = new AnalysisResult
        {
            Success = true,
            Sessions = Array.Empty<CameraSession>(),
            CaptureEvents = Array.Empty<CameraCaptureEvent>(),
            DeduplicationDetails = Array.Empty<DeduplicationInfo>(),
            SourceEvents = Array.Empty<NormalizedLogEvent>(),
            Statistics = new AnalysisStatistics(),
            Errors = Array.Empty<string>(),
            Warnings = Array.Empty<string>()
        }
    };

    private sealed class FakePipelineService : IPipelineService
    {
        private readonly PipelineResult? _result;
        private readonly Exception? _exception;

        public FakePipelineService(PipelineResult result) => _result = result;

        public FakePipelineService(Exception exception) => _exception = exception;

        public Task<PipelineResult> ExecuteAsync(
            string? outputDirectory = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return _exception != null
                ? Task.FromException<PipelineResult>(_exception)
                : Task.FromResult(_result!);
        }
    }

    private sealed class FakeResultOutputService : IResultOutputService
    {
        public bool Called { get; private set; }

        public bool? GenerateHtmlReport { get; private set; }

        public Task<OutputSummary> SaveResultsAsync(
            PipelineResult result,
            string? baseOutputDirectory = null,
            bool? generateHtmlReport = null,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            GenerateHtmlReport = generateHtmlReport;
            return Task.FromResult(new OutputSummary { OutputDirectory = "out", Success = true });
        }
    }
}
