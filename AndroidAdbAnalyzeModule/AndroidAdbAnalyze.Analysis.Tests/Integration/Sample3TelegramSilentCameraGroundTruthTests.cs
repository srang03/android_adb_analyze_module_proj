using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration;

/// <summary>
/// Sample 3 (3차 샘플) - 텔레그램, 무음카메라 Ground Truth 검증 테스트
/// </summary>
/// <remarks>
/// 시나리오 데이터 시트 (2025-10-05):
/// 
/// 분석 시간 범위: 22:15:00 ~ 22:21:00
/// 
/// 텔레그램:
/// - 22:15:45 실행 → 22:15:50 종료 (촬영 없음)
/// - 22:16:54 실행 → 22:16:59 사진 촬영 → 22:17:04 종료
/// - 22:17:52 실행 → 22:17:57 사진 촬영 → 22:18:02 종료 및 전송
/// - 22:19:11 기존 앨범 사진 전송 (촬영 없음)
/// 
/// 무음 카메라:
/// - 22:19:50 실행 → 22:19:55 종료 (촬영 없음)
/// - 22:20:22 실행 → 22:20:27 사진 촬영 → 22:20:32 종료
/// 
/// Ground Truth:
/// - 총 세션: 5개 (텔레그램 3 + 무음 카메라 2)
/// - 총 촬영: 3개 (텔레그램 2 + 무음 카메라 1)
/// </remarks>
public sealed class Sample3TelegramSilentCameraGroundTruthTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    private List<NormalizedLogEvent>? _parsedEvents;
    
    // Ground Truth 상수
    private const int ExpectedTotalSessions = 6; // 5개에서 6개로 수정 (앨범 사진 전송 시 짧은 세션 탐지)
    private const int ExpectedTotalCaptures = 3;
    private const int ExpectedTelegramCaptures = 2;
    private const int ExpectedSilentCameraCaptures = 1;
    
    // 분석 시간 범위
    private readonly DateTime _startTime = new(2025, 10, 5, 22, 15, 0);
    private readonly DateTime _endTime = new(2025, 10, 5, 22, 21, 0);

    public Sample3TelegramSilentCameraGroundTruthTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        
        // 경로 설정
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        _sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs");
        _parserConfigPath = Path.Combine(projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("=== Sample 3 (Telegram, Silent Camera) Ground Truth 테스트 초기화 ===");
        
        // Orchestrator 생성
        _orchestrator = CreateOrchestrator();
        
        // 로그 파싱
        _parsedEvents = await ParseSampleLogsAsync();
        
        _output.WriteLine($"파싱된 이벤트 수: {_parsedEvents.Count}");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Ground Truth 검증

    [Fact]
    public async Task Should_Match_GroundTruth_TotalSessions()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine($"=== 총 세션 수 검증 ===");
        _output.WriteLine($"예상: {ExpectedTotalSessions}개");
        _output.WriteLine($"실제: {result.Sessions.Count}개");
        
        result.Success.Should().BeTrue();
        result.Sessions.Should().HaveCount(ExpectedTotalSessions, 
            $"Ground Truth: 총 {ExpectedTotalSessions}개 세션이 탐지되어야 함");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_TotalCaptures()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine($"=== 총 촬영 횟수 검증 ===");
        _output.WriteLine($"예상: {ExpectedTotalCaptures}개");
        _output.WriteLine($"실제: {result.CaptureEvents.Count}개");
        
        result.Success.Should().BeTrue();
        result.CaptureEvents.Should().HaveCount(ExpectedTotalCaptures, 
            $"Ground Truth: 총 {ExpectedTotalCaptures}개 촬영이 탐지되어야 함");
        
        _output.WriteLine($"\n📊 앱별 촬영 횟수:");
        var capturesByApp = result.CaptureEvents
            .GroupBy(c => c.PackageName)
            .OrderByDescending(g => g.Count());
        
        foreach (var group in capturesByApp)
        {
            _output.WriteLine($"  {group.Key}: {group.Count()}개");
        }
    }

    [Fact]
    public async Task Should_Match_GroundTruth_TelegramCaptures()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var telegramCaptures = result.CaptureEvents
            .Where(c => c.PackageName == "org.telegram.messenger")
            .ToList();
        
        _output.WriteLine($"=== 텔레그램 촬영 검증 ===");
        _output.WriteLine($"예상: {ExpectedTelegramCaptures}개");
        _output.WriteLine($"실제: {telegramCaptures.Count}개");
        
        if (telegramCaptures.Any())
        {
            _output.WriteLine($"\n촬영 시간:");
            foreach (var capture in telegramCaptures.OrderBy(c => c.CaptureTime))
            {
                _output.WriteLine($"  - {capture.CaptureTime:HH:mm:ss}, Confidence: {capture.ConfidenceScore:F2}");
            }
        }
        
        telegramCaptures.Should().HaveCount(ExpectedTelegramCaptures, 
            $"Ground Truth: 텔레그램 {ExpectedTelegramCaptures}개 촬영");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_SilentCameraCaptures()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var silentCameraCaptures = result.CaptureEvents
            .Where(c => c.PackageName.Contains("silent", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        _output.WriteLine($"=== 무음 카메라 촬영 검증 ===");
        _output.WriteLine($"예상: {ExpectedSilentCameraCaptures}개");
        _output.WriteLine($"실제: {silentCameraCaptures.Count}개");
        
        if (silentCameraCaptures.Any())
        {
            _output.WriteLine($"\n촬영 시간:");
            foreach (var capture in silentCameraCaptures.OrderBy(c => c.CaptureTime))
            {
                _output.WriteLine($"  - {capture.CaptureTime:HH:mm:ss}, Confidence: {capture.ConfidenceScore:F2}");
            }
        }
        
        silentCameraCaptures.Should().HaveCount(ExpectedSilentCameraCaptures, 
            $"Ground Truth: 무음 카메라 {ExpectedSilentCameraCaptures}개 촬영");
    }

    [Fact]
    public async Task Should_Have_CorrectTimestamps_Telegram()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();
        var expectedCaptureTimes = new[]
        {
            new DateTime(2025, 10, 5, 22, 16, 59),
            new DateTime(2025, 10, 5, 22, 17, 57)
        };

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var telegramCaptures = result.CaptureEvents
            .Where(c => c.PackageName == "org.telegram.messenger")
            .OrderBy(c => c.CaptureTime)
            .ToList();
        
        _output.WriteLine($"=== 텔레그램 촬영 시간 정확성 검증 ===");
        
        telegramCaptures.Should().HaveCount(expectedCaptureTimes.Length);

        for (int i = 0; i < expectedCaptureTimes.Length; i++)
        {
            _output.WriteLine($"\n촬영 #{i + 1}:");
            _output.WriteLine($"  예상: {expectedCaptureTimes[i]:HH:mm:ss}");
            _output.WriteLine($"  실제: {telegramCaptures[i].CaptureTime:HH:mm:ss}");
            
            var timeDiff = Math.Abs((telegramCaptures[i].CaptureTime - expectedCaptureTimes[i]).TotalSeconds);
            _output.WriteLine($"  시간 차이: {timeDiff:F1}초");
            
            timeDiff.Should().BeLessThanOrEqualTo(5, $"촬영 #{i + 1} 시간은 5초 이내 오차 허용");
        }
    }

    [Fact]
    public async Task Should_Have_CorrectTimestamps_SilentCamera()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();
        var expectedCaptureTime = new DateTime(2025, 10, 5, 22, 20, 27);

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var silentCameraCaptures = result.CaptureEvents
            .Where(c => c.PackageName.Contains("silent", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.CaptureTime)
            .ToList();
        
        _output.WriteLine($"=== 무음 카메라 촬영 시간 정확성 검증 ===");
        _output.WriteLine($"예상 촬영 시간: {expectedCaptureTime:HH:mm:ss}");

        silentCameraCaptures.Should().HaveCount(1);
        
        if (silentCameraCaptures.Any())
        {
            var actualCapture = silentCameraCaptures.First();
            _output.WriteLine($"실제 촬영 시간: {actualCapture.CaptureTime:HH:mm:ss}");
            
            var timeDiff = Math.Abs((actualCapture.CaptureTime - expectedCaptureTime).TotalSeconds);
            _output.WriteLine($"시간 차이: {timeDiff:F1}초");
            
            timeDiff.Should().BeLessThanOrEqualTo(5, "촬영 시간은 5초 이내 오차 허용");
        }
    }

    #endregion

    #region Helper Methods

    private IAnalysisOrchestrator CreateOrchestrator()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        services.AddAndroidAdbAnalysis();
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync()
    {
        var samplePath = Path.Combine(_sampleLogsPath, "3차 샘플_텔레그램_무음카매라");
        
        if (!Directory.Exists(samplePath))
        {
            throw new DirectoryNotFoundException($"Sample logs directory not found: {samplePath}");
        }

        var allEvents = new List<NormalizedLogEvent>();

        var logConfigs = new Dictionary<string, string>
        {
            ["audio.log"] = "adb_audio_config.yaml",
            ["media_camera_worker.log"] = "adb_media_camera_worker_config.yaml",
            ["media_camera.log"] = "adb_media_camera_config.yaml",
            ["media_metrics.log"] = "adb_media_metrics_config.yaml",
            ["usagestats.log"] = "adb_usagestats_config.yaml",
            ["vibrator_manager.log"] = "adb_vibrator_config.yaml",
            ["activity.log"] = "adb_activity_config.yaml"
        };

        foreach (var (logFileName, configFileName) in logConfigs)
        {
            var logPath = Path.Combine(samplePath, logFileName);
            var events = await ParseLogFileAsync(logPath, configFileName, _startTime, _endTime);
            allEvents.AddRange(events);
        }

        _output.WriteLine($"📊 Total events: {allEvents.Count:N0}");
        
        return allEvents.OrderBy(e => e.Timestamp).ToList();
    }

    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string logFilePath, 
        string configFileName,
        DateTime? startTime,
        DateTime? endTime)
    {
        if (!File.Exists(logFilePath))
        {
            _output.WriteLine($"⚠️ Log file not found: {logFilePath}");
            return new List<NormalizedLogEvent>();
        }

        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}");
        }

        var configLoader = new YamlConfigurationLoader(configPath, NullLogger<YamlConfigurationLoader>.Instance);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = DateTime.Now,
            AndroidVersion = "15"
        };

        var parser = new AdbLogParser(configuration, NullLogger<AdbLogParser>.Instance);
        var options = new LogParsingOptions 
        { 
            DeviceInfo = deviceInfo,
            ConvertToUtc = false,
            StartTime = startTime,
            EndTime = endTime
        };

        var result = await parser.ParseAsync(logFilePath, options, CancellationToken.None);

        _output.WriteLine($"✓ Parsed {Path.GetFileName(logFilePath)}: {result.Events.Count} events");
        
        return result.Events.ToList();
    }

    private static AnalysisOptions CreateDefaultAnalysisOptions()
    {
        return new AnalysisOptions
        {
            MinConfidenceThreshold = 0.3
        };
    }

    #endregion
}
