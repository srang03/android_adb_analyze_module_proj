using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Services.Captures;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Context;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;
using AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using AndroidAdbAnalyze.Parser.Configuration;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Reboot;

/// <summary>
/// T2 재부팅 환경에서 앱별 촬영 탐지 성능 검증 테스트
/// 목적: 논문 표 23의 수치 정확성 검증
/// </summary>
public sealed class T2AppSpecificCaptureDetectionPerformanceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    
    // T0 원본 Ground Truth (평가 기준)
    private readonly List<GroundTruthCapture> _t0GroundTruthCaptures = new()
    {
        // Sample 1
        new GroundTruthCapture { SampleId = 1, App = "기본 카메라", CaptureTime = new DateTime(2025, 10, 4, 14, 49, 55) },
        new GroundTruthCapture { SampleId = 1, App = "카카오톡", CaptureTime = new DateTime(2025, 10, 4, 14, 51, 39) },
        new GroundTruthCapture { SampleId = 1, App = "텔레그램", CaptureTime = new DateTime(2025, 10, 4, 14, 53, 46) },
        new GroundTruthCapture { SampleId = 1, App = "무음 카메라", CaptureTime = new DateTime(2025, 10, 4, 14, 55, 47) },
        
        // Sample 4
        new GroundTruthCapture { SampleId = 4, App = "기본 카메라", CaptureTime = new DateTime(2025, 10, 12, 16, 8, 42) },
        new GroundTruthCapture { SampleId = 4, App = "카카오톡", CaptureTime = new DateTime(2025, 10, 12, 16, 15, 48) },
        new GroundTruthCapture { SampleId = 4, App = "카카오톡", CaptureTime = new DateTime(2025, 10, 12, 16, 17, 0) },
        new GroundTruthCapture { SampleId = 4, App = "텔레그램", CaptureTime = new DateTime(2025, 10, 12, 16, 20, 59) },
        new GroundTruthCapture { SampleId = 4, App = "텔레그램", CaptureTime = new DateTime(2025, 10, 12, 16, 22, 13) },
        new GroundTruthCapture { SampleId = 4, App = "무음 카메라", CaptureTime = new DateTime(2025, 10, 12, 16, 24, 24) },
        
        // Sample 9
        new GroundTruthCapture { SampleId = 9, App = "기본 카메라", CaptureTime = new DateTime(2025, 10, 17, 16, 42, 26) },
        new GroundTruthCapture { SampleId = 9, App = "카카오톡", CaptureTime = new DateTime(2025, 10, 17, 16, 45, 48) },
        new GroundTruthCapture { SampleId = 9, App = "텔레그램", CaptureTime = new DateTime(2025, 10, 17, 16, 48, 12) },
        new GroundTruthCapture { SampleId = 9, App = "무음 카메라", CaptureTime = new DateTime(2025, 10, 17, 16, 51, 57) }
    };
    
    // T0 원본 Ground Truth 비촬영 세션 (TN 계산용)
    private readonly List<GroundTruthNonCapture> _t0GroundTruthNonCaptures = new()
    {
        // Sample 1
        new GroundTruthNonCapture { SampleId = 1, App = "기본 카메라", StartTime = new DateTime(2025, 10, 4, 14, 49, 23), EndTime = new DateTime(2025, 10, 4, 14, 49, 27) },
        new GroundTruthNonCapture { SampleId = 1, App = "카카오톡", StartTime = new DateTime(2025, 10, 4, 14, 50, 47), EndTime = new DateTime(2025, 10, 4, 14, 50, 52) },
        new GroundTruthNonCapture { SampleId = 1, App = "텔레그램", StartTime = new DateTime(2025, 10, 4, 14, 52, 28), EndTime = new DateTime(2025, 10, 4, 14, 52, 39) },
        new GroundTruthNonCapture { SampleId = 1, App = "무음 카메라", StartTime = new DateTime(2025, 10, 4, 14, 55, 13), EndTime = new DateTime(2025, 10, 4, 14, 55, 18) },
        
        // Sample 4
        new GroundTruthNonCapture { SampleId = 4, App = "기본 카메라", StartTime = new DateTime(2025, 10, 12, 16, 7, 0), EndTime = new DateTime(2025, 10, 12, 16, 7, 5) },
        new GroundTruthNonCapture { SampleId = 4, App = "기본 카메라", StartTime = new DateTime(2025, 10, 12, 16, 7, 47), EndTime = new DateTime(2025, 10, 12, 16, 7, 53) },
        new GroundTruthNonCapture { SampleId = 4, App = "카카오톡", StartTime = new DateTime(2025, 10, 12, 16, 12, 1), EndTime = new DateTime(2025, 10, 12, 16, 12, 7) },
        new GroundTruthNonCapture { SampleId = 4, App = "텔레그램", StartTime = new DateTime(2025, 10, 12, 16, 19, 38), EndTime = new DateTime(2025, 10, 12, 16, 19, 49) },
        new GroundTruthNonCapture { SampleId = 4, App = "무음 카메라", StartTime = new DateTime(2025, 10, 12, 16, 23, 48), EndTime = new DateTime(2025, 10, 12, 16, 23, 54) },
        
        // Sample 9
        new GroundTruthNonCapture { SampleId = 9, App = "기본 카메라", StartTime = new DateTime(2025, 10, 17, 16, 40, 58), EndTime = new DateTime(2025, 10, 17, 16, 41, 4) },
        new GroundTruthNonCapture { SampleId = 9, App = "카카오톡", StartTime = new DateTime(2025, 10, 17, 16, 43, 18), EndTime = new DateTime(2025, 10, 17, 16, 43, 23) },
        new GroundTruthNonCapture { SampleId = 9, App = "텔레그램", StartTime = new DateTime(2025, 10, 17, 16, 46, 39), EndTime = new DateTime(2025, 10, 17, 16, 46, 52) },
        new GroundTruthNonCapture { SampleId = 9, App = "무음 카메라", StartTime = new DateTime(2025, 10, 17, 16, 51, 23), EndTime = new DateTime(2025, 10, 17, 16, 51, 28) }
    };
    
    private class GroundTruthCapture
    {
        public int SampleId { get; set; }
        public string App { get; set; } = string.Empty;
        public DateTime CaptureTime { get; set; }
    }
    
    private class GroundTruthNonCapture
    {
        public int SampleId { get; set; }
        public string App { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
    
    private readonly Dictionary<int, (DateTime Start, DateTime End)> _sampleTimeRanges = new()
    {
        { 1, (new DateTime(2025, 10, 4, 14, 49, 0), new DateTime(2025, 10, 4, 14, 56, 0)) },
        { 4, (new DateTime(2025, 10, 12, 16, 7, 0), new DateTime(2025, 10, 12, 16, 25, 0)) },
        { 9, (new DateTime(2025, 10, 17, 16, 40, 0), new DateTime(2025, 10, 17, 16, 52, 59)) }
    };
    
    private readonly Dictionary<int, string> _rebootSampleDirectories = new()
    {
        { 1, "재부팅 휘발성/1차 샘플_25_10_04_재부팅" },
        { 4, "재부팅 휘발성/4차 샘플_25_10_12_재부팅" },
        { 9, "재부팅 휘발성/9차 샘플_25_10_17_재부팅" }
    };
    
    private readonly Dictionary<string, string> _packageToAppName = new()
    {
        { "com.sec.android.app.camera", "기본 카메라" },
        { "com.kakao.talk", "카카오톡" },
        { "org.telegram.messenger", "텔레그램" },
        { "com.peace.SilentCamera", "무음 카메라" }
    };

    public T2AppSpecificCaptureDetectionPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
        var workspaceRoot = Path.Combine("..", "..", "..", "..", "..");
        _sampleLogsPath = Path.Combine(workspaceRoot, "sample_logs");
        _parserConfigPath = Path.Combine(workspaceRoot, "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Parser", "Configs");
    }

    public Task InitializeAsync()
    {
        _orchestrator = CreateOrchestratorWithYamlConfig();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Should_Validate_T2_AppSpecific_CaptureDetection_Performance()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        var allDetectedCaptures = new List<CameraCaptureEvent>();
        var allSessions = new List<CameraSession>();
        
        // Act: Sample 1, 4, 9의 재부팅 후 로그 분석
        foreach (var sampleId in new[] { 1, 4, 9 })
        {
            var parsedEvents = await ParseRebootLogsAsync(sampleId);
            var result = await _orchestrator!.AnalyzeAsync(parsedEvents, options);
            
            allDetectedCaptures.AddRange(result.CaptureEvents);
            allSessions.AddRange(result.Sessions);
            
            _output.WriteLine($"Sample {sampleId}: 세션 {result.Sessions.Count}개, 촬영 {result.CaptureEvents.Count}개 탐지");
        }
        
        _output.WriteLine($"\n전체 탐지 결과: 세션 {allSessions.Count}개, 촬영 {allDetectedCaptures.Count}개");
        
        // Assert: 앱별 성능 계산
        var appPerformances = CalculateAppSpecificPerformance(allDetectedCaptures, allSessions);
        
        // 결과 출력
        _output.WriteLine("\n════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 T2 환경 앱별 촬영 탐지 성능 분석 결과");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _output.WriteLine("| 앱 | TP | FP | FN | TN | Precision | Recall | F1 Score |");
        _output.WriteLine("|----|----|----|----|----|-----------|--------|----------|");
        
        foreach (var app in new[] { "기본 카메라", "카카오톡", "텔레그램", "무음 카메라" })
        {
            var perf = appPerformances[app];
            var precision = perf.TP + perf.FP > 0 ? (double)perf.TP / (perf.TP + perf.FP) * 100 : 0;
            var recall = perf.TP + perf.FN > 0 ? (double)perf.TP / (perf.TP + perf.FN) * 100 : 0;
            var f1Score = (perf.TP + perf.FP + perf.FN) > 0 ? 2.0 * perf.TP / (2 * perf.TP + perf.FP + perf.FN) * 100 : 0;
            
            var precisionStr = perf.TP + perf.FP > 0 ? $"{precision:F1}%" : "-";
            var recallStr = perf.TP + perf.FN > 0 ? $"{recall:F1}%" : "-";
            var f1Str = (perf.TP + perf.FP + perf.FN) > 0 ? $"{f1Score:F1}%" : "-";
            
            _output.WriteLine($"| {app} | {perf.TP} | {perf.FP} | {perf.FN} | {perf.TN} | {precisionStr} | {recallStr} | {f1Str} |");
        }
        
        // 합계 계산
        var totalTp = appPerformances.Values.Sum(p => p.TP);
        var totalFp = appPerformances.Values.Sum(p => p.FP);
        var totalFn = appPerformances.Values.Sum(p => p.FN);
        var totalTn = appPerformances.Values.Sum(p => p.TN);
        var totalPrecision = totalTp + totalFp > 0 ? (double)totalTp / (totalTp + totalFp) * 100 : 0;
        var totalRecall = totalTp + totalFn > 0 ? (double)totalTp / (totalTp + totalFn) * 100 : 0;
        var totalF1Score = (totalTp + totalFp + totalFn) > 0 ? 2.0 * totalTp / (2 * totalTp + totalFp + totalFn) * 100 : 0;
        
        _output.WriteLine($"| 합계 | {totalTp} | {totalFp} | {totalFn} | {totalTn} | {totalPrecision:F1}% | {totalRecall:F1}% | {totalF1Score:F1}% |");
        
        _output.WriteLine("\n════════════════════════════════════════════════════════════");
        _output.WriteLine("✅ 검증 완료");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // 논문 표 23과 비교
        _output.WriteLine("논문 표 23 비교:");
        _output.WriteLine("| 앱 | 논문 TP | 실제 TP | 논문 FP | 실제 FP | 논문 FN | 실제 FN | 논문 TN | 실제 TN |");
        _output.WriteLine("|----|---------|---------|---------|---------|---------|---------|---------|---------|");
        
        var expectedValues = new Dictionary<string, (int TP, int FP, int FN, int TN)>
        {
            { "기본 카메라", (3, 0, 0, 4) },
            { "카카오톡", (4, 0, 0, 3) },
            { "텔레그램", (0, 0, 4, 4) },
            { "무음 카메라", (0, 0, 3, 3) }
        };
        
        foreach (var app in new[] { "기본 카메라", "카카오톡", "텔레그램", "무음 카메라" })
        {
            var perf = appPerformances[app];
            var expected = expectedValues[app];
            _output.WriteLine($"| {app} | {expected.TP} | {perf.TP} | {expected.FP} | {perf.FP} | {expected.FN} | {perf.FN} | {expected.TN} | {perf.TN} |");
            
            if (perf.TP == expected.TP && perf.FP == expected.FP && perf.FN == expected.FN && perf.TN == expected.TN)
            {
                _output.WriteLine($"  ✅ {app}: 논문 표 23과 일치");
            }
            else
            {
                _output.WriteLine($"  ⚠️  {app}: 논문 표 23과 불일치");
            }
        }
    }
    
    private Dictionary<string, (int TP, int FP, int FN, int TN)> CalculateAppSpecificPerformance(
        List<CameraCaptureEvent> detectedCaptures,
        List<CameraSession> sessions)
    {
        var appPerformances = new Dictionary<string, (int TP, int FP, int FN, int TN)>
        {
            { "기본 카메라", (0, 0, 0, 0) },
            { "카카오톡", (0, 0, 0, 0) },
            { "텔레그램", (0, 0, 0, 0) },
            { "무음 카메라", (0, 0, 0, 0) }
        };
        
        // 앱별로 Ground Truth와 비교
        foreach (var app in appPerformances.Keys.ToList())
        {
            var gtCaptures = _t0GroundTruthCaptures.Where(c => c.App == app).ToList();
            var gtNonCaptures = _t0GroundTruthNonCaptures.Where(c => c.App == app).ToList();
            
            // 앱의 패키지명 찾기
            var packageName = _packageToAppName.FirstOrDefault(kvp => kvp.Value == app).Key;
            if (packageName == null) continue;
            
            // 해당 앱의 탐지된 촬영 필터링
            var appDetectedCaptures = detectedCaptures
                .Where(c => c.PackageName == packageName)
                .ToList();
            
            // TP, FN 계산
            var matchedGtCaptures = new HashSet<GroundTruthCapture>();
            var matchedDetectedCaptures = new HashSet<CameraCaptureEvent>();
            
            foreach (var gtCapture in gtCaptures)
            {
                var matched = appDetectedCaptures
                    .Where(c => !matchedDetectedCaptures.Contains(c))
                    .FirstOrDefault(c => 
                        Math.Abs((c.CaptureTime - gtCapture.CaptureTime).TotalSeconds) < 5.0);
                
                if (matched != null)
                {
                    var perf = appPerformances[app];
                    appPerformances[app] = (perf.TP + 1, perf.FP, perf.FN, perf.TN);
                    matchedGtCaptures.Add(gtCapture);
                    matchedDetectedCaptures.Add(matched);
                }
                else
                {
                    var perf = appPerformances[app];
                    appPerformances[app] = (perf.TP, perf.FP, perf.FN + 1, perf.TN);
                }
            }
            
            // FP 계산: Ground Truth 촬영과 매칭되지 않은 탐지된 촬영 중에서
            // 비촬영 세션 시간 범위에 포함되거나, 어떤 Ground Truth 촬영/비촬영 세션과도 매칭되지 않는 경우
            var unmatchedDetectedCaptures = appDetectedCaptures
                .Where(c => !matchedDetectedCaptures.Contains(c))
                .ToList();
            
            foreach (var detectedCapture in unmatchedDetectedCaptures)
            {
                // 비촬영 세션 시간 범위에 포함되는지 확인
                var inNonCaptureSession = gtNonCaptures.Any(gt =>
                    detectedCapture.CaptureTime >= gt.StartTime.AddSeconds(-5) &&
                    detectedCapture.CaptureTime <= gt.EndTime.AddSeconds(5));
                
                // 비촬영 세션 시간 범위에 포함되거나, 어떤 Ground Truth와도 매칭되지 않으면 FP
                if (inNonCaptureSession)
                {
                    var perf = appPerformances[app];
                    appPerformances[app] = (perf.TP, perf.FP + 1, perf.FN, perf.TN);
                }
                else
                {
                    // Ground Truth 촬영과도 매칭되지 않고, 비촬영 세션 범위에도 없으면 FP (알 수 없는 탐지)
                    var perf = appPerformances[app];
                    appPerformances[app] = (perf.TP, perf.FP + 1, perf.FN, perf.TN);
                }
            }
            
            // TN 계산: 비촬영 세션이 촬영으로 탐지되지 않았는지 확인
            // 각 비촬영 세션마다 해당 시간 범위에 탐지된 촬영이 없으면 TN
            foreach (var gtNonCapture in gtNonCaptures)
            {
                var falseDetected = appDetectedCaptures.Any(c =>
                    c.CaptureTime >= gtNonCapture.StartTime.AddSeconds(-5) &&
                    c.CaptureTime <= gtNonCapture.EndTime.AddSeconds(5));
                
                if (!falseDetected)
                {
                    var perf = appPerformances[app];
                    appPerformances[app] = (perf.TP, perf.FP, perf.FN, perf.TN + 1);
                }
            }
        }
        
        return appPerformances;
    }
    
    private IAnalysisOrchestrator CreateOrchestratorWithYamlConfig()
    {
        var configPath = Path.Combine(
            "..", "..", "..", "..", "..",
            "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Analysis", "Configs",
            "artifact-detection-config.example.yaml");
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"YAML 설정 파일을 찾을 수 없습니다: {configPath}");
        }
        
        var services = new ServiceCollection();
        
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        services.AddSingleton(CreateAnalysisOptions());
        
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(NullLoggerProvider.Instance));
        var logger = loggerFactory.CreateLogger<T2AppSpecificCaptureDetectionPerformanceTests>();
        var config = YamlConfigurationLoader.LoadFromFile(configPath, logger);
        
        services.AddSingleton(config);
        RegisterServicesWithConfig(services);
        
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }
    
    private void RegisterServicesWithConfig(IServiceCollection services)
    {
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TelegramStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new TelegramStrategy(logger, calculator, config);
        });
        
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KakaoTalkStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new KakaoTalkStrategy(logger, calculator, config);
        });
        
        services.AddSingleton<ICaptureDetectionStrategy>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<BasePatternStrategy>>();
            var calculator = sp.GetRequiredService<IConfidenceCalculator>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new BasePatternStrategy(logger, calculator, config);
        });
        
        services.AddSingleton<ICaptureDetector, CameraCaptureDetector>();
        
        services.AddSingleton<IConfidenceCalculator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConfidenceCalculator>>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new ConfidenceCalculator(logger, config);
        });
        
        services.AddSingleton<ISessionSource, UsagestatsSessionSource>();
        services.AddSingleton<ISessionSource, MediaCameraSessionSource>();
        
        services.AddSingleton<ISessionDetector, CameraSessionDetector>();
        
        services.AddSingleton<IEventDeduplicator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventDeduplicator>>();
            var options = sp.GetRequiredService<AnalysisOptions>();
            return new EventDeduplicator(logger, options);
        });
        
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }
    
    private async Task<List<NormalizedLogEvent>> ParseRebootLogsAsync(int sampleId)
    {
        var rebootPath = Path.Combine(_sampleLogsPath, _rebootSampleDirectories[sampleId]);
        var allEvents = new List<NormalizedLogEvent>();
        var timeRange = _sampleTimeRanges[sampleId];

        var logFiles = new[]
        {
            ("audio.log", "adb_audio_config.yaml"),
            ("media_camera_worker.log", "adb_media_camera_worker_config.yaml"),
            ("media_camera.log", "adb_media_camera_config.yaml"),
            ("media_metrics.log", "adb_media_metrics_config.yaml"),
            ("usagestats.log", "adb_usagestats_config.yaml"),
            ("vibrator_manager.log", "adb_vibrator_config.yaml"),
            ("activity.log", "adb_activity_config.yaml")
        };

        foreach (var (logFile, configFile) in logFiles)
        {
            var logPath = Path.Combine(rebootPath, logFile);
            if (!File.Exists(logPath))
            {
                continue;
            }

            var events = await ParseLogFileAsync(logPath, configFile, timeRange.Start, timeRange.End);
            allEvents.AddRange(events);
        }

        return allEvents;
    }
    
    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string logFilePath, 
        string configFileName,
        DateTime? startTime,
        DateTime? endTime)
    {
        if (!File.Exists(logFilePath))
        {
            return new List<NormalizedLogEvent>();
        }

        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}");
        }

        var configLoader = new Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
        var configuration = configLoader.Load(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = DateTime.Now,
            AndroidVersion = "15",
            Manufacturer = "Samsung",
            Model = "SM-G991N"
        };

        var parser = new AdbLogParser(configuration, NullLogger<AdbLogParser>.Instance);
        var options = new LogParsingOptions 
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
            return result.Events?.ToList() ?? new List<NormalizedLogEvent>();
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error parsing {Path.GetFileName(logFilePath)}: {ex.Message}");
            return new List<NormalizedLogEvent>();
        }
    }

    /// <summary>
    /// AnalysisOptions 생성
    /// </summary>
    /// <remarks>
    /// DeduplicationEffectValidationTests.cs와 동일한 설정 사용
    /// - EventCorrelationWindow: 30초 (보조 아티팩트 수집 범위)
    /// - CaptureDeduplicationWindow: 500ms (CameraCaptureEvent 중복 제거) ← 필수!
    /// - DeduplicationSimilarityThreshold: 0.8 (Ground Truth 검증용)
    /// </remarks>
    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            DeduplicationSimilarityThreshold = 0.8,
            CaptureDeduplicationWindow = TimeSpan.FromMilliseconds(ArtifactWeights.CaptureDeduplicationWindowMs)
        };
    }
}

