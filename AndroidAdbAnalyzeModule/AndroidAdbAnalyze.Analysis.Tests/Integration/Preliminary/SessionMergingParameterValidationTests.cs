using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Services.Captures;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Context;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;
using AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Analysis.Services.Reports;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Analysis.Services.Transmission;
using AndroidAdbAnalyze.Analysis.Services.Visualization;
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using static AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants.ArtifactWeights;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Preliminary;

/// <summary>
/// 세션 병합 파라미터 타당성 검증 테스트 (예비 실험 기반)
/// </summary>
/// <remarks>
/// 목적:
/// - 예비 실험(Preliminary 1-3)에서 MinOverlapRatio 설정 근거 측정
/// - 같은 세션 쌍의 평균/최소/최대 겹침 비율 계산
/// - 다른 세션 쌍의 평균/최소/최대 겹침 비율 계산
/// - 80% 임계값의 타당성 검증
/// 
/// 논문 반영:
/// - 제4장 제3절: MinOverlapRatio 설정 근거 (예비 실험 기반)
/// - 제5장 제3절: 본 실험 검증 (Sample 1-10 기반)
/// 
/// 설계 원칙:
/// - 하드코딩 없음: 모든 데이터는 실제 분석 결과에서 추출
/// - 재사용 가능: SessionMergingParameterValidator 공용 메서드 사용
/// - 검증 가능: 계산 과정과 결과를 명확히 출력
/// </remarks>
public sealed class SessionMergingParameterValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    // 예비 실험 원본 세션 캐싱 (병합 전)
    private List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>? _preliminary1RawSessions;
    private List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>? _preliminary2RawSessions;
    private List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>? _preliminary3RawSessions;

    public SessionMergingParameterValidationTests(ITestOutputHelper output)
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
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("🔬 예비 실험 세션 병합 파라미터 검증 테스트 초기화");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // Preliminary 1-3 원본 세션 추출 (병합 전)
        _preliminary1RawSessions = await ExtractRawSessionsFromSample("예비 실험/예비 실험 1차 25_09_01", 
            new DateTime(2025, 9, 1, 9, 45, 0), 
            new DateTime(2025, 9, 1, 9, 53, 0));
        
        _preliminary2RawSessions = await ExtractRawSessionsFromSample("예비 실험/예비 실험 2차 25_09_06", 
            new DateTime(2025, 9, 6, 10, 10, 0), 
            new DateTime(2025, 9, 6, 10, 22, 0));
        
        _preliminary3RawSessions = await ExtractRawSessionsFromSample("예비 실험/예비 실험 3차 25_09_07", 
            new DateTime(2025, 9, 7, 10, 35, 0), 
            new DateTime(2025, 9, 7, 10, 44, 59));
        
        _output.WriteLine("\n✅ 예비 실험 3회 원본 세션 추출 완료 (병합 전)\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 예비 실험 MinOverlapRatio 측정 테스트
    /// </summary>
    /// <remarks>
    /// 논문 제4장 제3절 "나. 시간 겹침 비율(MinOverlapRatio) 설정 근거"에 사용될 실측 데이터 생성
    /// 
    /// **측정 대상**: "같은 세션 쌍" = usagestats + media.camera 쌍 (서로 다른 로그 소스에서 추출된 같은 카메라 사용)
    /// - Activity 기반 앱(기본 카메라, 카카오톡)은 usagestats와 media.camera에 동시 기록
    /// - 예비 실험 3회 × 2개 앱 × 2개 시나리오 = 12개 쌍 예상
    /// - 이들 쌍의 시간 겹침 비율을 측정하여 80% 임계값 타당성 검증
    /// </remarks>
    [Fact]
    public void Measure_MinOverlapRatio_PreliminaryExperiments()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 예비 실험 MinOverlapRatio 측정 (Preliminary 1-3)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 1. 각 예비 실험별 통계 계산 (병합 전 원본 세션 사용)
        _output.WriteLine("📋 예비 실험 1차 분석:");
        var stats1 = SessionMergingParameterValidator.CalculateOverlapRatioStatistics(
            _preliminary1RawSessions!, _output);

        _output.WriteLine("📋 예비 실험 2차 분석:");
        var stats2 = SessionMergingParameterValidator.CalculateOverlapRatioStatistics(
            _preliminary2RawSessions!, _output);

        _output.WriteLine("📋 예비 실험 3차 분석:");
        var stats3 = SessionMergingParameterValidator.CalculateOverlapRatioStatistics(
            _preliminary3RawSessions!, _output);

        // 2. 예비 실험 3회 통합 통계 계산
        var allSameSessionPairs = stats1.SameSessionPairs
            .Concat(stats2.SameSessionPairs)
            .Concat(stats3.SameSessionPairs)
            .ToList();

        var allDifferentSessionPairs = stats1.DifferentSessionPairs
            .Concat(stats2.DifferentSessionPairs)
            .Concat(stats3.DifferentSessionPairs)
            .ToList();

        // 3. 결과 출력
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 예비 실험 3회 통합 결과");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine($"총 세션 쌍 분석:");
        _output.WriteLine($"  - 같은 세션 쌍 (usagestats + media.camera): {allSameSessionPairs.Count}개");
        _output.WriteLine($"  - 다른 세션 쌍: {allDifferentSessionPairs.Count}개\n");

        if (allSameSessionPairs.Any())
        {
            var avgOverlap = allSameSessionPairs.Average(p => p.OverlapRatio);
            var minOverlap = allSameSessionPairs.Min(p => p.OverlapRatio);
            var maxOverlap = allSameSessionPairs.Max(p => p.OverlapRatio);
            
            _output.WriteLine("✅ 예비 실험 MinOverlapRatio 측정 완료:");
            var threshold = ArtifactWeights.MinOverlapRatio;
            _output.WriteLine($"  - 평균 겹침 비율: {avgOverlap:P0} ({avgOverlap:F2})");
            _output.WriteLine($"  - 최소 겹침 비율: {minOverlap:P0} ({minOverlap:F2})");
            _output.WriteLine($"  - 최대 겹침 비율: {maxOverlap:P0} ({maxOverlap:F2})");
            _output.WriteLine($"  - 설정 임계값 {threshold:P0} 타당성: {(minOverlap >= threshold ? "✅ 타당" : "⚠️ 재검토 필요")}\n");
        }
        else
        {
            _output.WriteLine("⚠️  같은 세션 쌍이 발견되지 않았습니다.");
            _output.WriteLine("  - Activity 기반 앱(기본 카메라, 카카오톡)에서 usagestats + media.camera 쌍이 생성되어야 함");
            _output.WriteLine("  - 로그 파싱 또는 세션 추출 로직 확인 필요\n");
        }

        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // JSON 파일로 결과 저장
        var resultPath = Path.Combine(Directory.GetCurrentDirectory(), "preliminary_minoverlapratio_result.json");
        var result = new
        {
            TotalSameSessionPairs = allSameSessionPairs.Count,
            TotalDifferentSessionPairs = allDifferentSessionPairs.Count,
            SameSessionPairs = allSameSessionPairs.Select(p => new
            {
                Session1Package = p.Session1.PackageName,
                Session1StartTime = p.Session1.StartTime.ToString("HH:mm:ss"),
                Session1EndTime = p.Session1.EndTime?.ToString("HH:mm:ss"),
                Session1Sources = string.Join(",", p.Session1.SourceLogTypes),
                Session2Package = p.Session2.PackageName,
                Session2StartTime = p.Session2.StartTime.ToString("HH:mm:ss"),
                Session2EndTime = p.Session2.EndTime?.ToString("HH:mm:ss"),
                Session2Sources = string.Join(",", p.Session2.SourceLogTypes),
                OverlapRatio = p.OverlapRatio
            }).ToList(),
            Statistics = allSameSessionPairs.Any() ? new
            {
                AverageOverlap = allSameSessionPairs.Average(p => p.OverlapRatio),
                MinOverlap = allSameSessionPairs.Min(p => p.OverlapRatio),
                MaxOverlap = allSameSessionPairs.Max(p => p.OverlapRatio)
            } : null
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(resultPath, json);
        _output.WriteLine($"✅ 결과가 JSON 파일로 저장되었습니다: {resultPath}");
    }

    #region Helper Methods

    /// <summary>
    /// 예비 실험 샘플에서 병합 전 원본 세션을 추출합니다.
    /// </summary>
    private async Task<List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>> ExtractRawSessionsFromSample(
        string sampleDirectory, 
        DateTime startTime, 
        DateTime endTime)
    {
        _output.WriteLine($"분석 중: {sampleDirectory}");
        
        // 1. 로그 파싱
        var samplePath = Path.Combine(_sampleLogsPath, sampleDirectory);
        var parsedEvents = await ParseSampleLogsAsync(samplePath, startTime, endTime);
        
        _output.WriteLine($"  파싱된 이벤트: {parsedEvents.Count}개");
        
        // 2. SessionSource들을 직접 호출하여 병합 전 원본 세션 추출
        var confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        var sessionSources = new List<ISessionSource>
        {
            new UsagestatsSessionSource(NullLogger<UsagestatsSessionSource>.Instance, confidenceCalculator),
            new MediaCameraSessionSource(NullLogger<MediaCameraSessionSource>.Instance, confidenceCalculator)
        };
        
        var options = CreateAnalysisOptions();
        var rawSessions = new List<AndroidAdbAnalyze.Analysis.Models.Sessions.CameraSession>();
        
        foreach (var source in sessionSources)
        {
            var sourceSessions = source.ExtractSessions(parsedEvents, options);
            _output.WriteLine($"  {source.SourceName}: {sourceSessions.Count}개 세션");
            
            // 각 세션의 상세 정보 출력
            foreach (var session in sourceSessions)
            {
                _output.WriteLine($"    - {session.PackageName}: {session.StartTime:HH:mm:ss} ~ {session.EndTime:HH:mm:ss}");
            }
            
            rawSessions.AddRange(sourceSessions);
        }
        
        _output.WriteLine($"  총 원본 세션: {rawSessions.Count}개 (병합 전)\n");
        
        return rawSessions;
    }

    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync(
        string samplePath,
        DateTime startTime,
        DateTime endTime)
    {
        var allEvents = new List<NormalizedLogEvent>();

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

    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string logFilePath,
        string configFileName,
        DateTime startTime,
        DateTime endTime)
    {
        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
            return new List<NormalizedLogEvent>();

        // YAML 설정 로드
        var configLoader = new Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
        var configuration = configLoader.Load(configPath);

        // DeviceInfo 생성
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = startTime,
            Model = "Samsung Galaxy S24",
            AndroidVersion = "15"
        };

        // Parser 생성
        var parser = new AdbLogParser(configuration, NullLogger<AdbLogParser>.Instance);

        // 파싱 옵션 설정
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = false,
            StartTime = startTime,
            EndTime = endTime
        };

        // 파싱 실행
        var result = await parser.ParseAsync(logFilePath, options);

        return result.Success ? result.Events.ToList() : new List<NormalizedLogEvent>();
    }

    /// <summary>
    /// Orchestrator 생성
    /// </summary>
    private IAnalysisOrchestrator CreateOrchestrator()
    {
        var services = new ServiceCollection();
        
        // YAML 설정 로드
        var configPath = Path.Combine(_parserConfigPath, "..", "AndroidAdbAnalyze.Analysis", "Configs", "artifact-detection-config.example.yaml");
        var artifactConfig = AndroidAdbAnalyze.Analysis.Configuration.YamlConfigurationLoader.LoadFromFile(configPath);
        
        // 설정 등록
        services.AddSingleton(artifactConfig);
        services.AddSingleton(CreateAnalysisOptions());
        
        // 서비스 등록
        RegisterServices(services);
        
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    private void RegisterServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        
        // Session Context Provider
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies
        services.AddSingleton<ICaptureDetectionStrategy, TelegramStrategy>();
        services.AddSingleton<ICaptureDetectionStrategy, KakaoTalkStrategy>();
        services.AddSingleton<ICaptureDetectionStrategy, BasePatternStrategy>();
        
        // Capture Detector
        services.AddSingleton<ICaptureDetector, CameraCaptureDetector>();
        
        // Confidence Calculator
        services.AddSingleton<IConfidenceCalculator, ConfidenceCalculator>();
        
        // Session Sources
        services.AddSingleton<ISessionSource, UsagestatsSessionSource>();
        services.AddSingleton<ISessionSource, MediaCameraSessionSource>();
        
        // Session Detector
        services.AddSingleton<ISessionDetector, CameraSessionDetector>();
        
        // Deduplication Services
        services.AddSingleton<IEventDeduplicator, EventDeduplicator>();
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        // Transmission Detection Services
        services.AddSingleton<ITransmissionDetector, WifiTransmissionDetector>();
        
        // Reporting Services
        services.AddSingleton<IReportGenerator, HtmlReportGenerator>();
        services.AddSingleton<ITimelineBuilder, TimelineBuilder>();
        
        // Orchestration
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }

    /// <summary>
    /// 분석 옵션 생성
    /// </summary>
    private AnalysisOptions CreateAnalysisOptions()
    {
        // AnalysisOptions 기본값 사용 (하드코딩 금지)
        return new AnalysisOptions();
    }

    #endregion
}

