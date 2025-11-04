using System.Diagnostics;
using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Reports;
using AndroidAdbAnalyze.Analysis.Services.Visualization;
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
/// End-to-End 통합 테스트
/// UI 워크플로우 시뮬레이션: Parser DLL → Analysis DLL → HTML Report
/// </summary>
public sealed class EndToEndAnalysisTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;

    public EndToEndAnalysisTests(ITestOutputHelper output)
    {
        _output = output;
        
        // 경로 설정
        var currentDir = Directory.GetCurrentDirectory();
        
        // 프로젝트 루트: AndroidAdbAnalyzeModule/ (솔루션 디렉토리의 하위)
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        _sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs");
        // ✅ Config 파일 경로 수정: 통합된 Configs 폴더 사용
        _parserConfigPath = Path.Combine(projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
        
        _output.WriteLine($"Current Dir: {currentDir}");
        _output.WriteLine($"Project Root: {projectRoot}");
        _output.WriteLine($"Sample Logs: {_sampleLogsPath}");
        _output.WriteLine($"Parser Configs: {_parserConfigPath}");
    }

    #region Helper Methods

    /// <summary>
    /// UI처럼 Parser DLL을 사용하여 로그 파일 파싱
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string logFilePath, 
        string configFileName,
        DateTime? startTime = null,
        DateTime? endTime = null)
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

        // YAML 설정 로드 (API_Usage_Guide.md 참조)
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
            ConvertToUtc = false,  // 로컬 시간 유지 (시나리오 데이터 시트와 직접 매칭)
            StartTime = startTime,
            EndTime = endTime
        };

        var result = await parser.ParseAsync(logFilePath, options, CancellationToken.None);

        _output.WriteLine($"✓ Parsed {Path.GetFileName(logFilePath)}: {result.Events.Count} events");
        
        return result.Events.ToList();
    }

    /// <summary>
    /// UI처럼 여러 로그 파일을 파싱하여 병합
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync(
        string sampleFolderName, 
        DateTime? startTime = null, 
        DateTime? endTime = null)
    {
        var samplePath = Path.Combine(_sampleLogsPath, sampleFolderName);
        
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
            var events = await ParseLogFileAsync(logPath, configFileName, startTime, endTime);
            allEvents.AddRange(events);
        }

        _output.WriteLine($"📊 Total events from {sampleFolderName}: {allEvents.Count:N0}");
        
        // 디버깅: EventType 통계 출력 (Top 20으로 확대)
        var eventTypeCounts = allEvents
            .GroupBy(e => e.EventType)
            .OrderByDescending(g => g.Count())
            .Take(20);
        
        _output.WriteLine($"📝 Top 20 Event Types:");
        foreach (var group in eventTypeCounts)
        {
            _output.WriteLine($"  - {group.Key}: {group.Count()}개");
        }
        
        // 디버깅: DATABASE 및 MEDIA_INSERT 관련 이벤트 상세 출력
        var dbEvents = allEvents.Where(e => 
            e.EventType.Contains("DATABASE", StringComparison.OrdinalIgnoreCase) || 
            e.EventType.Contains("MEDIA_INSERT", StringComparison.OrdinalIgnoreCase) ||
            e.EventType.Contains("DB_", StringComparison.OrdinalIgnoreCase)).ToList();
        
        _output.WriteLine($"\n🔍 DATABASE/MEDIA_INSERT 관련 이벤트: {dbEvents.Count}개");
        if (dbEvents.Count > 0)
        {
            _output.WriteLine($"  타입별 분포:");
            var dbByType = dbEvents.GroupBy(e => e.EventType).OrderByDescending(g => g.Count());
            foreach (var group in dbByType)
            {
                _output.WriteLine($"    - {group.Key}: {group.Count()}개");
            }
            
            _output.WriteLine($"\n  최근 5개 샘플:");
            foreach (var evt in dbEvents.Take(5))
            {
                _output.WriteLine($"    - {evt.EventType} at {evt.Timestamp:HH:mm:ss.fff}");
                if (evt.Attributes.ContainsKey("package"))
                    _output.WriteLine($"      Package: {evt.Attributes["package"]}");
            }
        }
        else
        {
            _output.WriteLine($"  ⚠️  DATABASE/MEDIA_INSERT 이벤트 없음!");
        }
        
        // 디버깅: CAMERA_CONNECT/DISCONNECT 상세 분석
        var connectEvents = allEvents.Where(e => e.EventType == "CAMERA_CONNECT").ToList();
        var disconnectEvents = allEvents.Where(e => e.EventType == "CAMERA_DISCONNECT").ToList();
        
        _output.WriteLine($"\n🎥 카메라 이벤트 분석:");
        _output.WriteLine($"  CAMERA_CONNECT: {connectEvents.Count}개");
        _output.WriteLine($"  CAMERA_DISCONNECT: {disconnectEvents.Count}개");
        _output.WriteLine($"  불균형: {Math.Abs(connectEvents.Count - disconnectEvents.Count)}개");
        
        return allEvents;
    }

    /// <summary>
    /// Analysis Orchestrator 생성 (DI 컨테이너 기반)
    /// </summary>
    /// <remarks>
    /// Phase 5에서 구현된 ServiceCollectionExtensions.AddAndroidAdbAnalysis()를 사용하여
    /// 모든 분석 서비스를 자동 등록하고 해결합니다.
    /// </remarks>
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
        
        // AnalysisOptions 등록 (EventDeduplicator 의존성)
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });
        
        // AndroidAdbAnalysis 서비스 등록 (Phase 5)
        services.AddAndroidAdbAnalysis();
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        // IAnalysisOrchestrator 해결
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    /// <summary>
    /// 기본 분석 옵션 생성
    /// </summary>
    private AnalysisOptions CreateDefaultAnalysisOptions()
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

    #region Basic Tests

    [Fact]
    public async Task BasicAnalysis_WithMockData_Succeeds()
    {
        // Arrange: 간단한 Mock 데이터
        var events = new List<NormalizedLogEvent>
        {
            new NormalizedLogEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                EventType = "CAMERA_CONNECT",
                SourceSection = "test",
                Attributes = new Dictionary<string, object>
                {
                    ["package"] = "com.sec.android.app.camera",
                    ["pid"] = "12345"
                },
                RawLine = "test",
                SourceFileName = "test.log"
            },
            new NormalizedLogEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddSeconds(10),
                EventType = "CAMERA_DISCONNECT",
                SourceSection = "test",
                Attributes = new Dictionary<string, object>
                {
                    ["package"] = "com.sec.android.app.camera",
                    ["pid"] = "12345"
                },
                RawLine = "test",
                SourceFileName = "test.log"
            }
        };

        var orchestrator = CreateOrchestrator();
        var options = CreateDefaultAnalysisOptions();

        // Act
        var result = await orchestrator.AnalyzeAsync(events, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Statistics.TotalSourceEvents.Should().Be(2);
        
        _output.WriteLine($"✓ Basic analysis succeeded: {result.Statistics.TotalSessions} sessions detected");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task PerformanceBaseline_Sample2_MeasuresExecutionTime()
    {
        // Arrange
        var events = await ParseSampleLogsAsync("1차 샘플_25_10_04");
        events.Should().NotBeEmpty();

        var orchestrator = CreateOrchestrator();
        var options = CreateDefaultAnalysisOptions();

        // Act - 메모리 측정
        var beforeMemory = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();
        
        var result = await orchestrator.AnalyzeAsync(events, options);
        
        stopwatch.Stop();
        var afterMemory = GC.GetTotalMemory(false);
        var memoryUsed = (afterMemory - beforeMemory) / 1024.0 / 1024.0; // MB

        // Assert
        result.Success.Should().BeTrue();

        _output.WriteLine("=== 성능 Baseline ===");
        _output.WriteLine($"이벤트 수: {events.Count:N0}");
        _output.WriteLine($"처리 시간: {stopwatch.Elapsed.TotalSeconds:F3}초");
        _output.WriteLine($"메모리 사용: {memoryUsed:F2} MB");
        _output.WriteLine($"처리 속도: {events.Count / stopwatch.Elapsed.TotalSeconds:F0} events/sec");

        // 성능 기준 (재조정 가능)
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30), "30초 이내 처리");
        memoryUsed.Should().BeLessThan(500, "500MB 미만 사용");
    }

    #endregion

    #region HTML Report Tests

    [Fact]
    public async Task HtmlReport_Sample2_GeneratesAndSaves()
    {
        // Arrange
        var events = await ParseSampleLogsAsync("1차 샘플_25_10_04");
        events.Should().NotBeEmpty();

        var orchestrator = CreateOrchestrator();
        var options = CreateDefaultAnalysisOptions();

        var result = await orchestrator.AnalyzeAsync(events, options);
        result.Success.Should().BeTrue();

        // HTML 생성
        var timelineBuilder = new TimelineBuilder(NullLogger<TimelineBuilder>.Instance);
        var htmlGenerator = new HtmlReportGenerator(
            timelineBuilder,
            NullLogger<HtmlReportGenerator>.Instance);

        // Act
        var htmlReport = htmlGenerator.GenerateReport(result);

        // Assert
        htmlReport.Should().NotBeNullOrEmpty();
        htmlReport.Should().Contain("<!DOCTYPE html>");
        htmlReport.Should().Contain("카메라 세션");
        htmlReport.Should().Contain("촬영 이벤트");

        // 파일 저장 (테스트 출력 디렉토리)
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "test_report_sample1.html");
        await File.WriteAllTextAsync(outputPath, htmlReport);

        _output.WriteLine($"✓ HTML 보고서 생성 완료: {outputPath}");
        _output.WriteLine($"  크기: {htmlReport.Length / 1024.0:F1} KB");
        
        File.Exists(outputPath).Should().BeTrue("HTML 파일이 생성되어야 함");
    }

    #endregion
}
