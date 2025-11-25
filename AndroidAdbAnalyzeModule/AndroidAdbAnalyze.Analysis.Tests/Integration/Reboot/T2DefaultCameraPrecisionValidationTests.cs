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
/// T2 재부팅 환경에서 기본 카메라의 Precision 60.0% 검증 테스트
/// 목적: 표 25의 수치 정확성 검증
/// </summary>
public sealed class T2DefaultCameraPrecisionValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    
    // Ground Truth: Sample 1, 4, 9의 기본 카메라 세션
    private readonly List<GroundTruthSession> _groundTruthSessions = new()
    {
        // Sample 1
        new GroundTruthSession { SampleId = 1, SessionId = "S1-1", App = "기본 카메라", IsCapture = false, StartTime = new DateTime(2025, 10, 4, 14, 49, 23), EndTime = new DateTime(2025, 10, 4, 14, 49, 27) },
        new GroundTruthSession { SampleId = 1, SessionId = "S1-2", App = "기본 카메라", IsCapture = true, CaptureTime = new DateTime(2025, 10, 4, 14, 49, 54), StartTime = new DateTime(2025, 10, 4, 14, 49, 49), EndTime = new DateTime(2025, 10, 4, 14, 49, 59) },
        
        // Sample 4
        new GroundTruthSession { SampleId = 4, SessionId = "S4-1", App = "기본 카메라", IsCapture = false, StartTime = new DateTime(2025, 10, 12, 16, 7, 0), EndTime = new DateTime(2025, 10, 12, 16, 7, 5) },
        new GroundTruthSession { SampleId = 4, SessionId = "S4-2", App = "기본 카메라", IsCapture = false, StartTime = new DateTime(2025, 10, 12, 16, 7, 47), EndTime = new DateTime(2025, 10, 12, 16, 7, 53) },
        new GroundTruthSession { SampleId = 4, SessionId = "S4-3", App = "기본 카메라", IsCapture = true, CaptureTime = new DateTime(2025, 10, 12, 16, 8, 42), StartTime = new DateTime(2025, 10, 12, 16, 8, 36), EndTime = new DateTime(2025, 10, 12, 16, 8, 47) },
        
        // Sample 9
        new GroundTruthSession { SampleId = 9, SessionId = "S9-1", App = "기본 카메라", IsCapture = false, StartTime = new DateTime(2025, 10, 17, 16, 40, 58), EndTime = new DateTime(2025, 10, 17, 16, 41, 4) },
        new GroundTruthSession { SampleId = 9, SessionId = "S9-2", App = "기본 카메라", IsCapture = true, CaptureTime = new DateTime(2025, 10, 17, 16, 42, 26), StartTime = new DateTime(2025, 10, 17, 16, 42, 20), EndTime = new DateTime(2025, 10, 17, 16, 42, 31) }
    };
    
    private class GroundTruthSession
    {
        public int SampleId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string App { get; set; } = string.Empty;
        public bool IsCapture { get; set; }
        public DateTime? CaptureTime { get; set; }
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

    public T2DefaultCameraPrecisionValidationTests(ITestOutputHelper output)
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
    public async Task Should_Validate_T2_DefaultCamera_Precision_60Percent()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== T2 재부팅 환경 기본 카메라 Precision 60.0% 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        var allDetectedSessions = new List<CameraSession>();
        var allDetectedCaptures = new List<CameraCaptureEvent>();

        // Sample 1, 4, 9 각각 분석
        foreach (var sampleId in new[] { 1, 4, 9 })
        {
            _output.WriteLine($"📊 Sample {sampleId} 분석 중...");
            
            var timeRange = _sampleTimeRanges[sampleId];
            var parsedEvents = await ParseRebootLogsAsync(sampleId, timeRange.Start, timeRange.End);
            
            var options = new AnalysisOptions
            {
                DeduplicationSimilarityThreshold = 0.8
            };
            
            var result = await _orchestrator!.AnalyzeAsync(parsedEvents, options);
            
            // 기본 카메라 세션 및 촬영만 필터링
            var defaultCameraSessions = result.Sessions
                .Where(s => s.PackageName == "com.sec.android.app.camera")
                .ToList();
            
            var defaultCameraCaptures = result.CaptureEvents
                .Where(c => c.PackageName == "com.sec.android.app.camera")
                .ToList();
            
            allDetectedSessions.AddRange(defaultCameraSessions);
            allDetectedCaptures.AddRange(defaultCameraCaptures);
            
            _output.WriteLine($"   - 세션: {defaultCameraSessions.Count}개");
            _output.WriteLine($"   - 촬영: {defaultCameraCaptures.Count}개\n");
        }

        // Ground Truth와 비교하여 TP, FP, FN, TN 계산
        var gtDefaultCameraSessions = _groundTruthSessions
            .Where(s => s.App == "기본 카메라")
            .ToList();
        
        var gtCaptures = gtDefaultCameraSessions
            .Where(s => s.IsCapture)
            .ToList();
        
        var gtNonCaptures = gtDefaultCameraSessions
            .Where(s => !s.IsCapture)
            .ToList();

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📋 Ground Truth (기본 카메라)");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"촬영 세션: {gtCaptures.Count}개");
        foreach (var gt in gtCaptures)
        {
            _output.WriteLine($"  - {gt.SessionId}: {gt.CaptureTime:HH:mm:ss}");
        }
        _output.WriteLine($"\n사용 세션: {gtNonCaptures.Count}개");
        foreach (var gt in gtNonCaptures)
        {
            _output.WriteLine($"  - {gt.SessionId}: {gt.StartTime:HH:mm:ss}~{gt.EndTime:HH:mm:ss}");
        }

        _output.WriteLine("\n════════════════════════════════════════════════════════════");
        _output.WriteLine("📋 탐지 결과 (기본 카메라)");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"촬영 탐지: {allDetectedCaptures.Count}개");
        foreach (var capture in allDetectedCaptures.OrderBy(c => c.CaptureTime))
        {
            _output.WriteLine($"  - {capture.CaptureTime:HH:mm:ss} (세션: {capture.ParentSessionId})");
        }

        // TP, FP, FN, TN 계산
        var tp = 0; // 촬영을 촬영으로 정확히 탐지
        var fp = 0; // 사용을 촬영으로 오탐
        var fn = 0; // 촬영을 사용으로 오탐 (미탐지)
        var tn = 0; // 사용을 사용으로 정확히 분류

        // TP 계산: Ground Truth 촬영이 탐지되었는지 확인
        var matchedGtCaptures = new HashSet<GroundTruthSession>();
        var matchedDetectedCaptures = new HashSet<CameraCaptureEvent>();
        
        foreach (var gtCapture in gtCaptures)
        {
            var matched = allDetectedCaptures
                .Where(c => !matchedDetectedCaptures.Contains(c))
                .FirstOrDefault(c => 
                    Math.Abs((c.CaptureTime - gtCapture.CaptureTime!.Value).TotalSeconds) < 5.0);
            
            if (matched != null)
            {
                tp++;
                matchedGtCaptures.Add(gtCapture);
                matchedDetectedCaptures.Add(matched);
            }
            else
            {
                fn++;
            }
        }

        // FP 계산: 사용 세션 시간 범위 내에서 탐지된 촬영 (논문 표 25의 정의에 따라)
        // FP = 사용을 촬영으로 오분류
        foreach (var gtNonCapture in gtNonCaptures)
        {
            var falseDetected = allDetectedCaptures
                .Where(c => !matchedDetectedCaptures.Contains(c))
                .Any(c =>
                    c.CaptureTime >= gtNonCapture.StartTime.AddSeconds(-5) &&
                    c.CaptureTime <= gtNonCapture.EndTime.AddSeconds(5));
            
            if (falseDetected)
            {
                fp++;
            }
        }

        // 추가 FP: Ground Truth 촬영과 매칭되지 않고, 사용 세션 시간 범위에도 없는 탐지된 촬영
        foreach (var detectedCapture in allDetectedCaptures)
        {
            if (matchedDetectedCaptures.Contains(detectedCapture))
                continue;
            
            // 사용 세션 시간 범위에 있는지 확인
            var inNonCaptureSession = gtNonCaptures.Any(gt =>
                detectedCapture.CaptureTime >= gt.StartTime.AddSeconds(-5) &&
                detectedCapture.CaptureTime <= gt.EndTime.AddSeconds(5));
            
            if (!inNonCaptureSession)
            {
                // Ground Truth 촬영과도 매칭되지 않고, 사용 세션 범위에도 없으면 FP
                fp++;
            }
        }

        // TN 계산: 사용 세션이 촬영으로 탐지되지 않았는지 확인
        foreach (var gtNonCapture in gtNonCaptures)
        {
            var falseDetected = allDetectedCaptures.Any(c =>
                c.CaptureTime >= gtNonCapture.StartTime.AddSeconds(-5) &&
                c.CaptureTime <= gtNonCapture.EndTime.AddSeconds(5));
            
            if (!falseDetected)
            {
                tn++;
            }
        }

        // Precision, Recall, F1 Score 계산
        var precision = (tp + fp) > 0 ? (double)tp / (tp + fp) * 100 : 0;
        var recall = (tp + fn) > 0 ? (double)tp / (tp + fn) * 100 : 0;
        var f1Score = (tp + fp + fn) > 0 ? 2.0 * tp / (2 * tp + fp + fn) * 100 : 0;

        _output.WriteLine("\n════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 성능 지표 계산 결과");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"TP (True Positive): {tp}개");
        _output.WriteLine($"FP (False Positive): {fp}개");
        _output.WriteLine($"FN (False Negative): {fn}개");
        _output.WriteLine($"TN (True Negative): {tn}개");
        _output.WriteLine($"\nPrecision = TP/(TP+FP) = {tp}/({tp}+{fp}) = {precision:F1}%");
        _output.WriteLine($"Recall = TP/(TP+FN) = {tp}/({tp}+{fn}) = {recall:F1}%");
        _output.WriteLine($"F1 Score = 2×TP/(2×TP+FP+FN) = 2×{tp}/(2×{tp}+{fp}+{fn}) = {f1Score:F1}%");

        _output.WriteLine("\n════════════════════════════════════════════════════════════");
        _output.WriteLine("✅ 검증 결과");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        
        // 논문 표 25의 수치와 비교
        var expectedTp = 3;
        var expectedFp = 2;
        var expectedFn = 0;
        var expectedTn = 4;
        var expectedPrecision = 60.0;
        var expectedRecall = 100.0;
        var expectedF1Score = 75.0;

        _output.WriteLine($"논문 표 25 수치:");
        _output.WriteLine($"  TP={expectedTp}, FP={expectedFp}, FN={expectedFn}, TN={expectedTn}");
        _output.WriteLine($"  Precision={expectedPrecision}%, Recall={expectedRecall}%, F1={expectedF1Score}%");
        
        _output.WriteLine($"\n실제 계산 결과:");
        _output.WriteLine($"  TP={tp}, FP={fp}, FN={fn}, TN={tn}");
        _output.WriteLine($"  Precision={precision:F1}%, Recall={recall:F1}%, F1={f1Score:F1}%");

        // 검증
        tp.Should().Be(expectedTp, "TP는 3개여야 함");
        fp.Should().Be(expectedFp, "FP는 2개여야 함");
        fn.Should().Be(expectedFn, "FN은 0개여야 함");
        tn.Should().Be(expectedTn, "TN은 4개여야 함");
        precision.Should().BeApproximately(expectedPrecision, 0.1, "Precision은 60.0%여야 함");
        recall.Should().BeApproximately(expectedRecall, 0.1, "Recall은 100%여야 함");
        f1Score.Should().BeApproximately(expectedF1Score, 0.1, "F1 Score는 75.0%여야 함");

        _output.WriteLine("\n✅ 모든 수치가 논문 표 25와 일치합니다!");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
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
        
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });
        
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(NullLoggerProvider.Instance));
        var logger = loggerFactory.CreateLogger<T2DefaultCameraPrecisionValidationTests>();
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

    private async Task<List<NormalizedLogEvent>> ParseRebootLogsAsync(int sampleId, DateTime startTime, DateTime endTime)
    {
        var rebootPath = Path.Combine(_sampleLogsPath, _rebootSampleDirectories[sampleId]);
        var allEvents = new List<NormalizedLogEvent>();

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

            var events = await ParseLogFileAsync(logPath, configFile, startTime, endTime);
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
}

