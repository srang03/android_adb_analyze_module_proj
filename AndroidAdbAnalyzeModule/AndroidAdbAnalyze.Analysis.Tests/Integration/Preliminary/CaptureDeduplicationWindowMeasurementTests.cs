namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Preliminary;

using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Parser.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;

/// <summary>
/// CaptureDeduplicationWindow 파라미터 실측 검증 테스트 (예비 실험)
/// 목적: 하나의 촬영에서 발생하는 모든 아티팩트의 시간 범위 측정
/// 방법: 예비 실험 1~3차 데이터 분석
/// </summary>
public sealed class CaptureDeduplicationWindowMeasurementTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    private AndroidAdbAnalyze.Analysis.Models.Results.AnalysisResult? _preliminaryResult;
    private List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>? _allParsedEvents;

    public CaptureDeduplicationWindowMeasurementTests(ITestOutputHelper output)
    {
        _output = output;
        
        // 경로 설정
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        _sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs");
        _parserConfigPath = Path.Combine(projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
    }

    private List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>? _allSessions;

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 예비 실험 CaptureDeduplicationWindow 측정 초기화");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 예비 실험 3회 통합 분석
        var allCaptures = new List<AndroidAdbAnalyze.Analysis.Models.Events.CameraCaptureEvent>();
        var allSessions = new List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>();
        var allEvents = new List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>();
        
        var preliminarySamples = new[]
        {
            ("예비 실험/예비 실험 1차 25_09_01", new DateTime(2025, 9, 1, 9, 0, 0), new DateTime(2025, 9, 1, 23, 59, 59)),
            ("예비 실험/예비 실험 2차 25_09_06", new DateTime(2025, 9, 6, 9, 0, 0), new DateTime(2025, 9, 6, 23, 59, 59)),
            ("예비 실험/예비 실험 3차 25_09_07", new DateTime(2025, 9, 7, 9, 0, 0), new DateTime(2025, 9, 7, 23, 59, 59))
        };

        foreach (var (sampleDir, startTime, endTime) in preliminarySamples)
        {
            _output.WriteLine($"분석 중: {sampleDir}");
            var (result, parsedEvents) = await AnalyzeSample(sampleDir, startTime, endTime);
            
            allCaptures.AddRange(result.CaptureEvents);
            allSessions.AddRange(result.Sessions);
            allEvents.AddRange(parsedEvents);
            
            _output.WriteLine($"  세션: {result.Sessions.Count}개, 촬영: {result.CaptureEvents.Count}개\n");
        }

        _preliminaryResult = new AndroidAdbAnalyze.Analysis.Models.Results.AnalysisResult
        {
            Success = true,
            Sessions = allSessions,
            CaptureEvents = allCaptures
        };
        
        _allParsedEvents = allEvents;
        _allSessions = allSessions;

        _output.WriteLine($"예비 실험 총 세션: {allSessions.Count}개, 총 촬영: {allCaptures.Count}개\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Measure_CaptureArtifactTimeRange_PreliminaryExperiments()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 하나의 촬영에서 발생하는 아티팩트 시간 범위 측정");
        _output.WriteLine("   (예비 실험 1~3차, 총 12개 촬영)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 1. 통계 계산
        var statistics = CaptureDeduplicationWindowValidator
            .CalculateArtifactTimeRangeStatistics(_allSessions!, _allParsedEvents!, _output);

        // 2. 검증
        statistics.TotalCaptures.Should().BeGreaterThan(0, "예비 실험에서 촬영이 존재해야 함");
        statistics.MaxTimeRangeMs.Should().BeGreaterThan(0, "최대 시간 범위가 0보다 커야 함");

        // 3. 파라미터 설정 근거 계산
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📝 CaptureDeduplicationWindow 파라미터 설정 근거");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        var maxTimeRangeMs = statistics.MaxTimeRangeMs;
        var recommendedWindowMs = ArtifactWeights.CaptureDeduplicationWindowMs;
        var safetyMargin = recommendedWindowMs / maxTimeRangeMs;

        _output.WriteLine($"측정된 최대 시간 범위: {maxTimeRangeMs:F0}ms");
        _output.WriteLine($"권장 윈도우 설정: {recommendedWindowMs:F0}ms ({recommendedWindowMs / 1000.0:F1}초)");
        _output.WriteLine($"안전 마진: {safetyMargin:F2}배\n");

        _output.WriteLine("설정 근거:");
        _output.WriteLine($"  - 하나의 촬영에서 발생하는 핵심 아티팩트의 최대 시간 범위는 {maxTimeRangeMs:F0}ms");
        _output.WriteLine($"  - 시스템 변동성을 고려하여 {safetyMargin:F2}배 안전 마진 적용");
        _output.WriteLine($"  - 다른 파라미터와의 일관성: SameCameraUsageTimeThreshold (2.0배)와 유사한 수준");
        _output.WriteLine($"  - {recommendedWindowMs / 1000.0:F1}초 설정으로 동일 촬영의 핵심 아티팩트를 포괄하면서도");
        _output.WriteLine($"    {recommendedWindowMs:F0}ms 이상 간격의 별도 촬영은 구분 가능\n");

        // 4. 논문 작성용 요약
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📝 논문 작성용 요약 (제4장 제4절, 제5장 제3절)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("**측정 방법**:");
        _output.WriteLine("  - 측정 대상: 예비 실험 3회(12개 촬영)에서 하나의 촬영에서 발생하는 핵심 아티팩트의 시간 범위");
        _output.WriteLine("  - 포함 대상: DATABASE_INSERT, SILENT_CAMERA_CAPTURE, VIBRATION_EVENT, PLAYER_EVENT, URI_PERMISSION_GRANT");
        _output.WriteLine("  - 제외 대상: FOREGROUND_SERVICE, MEDIA_EXTRACTOR, PLAYER_CREATED, PLAYER_RELEASED (촬영 시점과 시간적 분리)");
        _output.WriteLine("  - 측정 방법: 각 촬영에서 발생한 핵심 아티팩트의 타임스탬프 수집");
        _output.WriteLine("  - 계산: 첫 번째 핵심 아티팩트부터 마지막 핵심 아티팩트까지의 시간 간격 측정\n");

        _output.WriteLine("**측정 결과**:");
        _output.WriteLine($"  - 최대값: {maxTimeRangeMs:F0}ms");
        _output.WriteLine($"  - 최소값: {statistics.MinTimeRangeMs:F0}ms");
        _output.WriteLine($"  - 평균값: {statistics.AverageTimeRangeMs:F1}ms");
        _output.WriteLine($"  - 측정 샘플: {statistics.TotalCaptures}개\n");

        _output.WriteLine("**파라미터 설정**:");
        _output.WriteLine($"  - 최대 측정값({maxTimeRangeMs:F0}ms)에 {safetyMargin:F2}배 안전 마진 적용");
        _output.WriteLine($"  - 최종 설정: {recommendedWindowMs / 1000.0:F1}초 ({recommendedWindowMs:F0}ms)");
        _output.WriteLine($"  - 근거: 동일 촬영의 핵심 아티팩트 포괄 + 별도 촬영 구분 가능 + 파라미터 간 일관성 확보\n");

        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 5. Assertion
        safetyMargin.Should().BeGreaterThan(1.0, "안전 마진이 1.0배보다 커야 함");
        safetyMargin.Should().BeLessThan(2.5, "안전 마진이 2.5배보다 작아야 함 (다른 파라미터와의 일관성)");
    }

    #region Helper Methods

    private async Task<(AndroidAdbAnalyze.Analysis.Models.Results.AnalysisResult, List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>)> AnalyzeSample(
        string sampleDirectory, 
        DateTime startTime, 
        DateTime endTime)
    {
        _output.WriteLine($"분석 중: {sampleDirectory}");
        
        // 1. 로그 파싱
        var samplePath = Path.Combine(_sampleLogsPath, sampleDirectory);
        var parsedEvents = await ParseSampleLogsAsync(samplePath, startTime, endTime);
        
        _output.WriteLine($"  파싱된 이벤트: {parsedEvents.Count}개");
        
        // 2. 분석 실행
        var orchestrator = CreateOrchestrator();
        var options = CreateAnalysisOptions();
        var result = await orchestrator.AnalyzeAsync(parsedEvents, options);
        
        return (result, parsedEvents);
    }

    private async Task<List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>> ParseSampleLogsAsync(
        string samplePath,
        DateTime startTime,
        DateTime endTime)
    {
        var allEvents = new List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>();

        // 로그 파일 매핑
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
            if (!File.Exists(logPath))
                continue;

            var events = await ParseLogFileAsync(logPath, configFileName, startTime, endTime);
            allEvents.AddRange(events);
        }

        return allEvents;
    }

    private async Task<List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>> ParseLogFileAsync(
        string logFilePath,
        string configFileName,
        DateTime startTime,
        DateTime endTime)
    {
        if (!File.Exists(logFilePath))
        {
            _output.WriteLine($"⚠️ Log file not found: {logFilePath}");
            return new List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>();
        }

        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
        {
            _output.WriteLine($"⚠️ Config file not found: {configPath}");
            return new List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>();
        }

        // YAML 설정 로드
        var configLoader = new AndroidAdbAnalyze.Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
        var configuration = configLoader.Load(configPath);

        // DeviceInfo 생성
        var deviceInfo = new AndroidAdbAnalyze.Parser.Core.Models.DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = startTime,
            Model = "SM-S911N",
            AndroidVersion = "15",
            Manufacturer = "Samsung"
        };

        // Parser 생성
        var parser = new AndroidAdbAnalyze.Parser.Parsing.AdbLogParser(configuration, NullLogger<AndroidAdbAnalyze.Parser.Parsing.AdbLogParser>.Instance);

        // 로그 파싱 옵션
        var options = new AndroidAdbAnalyze.Parser.Core.Models.LogParsingOptions
        {
            MaxFileSizeMB = 50,
            DeviceInfo = deviceInfo,
            ConvertToUtc = false,
            StartTime = startTime,
            EndTime = endTime
        };

        try
        {
            var result = await parser.ParseAsync(logFilePath, options);
            var events = result.Events?.ToList() ?? new List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>();
            
            _output.WriteLine($"✓ {Path.GetFileName(logFilePath),-30} : {events.Count,6:N0} events");
            return events;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Error parsing {Path.GetFileName(logFilePath)}: {ex.Message}");
            return new List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>();
        }
    }

    private IAnalysisOrchestrator CreateOrchestrator()
    {
        var services = new ServiceCollection();
        RegisterServices(services);
        
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    private void RegisterServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        
        // Analysis Options
        services.AddSingleton(CreateAnalysisOptions());
        
        // Register all analysis services manually
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
        services.AddSingleton<ISessionDetector, AndroidAdbAnalyze.Analysis.Services.Sessions.CameraSessionDetector>();
        services.AddSingleton<ICaptureDetector, AndroidAdbAnalyze.Analysis.Services.Captures.CameraCaptureDetector>();
        services.AddSingleton<IEventDeduplicator, AndroidAdbAnalyze.Analysis.Services.Deduplication.EventDeduplicator>();
        services.AddSingleton<IConfidenceCalculator, AndroidAdbAnalyze.Analysis.Services.Confidence.ConfidenceCalculator>();
        services.AddSingleton<ISessionContextProvider, AndroidAdbAnalyze.Analysis.Services.Context.SessionContextProvider>();
        services.AddSingleton<ITimelineBuilder, AndroidAdbAnalyze.Analysis.Services.Visualization.TimelineBuilder>();
        
        // Session Sources
        services.AddSingleton<ISessionSource, AndroidAdbAnalyze.Analysis.Services.Sessions.Sources.UsagestatsSessionSource>();
        services.AddSingleton<ISessionSource, AndroidAdbAnalyze.Analysis.Services.Sessions.Sources.MediaCameraSessionSource>();
        
        // Detection Strategies
        services.AddSingleton<ICaptureDetectionStrategy, AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.BasePatternStrategy>();
        services.AddSingleton<ICaptureDetectionStrategy, AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.TelegramStrategy>();
        services.AddSingleton<ICaptureDetectionStrategy, AndroidAdbAnalyze.Analysis.Services.DetectionStrategies.KakaoTalkStrategy>();
    }

    private AnalysisOptions CreateAnalysisOptions()
    {
        // AnalysisOptions 기본값 사용 (하드코딩 금지)
        return new AnalysisOptions();
    }

    #endregion
}

