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
/// Sample 3 (3차 샘플) Ground Truth 검증 테스트
/// </summary>
/// <remarks>
/// 시나리오 데이터 시트 (2025-10-05):
/// 
/// 분석 시간 범위: 21:58:00 ~ 22:06:00
/// 
/// 기본 카메라:
/// - 21:58:03 실행 → 21:58:09 종료 (촬영 없음)
/// - 21:59:08 실행 → 21:59:13 사진 촬영 → 21:59:18 종료
/// 
/// 카카오톡:
/// - 22:01:05 실행 → 22:01:10 종료 (촬영 없음)
/// - 22:02:17 실행 → 22:02:27 사진 촬영 → 22:02:32 종료
/// - 22:03:58 실행 → 22:04:03 사진 촬영 → 22:04:08 종료 → 22:04:13 사진 전송
/// - 22:05:53 기존 앨범 사진 전송 (촬영 없음)
/// 
/// Ground Truth:
/// - 총 세션: 5개 (기본 카메라 2 + 카카오톡 3)
/// - 총 촬영: 3개 (기본 카메라 1 + 카카오톡 2)
/// </remarks>
public sealed class Sample3GroundTruthTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    private List<NormalizedLogEvent>? _parsedEvents;
    
    // Ground Truth 상수
    private const int ExpectedTotalSessions = 5;
    private const int ExpectedTotalCaptures = 3;
    private const int ExpectedDefaultCameraCaptures = 1;
    private const int ExpectedKakaoTalkCaptures = 2;
    
    // 분석 시간 범위
    private readonly DateTime _startTime = new(2025, 10, 5, 21, 57, 0);
    private readonly DateTime _endTime = new(2025, 10, 5, 22, 06, 0);

    public Sample3GroundTruthTests(ITestOutputHelper output)
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
        _output.WriteLine("=== Sample 3 Ground Truth 테스트 초기화 ===");
        
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
        
        // 앱별 촬영 횟수 출력
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
    public async Task Should_Match_GroundTruth_DefaultCameraCaptures()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var defaultCameraCaptures = result.CaptureEvents
            .Where(c => c.PackageName == "com.sec.android.app.camera")
            .ToList();
        
        _output.WriteLine($"=== 기본 카메라 촬영 검증 ===");
        _output.WriteLine($"예상: {ExpectedDefaultCameraCaptures}개");
        _output.WriteLine($"실제: {defaultCameraCaptures.Count}개");
        
        if (defaultCameraCaptures.Any())
        {
            _output.WriteLine($"\n촬영 시간:");
            foreach (var capture in defaultCameraCaptures.OrderBy(c => c.CaptureTime))
            {
                _output.WriteLine($"  - {capture.CaptureTime:HH:mm:ss}, Confidence: {capture.ConfidenceScore:F2}");
            }
        }
        
        defaultCameraCaptures.Should().HaveCount(ExpectedDefaultCameraCaptures, 
            $"Ground Truth: 기본 카메라 {ExpectedDefaultCameraCaptures}개 촬영");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_KakaoTalkCaptures()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var kakaoCaptures = result.CaptureEvents
            .Where(c => c.PackageName == "com.kakao.talk")
            .ToList();
        
        _output.WriteLine($"=== 카카오톡 촬영 검증 ===");
        _output.WriteLine($"예상: {ExpectedKakaoTalkCaptures}개");
        _output.WriteLine($"실제: {kakaoCaptures.Count}개");
        
        if (kakaoCaptures.Any())
        {
            _output.WriteLine($"\n촬영 시간:");
            foreach (var capture in kakaoCaptures.OrderBy(c => c.CaptureTime))
            {
                _output.WriteLine($"  - {capture.CaptureTime:HH:mm:ss}, Confidence: {capture.ConfidenceScore:F2}");
                _output.WriteLine($"    Evidence: {string.Join(", ", capture.EvidenceTypes)}");
            }
        }
        
        kakaoCaptures.Should().HaveCount(ExpectedKakaoTalkCaptures, 
            $"Ground Truth: 카카오톡 {ExpectedKakaoTalkCaptures}개 촬영");
    }

    [Fact]
    public async Task Should_Have_CorrectTimestamps_DefaultCamera()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();
        var expectedCaptureTime = new DateTime(2025, 10, 5, 21, 59, 13);

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var defaultCameraCaptures = result.CaptureEvents
            .Where(c => c.PackageName == "com.sec.android.app.camera")
            .OrderBy(c => c.CaptureTime)
            .ToList();
        
        _output.WriteLine($"=== 기본 카메라 촬영 시간 정확성 검증 ===");
        _output.WriteLine($"예상 촬영 시간: {expectedCaptureTime:HH:mm:ss}");
        
        if (defaultCameraCaptures.Any())
        {
            var actualCapture = defaultCameraCaptures.First();
            _output.WriteLine($"실제 촬영 시간: {actualCapture.CaptureTime:HH:mm:ss}");
            
            // 5초 이내 오차 허용
            var timeDiff = Math.Abs((actualCapture.CaptureTime - expectedCaptureTime).TotalSeconds);
            _output.WriteLine($"시간 차이: {timeDiff:F1}초");
            
            timeDiff.Should().BeLessThanOrEqualTo(5, "촬영 시간은 5초 이내 오차 허용");
        }
    }

    [Fact]
    public async Task Should_Have_CorrectTimestamps_KakaoTalk()
    {
        // Arrange
        var options = CreateDefaultAnalysisOptions();
        var expectedCaptureTimes = new[]
        {
            new DateTime(2025, 10, 5, 22, 02, 27),
            new DateTime(2025, 10, 5, 22, 04, 03)
        };

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var kakaoCaptures = result.CaptureEvents
            .Where(c => c.PackageName == "com.kakao.talk")
            .OrderBy(c => c.CaptureTime)
            .ToList();
        
        _output.WriteLine($"=== 카카오톡 촬영 시간 정확성 검증 ===");
        
        for (int i = 0; i < Math.Min(expectedCaptureTimes.Length, kakaoCaptures.Count); i++)
        {
            _output.WriteLine($"\n촬영 #{i + 1}:");
            _output.WriteLine($"  예상: {expectedCaptureTimes[i]:HH:mm:ss}");
            _output.WriteLine($"  실제: {kakaoCaptures[i].CaptureTime:HH:mm:ss}");
            
            var timeDiff = Math.Abs((kakaoCaptures[i].CaptureTime - expectedCaptureTimes[i]).TotalSeconds);
            _output.WriteLine($"  시간 차이: {timeDiff:F1}초");
            
            timeDiff.Should().BeLessThanOrEqualTo(5, $"촬영 #{i + 1} 시간은 5초 이내 오차 허용");
        }
    }

    #endregion

    #region Helper Methods

    private IAnalysisOrchestrator CreateOrchestrator()
    {
        // DI 컨테이너 설정
        var services = new ServiceCollection();
        
        // Logging 인프라 추가
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AndroidAdbAnalysis 서비스 등록
        services.AddAndroidAdbAnalysis();
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        // IAnalysisOrchestrator 해결
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync()
    {
        var samplePath = Path.Combine(_sampleLogsPath, "3차 샘플_기본카메라_카카오톡");
        
        if (!Directory.Exists(samplePath))
        {
            throw new DirectoryNotFoundException($"Sample logs directory not found: {samplePath}");
        }

        var allEvents = new List<NormalizedLogEvent>();

        // 로그 파일 매핑 (실제 파일명 → 설정 파일)
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

        // YAML 설정 로드
        var configLoader = new YamlConfigurationLoader(configPath, NullLogger<YamlConfigurationLoader>.Instance);
        var configuration = await configLoader.LoadAsync(configPath);

        // DeviceInfo 생성
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = DateTime.Now,
            AndroidVersion = "15",
            Manufacturer = "Samsung",
            Model = "SM-G991N"
        };

        // Parser 생성 및 파싱
        var parser = new AdbLogParser(configuration, NullLogger<AdbLogParser>.Instance);
        var options = new LogParsingOptions 
        { 
            MaxFileSizeMB = 50,
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
            // 모든 패키지 분석
            PackageWhitelist = null,
            PackageBlacklist = Array.Empty<string>(),
            
            // 세션 설정
            MaxSessionGap = TimeSpan.FromMinutes(5),
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            
            // 신뢰도 임계값
            MinConfidenceThreshold = 0.3,
            
            // 오탐 방지
            ScreenshotPathPatterns = new[] { "screenshot", "Screenshot" },
            DownloadPathPatterns = new[] { "download", "Download" },
            
            // 불완전 세션 처리
            EnableIncompleteSessionHandling = true
        };
    }

    #endregion
}
