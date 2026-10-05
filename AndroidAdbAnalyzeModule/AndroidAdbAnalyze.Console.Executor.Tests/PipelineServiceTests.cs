using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Console.Executor.Configuration;
using AndroidAdbAnalyze.Console.Executor.Models;
using AndroidAdbAnalyze.Console.Executor.Services.Adb;
using AndroidAdbAnalyze.Console.Executor.Services.Device;
using AndroidAdbAnalyze.Console.Executor.Services.LogCollection;
using AndroidAdbAnalyze.Console.Executor.Services.Pipeline;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AndroidAdbAnalyze.Console.Executor.Tests;

/// <summary>
/// 현재 작업 디렉토리를 변경하는 테스트는 병렬 실행하지 않음
/// </summary>
[CollectionDefinition(nameof(CurrentDirectoryCollection), DisableParallelization = true)]
public class CurrentDirectoryCollection
{
}

/// <summary>
/// PipelineService 파싱/결과 전달 테스트 (ADB 없이 수집 결과를 대체)
/// </summary>
[Collection(nameof(CurrentDirectoryCollection))]
public class PipelineServiceTests
{
    private const string VibratorParserConfig = "Configs/Parser/adb_vibrator_config.yaml";

    private static readonly string VibratorLogPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "vibrator_manager.txt");

    [Fact]
    public async Task ExecuteAsync_TimeRangeWithoutTimeZone_ShouldBeInterpretedAsDeviceLocalTime()
    {
        // Arrange: 디바이스(Asia/Seoul) 현지 시각 2025-09-07 18:00 ~ 19:00 에는 vibrator 이벤트 15개가 있음
        var orchestrator = new CapturingOrchestrator();
        var pipeline = CreatePipeline(orchestrator, new DateTime(2025, 9, 9, 16, 0, 0));

        // Act
        var result = await pipeline.ExecuteAsync(
            startTime: new DateTime(2025, 9, 7, 18, 0, 0),
            endTime: new DateTime(2025, 9, 7, 19, 0, 0));

        // Assert: 이벤트 타임스탬프는 UTC (KST - 9시간)
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(15, orchestrator.Events.Count);
        Assert.All(orchestrator.Events, e =>
            Assert.InRange(e.Timestamp, new DateTime(2025, 9, 7, 9, 0, 0), new DateTime(2025, 9, 7, 10, 0, 0)));
    }

    [Fact]
    public async Task ExecuteAsync_EventsLoggedDuringCollection_ShouldKeepDeviceYear()
    {
        // Arrange: 디바이스 시각 조회(18:31:00.000) 이후 수집 중 기록된 이벤트(18:31:00.005)가 있는 경우
        var orchestrator = new CapturingOrchestrator();
        var pipeline = CreatePipeline(
            orchestrator,
            new DateTime(2025, 9, 7, 18, 31, 0),
            collectionDelay: TimeSpan.FromMilliseconds(200));

        // Act
        var result = await pipeline.ExecuteAsync();

        // Assert: 수집 중 기록된 이벤트가 전년도(2024)로 추론되면 안 됨
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotEmpty(orchestrator.Events);
        Assert.All(orchestrator.Events, e => Assert.Equal(2025, e.Timestamp.Year));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveRebootEvents_InAnalysisResult()
    {
        // Arrange
        var rebootEvent = new NormalizedLogEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = new DateTime(2025, 9, 7, 9, 0, 0),
            EventType = "DEVICE_BOOT_COMPLETED",
            SourceSection = "test"
        };
        var orchestrator = new CapturingOrchestrator(new[] { rebootEvent });
        var pipeline = CreatePipeline(orchestrator, new DateTime(2025, 9, 9, 16, 0, 0));

        // Act
        var result = await pipeline.ExecuteAsync();

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Single(result.AnalysisResult!.RebootEvents);
        Assert.Equal(1, result.AnalysisResult.Statistics.TotalRebootEvents);
    }

    [Fact]
    public async Task ExecuteAsync_FromOtherWorkingDirectory_ShouldResolveParserConfigFromBaseDirectory()
    {
        // Arrange: 실행 파일 위치가 아닌 다른 디렉토리에서 실행
        var orchestrator = new CapturingOrchestrator();
        var pipeline = CreatePipeline(orchestrator, new DateTime(2025, 9, 9, 16, 0, 0));
        var originalDirectory = Directory.GetCurrentDirectory();
        var otherDirectory = Directory.CreateTempSubdirectory("adbanalyze-cwd-");

        try
        {
            Directory.SetCurrentDirectory(otherDirectory.FullName);

            // Act
            var result = await pipeline.ExecuteAsync();

            // Assert
            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.ParsingResults["vibrator_manager"].Success,
                result.ParsingResults["vibrator_manager"].ErrorMessage);
            Assert.NotEmpty(orchestrator.Events);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            otherDirectory.Delete(recursive: true);
        }
    }

    private static PipelineService CreatePipeline(
        CapturingOrchestrator orchestrator,
        DateTime deviceCurrentTime,
        TimeSpan? collectionDelay = null)
    {
        return new PipelineService(
            new FakeDeviceManager(deviceCurrentTime),
            new FakeLogCollector(collectionDelay ?? TimeSpan.Zero),
            new FakeAdbCommandExecutor(),
            orchestrator,
            new AnalysisOptions(),
            NullLogger<PipelineService>.Instance,
            NullLoggerFactory.Instance);
    }

    private sealed class FakeDeviceManager : IDeviceManager
    {
        private readonly DateTime _currentTime;

        public FakeDeviceManager(DateTime currentTime) => _currentTime = currentTime;

        public Task<IReadOnlyList<AdbDevice>> GetConnectedDevicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdbDevice>>(new[] { Device });

        public Task<AdbDevice> EnsureSingleDeviceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Device);

        public Task<DeviceInfo> ExtractDeviceInfoAsync(AdbDevice device, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DeviceInfo { TimeZone = "Asia/Seoul", CurrentTime = _currentTime, AndroidVersion = "15" });

        private static AdbDevice Device => new() { Serial = "TEST", State = "device" };
    }

    private sealed class FakeLogCollector : ILogCollector
    {
        private readonly TimeSpan _delay;

        public FakeLogCollector(TimeSpan delay) => _delay = delay;

        public async Task<LogCollectionSummary> CollectAllLogsAsync(
            string? outputDirectory = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delay, cancellationToken);
            var result = new LogCollectionResult
            {
                LogDefinition = new LogDefinition
                {
                    Name = "vibrator_manager",
                    DumpsysService = "vibrator_manager",
                    OutputFileName = "vibrator_manager.log",
                    ParserConfig = VibratorParserConfig
                },
                Success = true,
                FilePath = VibratorLogPath
            };

            return new LogCollectionSummary
            {
                TotalLogs = 1,
                SuccessCount = 1,
                Results = new[] { result },
                OutputDirectory = Path.GetDirectoryName(VibratorLogPath)!
            };
        }

        public Task<LogCollectionResult> CollectLogAsync(
            LogDefinition logDefinition,
            string outputDirectory,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAdbCommandExecutor : IAdbCommandExecutor
    {
        public string AdbPath => "adb";

        public string? TargetDeviceSerial { get; set; }

        public Task<AdbCommandResult> ExecuteAsync(string arguments, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AdbCommandResult> ExecuteWithRetryAsync(string arguments, int? retryCount = null, TimeSpan? retryDelay = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> IsAdbAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class CapturingOrchestrator : IAnalysisOrchestrator
    {
        private readonly IReadOnlyList<NormalizedLogEvent> _rebootEvents;

        public CapturingOrchestrator(IReadOnlyList<NormalizedLogEvent>? rebootEvents = null)
        {
            _rebootEvents = rebootEvents ?? Array.Empty<NormalizedLogEvent>();
        }

        public IReadOnlyList<NormalizedLogEvent> Events { get; private set; } = Array.Empty<NormalizedLogEvent>();

        public Task<AnalysisResult> AnalyzeAsync(
            IReadOnlyList<NormalizedLogEvent> events,
            AnalysisOptions? options = null,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Events = events;
            return Task.FromResult(new AnalysisResult
            {
                Success = true,
                Sessions = Array.Empty<CameraSession>(),
                CaptureEvents = Array.Empty<CameraCaptureEvent>(),
                RebootEvents = _rebootEvents,
                DeduplicationDetails = Array.Empty<DeduplicationInfo>(),
                SourceEvents = events,
                Statistics = new AnalysisStatistics { TotalRebootEvents = _rebootEvents.Count },
                Errors = Array.Empty<string>(),
                Warnings = Array.Empty<string>()
            });
        }
    }
}
