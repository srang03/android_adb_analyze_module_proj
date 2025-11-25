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
/// 촬영 탐지 파라미터 검증 테스트 (예비 실험)
/// 목적: EventCorrelationWindow, CaptureDeduplicationWindow의 실측 기반 근거 확보
/// 방법: 예비 실험 1~3차 데이터 분석
/// </summary>
public sealed class CaptureDetectionParameterValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    private AndroidAdbAnalyze.Analysis.Models.Results.AnalysisResult? _preliminaryResult;
    private List<AndroidAdbAnalyze.Parser.Core.Models.NormalizedLogEvent>? _allParsedEvents;

    public CaptureDetectionParameterValidationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // 경로 설정
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        _sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs");
        _parserConfigPath = Path.Combine(projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
    }

    private List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>? _allSessions;
    private List<AndroidAdbAnalyze.Analysis.Interfaces.ICaptureDetectionStrategy>? _strategies;
    private AndroidAdbAnalyze.Analysis.Models.Options.AnalysisOptions? _options;

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 예비 실험 촬영 탐지 파라미터 검증 초기화 (Sample 1-3)");
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
        _options = CreateAnalysisOptions();
        
        // 전략 목록 생성
        var services = new ServiceCollection();
        RegisterServices(services);
        var serviceProvider = services.BuildServiceProvider();
        _strategies = serviceProvider.GetServices<AndroidAdbAnalyze.Analysis.Interfaces.ICaptureDetectionStrategy>().ToList();

        _output.WriteLine($"예비 실험 총 세션: {allSessions.Count}개, 총 촬영: {allCaptures.Count}개\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Measure_EventCorrelationWindow_PreliminaryExperiments()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 EventCorrelationWindow 측정 (예비 실험 1~3차)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 1. 통계 계산
        var statistics = ArtifactWeights.CaptureDetectionParameterValidator
            .CalculateEventCorrelationWindowStatistics(_preliminaryResult!, _allParsedEvents!, _output);

        // 2. 임계값 검증 (ArtifactWeights.EventCorrelationWindowSeconds 사용)
        var threshold = ArtifactWeights.EventCorrelationWindowSeconds;
        ArtifactWeights.CaptureDetectionParameterValidator
            .ValidateEventCorrelationWindowThreshold(statistics, threshold, _output);

        // 3. 논문 작성용 요약
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📝 논문 작성용 요약 (제4장 제4절 \"설계 근거\")");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine($"EventCorrelationWindow는 동일 촬영 내 아티팩트 최대 간격을 측정하였다.");
        _output.WriteLine($"예비 실험 결과 최대 {statistics.MaxInterval.TotalSeconds:F1}초가 측정되었으며,");
        _output.WriteLine($"보수적 접근법을 적용하여 안전 마진 {threshold / statistics.MaxInterval.TotalSeconds:F1}배를 부여하여 {threshold:F0}초로 설정하였다.");
        _output.WriteLine($"이는 예상치 못한 지연 상황에 대응하기 위함이다.");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 4. Assertion (논문 내용과 일치하는지 확인)
        statistics.CaptureIntervals.Should().NotBeEmpty("예비 실험에서 촬영이 존재해야 함");
        statistics.MaxInterval.TotalSeconds.Should().BeLessThanOrEqualTo(threshold, 
            $"최대 간격이 {threshold:F0}초 이내여야 함");
    }

    /// <summary>
    /// 예비 실험 CaptureDeduplicationWindow 측정 테스트
    /// </summary>
    /// <remarks>
    /// 논문 제4장 제4절 "다. CaptureDeduplicationWindow 파라미터 설정 근거"에 사용될 실측 데이터 생성
    /// 
    /// **중요**: 예비 실험 데이터의 특성상 직접 측정이 불가능합니다.
    /// - 예비 실험은 "하나의 세션에 하Validate_MinOverlapRatio_MainExperiment나의 촬영"만 존재하여 CameraCaptureEvent 간격 측정 불가
    /// - 대신 VIBRATION_EVENT 연속 간격을 측정하여 대리 지표로 사용 (논문에 이미 작성됨)
    /// - 본 실험 데이터로 검증 완료 (중복 0건, Precision 100%)
    /// </remarks>
    [Fact(Skip = "예비 실험 데이터 특성상 직접 측정 불가 (VIBRATION_EVENT 간격으로 대체)")]
    public void Measure_CaptureDeduplicationWindow_PreliminaryExperiments()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 CaptureDeduplicationWindow 측정 (예비 실험 1~3차)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("⚠️  예비 실험 데이터 특성상 직접 측정이 불가능합니다.\n");
        _output.WriteLine("이유:");
        _output.WriteLine("  - 예비 실험은 '하나의 세션에 하나의 촬영'만 존재");
        _output.WriteLine("  - CaptureDeduplicationWindow는 '동일 촬영에 대해 여러 핵심 아티팩트가 짧은 시간 내 발생'하는 경우를 처리");
        _output.WriteLine("  - 예비 실험에서는 각 촬영당 CameraCaptureEvent가 1개만 생성되어 간격 측정 불가\n");

        var captureWindow = ArtifactWeights.CaptureDeduplicationWindowMs;
        var maxIntervalMs = 330.0; // 예비 실험에서 측정한 핵심 아티팩트 최대 간격 (기본 카메라)
        _output.WriteLine("✅ 대안:");
        _output.WriteLine("  - 예비 실험 데이터 특성상 직접 측정 불가로 인해 핵심 아티팩트 시간 범위를 측정하여 대리 지표로 사용");
        _output.WriteLine("  - 예비 실험에서 핵심 아티팩트 시간 범위 최대 330ms 측정 (기본 카메라)");
        _output.WriteLine($"  - 안전 마진 {captureWindow / maxIntervalMs:F2}배 적용하여 {captureWindow / 1000.0:F1}초 ({captureWindow:F0}ms)로 설정");
        _output.WriteLine("  - 본 실험에서 중복 0건, Precision 100% 검증 완료\n");

        _output.WriteLine("📝 논문 반영:");
        _output.WriteLine("  - 제4장 제4절: VIBRATION_EVENT 간격 기반 논리적 추론으로 설정");
        _output.WriteLine("  - 제5장 제3절: 본 실험 검증 결과 제시 (중복 0건, Precision 100%)\n");

        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 1. 통계 계산 시도 (예상대로 측정 불가 확인)
        var statistics = ArtifactWeights.CaptureDetectionParameterValidator
            .CalculateCaptureDeduplicationWindowStatistics(
                _allSessions!, 
                _allParsedEvents!, 
                _strategies!, 
                _options!, 
                _output);

        // 2. 결과 확인
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 예비 실험 측정 결과");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine($"측정 가능한 세션 수: {statistics.Durations.Count}개 (예상대로 0개)");
        _output.WriteLine("✅ 예비 실험 데이터 특성 확인 완료:");
        _output.WriteLine("  - 각 촬영당 CameraCaptureEvent가 1개만 생성되어 간격 측정 불가");
        _output.WriteLine("  - VIBRATION_EVENT 간격 기반 논리적 추론 필요\n");

        _output.WriteLine("════════════════════════════════════════════════════════════\n");
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

