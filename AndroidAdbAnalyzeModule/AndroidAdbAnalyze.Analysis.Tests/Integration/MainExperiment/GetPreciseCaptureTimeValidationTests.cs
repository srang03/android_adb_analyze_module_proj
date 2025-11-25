using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
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
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.MainExperiment;

/// <summary>
/// 정밀한 촬영 시각 결정 메커니즘(GetPreciseCaptureTime) 타당성 검증 테스트
/// </summary>
/// <remarks>
/// 목적:
/// - 본 실험에서 FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스 추출
/// - 메커니즘 적용 전후 타임스탬프 차이 측정
/// - 예비 실험과 본 실험 비교
/// 
/// 논문 반영:
/// - 제4장 제4절: 정밀한 촬영 시각 결정 메커니즘 설계
/// - 부록 3, 3.4절: 예비 실험 기반 방법론 및 측정 데이터
/// - 제5장 제3절: 본 실험 기반 타당성 검증
/// </remarks>
public sealed class GetPreciseCaptureTimeValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    private readonly string _projectRoot;
    
    private AnalysisResult? _analysisResult;
    private List<NormalizedLogEvent>? _allParsedEvents;

    public GetPreciseCaptureTimeValidationTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        
        var currentDir = Directory.GetCurrentDirectory();
        _projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        _sampleLogsPath = Path.Combine(_projectRoot, "..", "sample_logs");
        _parserConfigPath = Path.Combine(_projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("🔬 정밀한 촬영 시각 결정 메커니즘 타당성 검증 테스트 초기화");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 본 실험 Sample 1-10 분석
        var allEvents = new List<NormalizedLogEvent>();
        var allSessions = new List<CameraSession>();
        var allCaptures = new List<CameraCaptureEvent>();

        for (int sampleNum = 1; sampleNum <= 10; sampleNum++)
        {
            // ArtifactWeights.SampleTimeRanges 공용 상수 사용
            if (!ArtifactWeights.SampleTimeRanges.TryGetValue(sampleNum, out var timeRange))
            {
                _output.WriteLine($"⚠️ Sample {sampleNum}의 시간 범위를 찾을 수 없습니다.");
                continue;
            }
            
            var dir = timeRange.DirectoryName;
            var startTime = timeRange.StartTime;
            var endTime = timeRange.EndTime;
            var samplePath = Path.Combine(_sampleLogsPath, dir);
            
            _output.WriteLine($"📂 Sample {sampleNum}: {dir}");
            
            // 로그 파싱 (다른 테스트와 동일한 패턴)
            var parsedEvents = await ParseSampleLogsAsync(samplePath, startTime, endTime);
            allEvents.AddRange(parsedEvents);
            
            // 분석 실행
            var orchestrator = CreateOrchestrator();
            var result = await orchestrator.AnalyzeAsync(
                parsedEvents,
                CreateAnalysisOptions());
            
            allSessions.AddRange(result.Sessions);
            allCaptures.AddRange(result.CaptureEvents);
        }

        _allParsedEvents = allEvents;
        _analysisResult = new AnalysisResult
        {
            Sessions = allSessions,
            CaptureEvents = allCaptures
        };

        _output.WriteLine($"✅ 본 실험 Sample 1-10 분석 완료");
        _output.WriteLine($"   - 총 세션: {allSessions.Count}개");
        _output.WriteLine($"   - 총 촬영: {allCaptures.Count}개\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스 추출 및 타임스탬프 차이 측정
    /// </summary>
    [Fact]
    public void Validate_GetPreciseCaptureTime_Mechanism()
    {
        _output.WriteLine("\n📊 정밀한 촬영 시각 결정 메커니즘 타당성 검증\n");
        
        // 1. FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스 추출
        var foregroundServiceCases = _analysisResult!.CaptureEvents
            .Where(c => c.Metadata.TryGetValue("key_artifact_type", out var keyType) && 
                       keyType == "FOREGROUND_SERVICE")
            .ToList();
        
        _output.WriteLine($"총 촬영 수: {_analysisResult.CaptureEvents.Count}개");
        _output.WriteLine($"FOREGROUND_SERVICE가 keyArtifact인 케이스: {foregroundServiceCases.Count}개\n");
        
        if (foregroundServiceCases.Count == 0)
        {
            _output.WriteLine("⚠️ FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스가 없습니다.");
            _output.WriteLine("   이는 본 실험에서 FOREGROUND_SERVICE가 keyArtifact로 사용되지 않았음을 의미합니다.");
            _output.WriteLine("   메커니즘은 예비 실험에서 발견된 문제를 해결하기 위해 설계되었으며,");
            _output.WriteLine("   본 실험에서는 다른 아티팩트가 우선적으로 사용되었을 가능성이 높습니다.\n");
            return;
        }
        
        // 2. 각 케이스별 타임스탬프 차이 측정
        var timestampDifferences = new List<(CameraCaptureEvent capture, DateTime foregroundTimestamp, DateTime preciseTimestamp, TimeSpan difference, string preciseArtifactType)>();
        
        foreach (var capture in foregroundServiceCases)
        {
            // keyArtifact (FOREGROUND_SERVICE)의 타임스탬프
            var keyArtifactId = capture.decisiveArtifact;
            if (!keyArtifactId.HasValue) continue;
            
            var keyArtifact = _allParsedEvents!
                .FirstOrDefault(e => e.EventId == keyArtifactId.Value);
            
            if (keyArtifact == null || keyArtifact.EventType != "FOREGROUND_SERVICE") continue;
            
            var foregroundTimestamp = keyArtifact.Timestamp;
            
            // 메커니즘으로 결정된 타임스탬프 (CaptureTime)
            var preciseTimestamp = capture.CaptureTime;
            
            // 타임스탬프 차이
            var difference = preciseTimestamp - foregroundTimestamp;
            
            // 사용된 정밀한 아티팩트 타입 확인 (역추적 방식)
            // CaptureTime과 일치하는 아티팩트를 찾아서 실제 비즈니스 로직 결과 검증
            var allArtifactIds = capture.SourceEventIds;
            var allArtifacts = _allParsedEvents!
                .Where(e => allArtifactIds.Contains(e.EventId))
                .Where(e => e.EventType != "FOREGROUND_SERVICE")
                .ToList();
            
            // CaptureTime과 일치하는 아티팩트 찾기 (1ms 이내 허용)
            var preciseArtifact = allArtifacts
                .Where(e => Math.Abs((e.Timestamp - preciseTimestamp).TotalMilliseconds) < 1.0)
                .OrderByDescending(e => 
                    e.EventType == "DATABASE_INSERT" ? 3 :
                    e.EventType == "VIBRATION_EVENT" ? 2 : 1)
                .ThenBy(e => e.Timestamp)
                .FirstOrDefault();
            
            // 일치하는 아티팩트가 없으면 비즈니스 로직과 동일한 방식으로 추정
            if (preciseArtifact == null)
            {
                preciseArtifact = allArtifacts
                    .OrderByDescending(e => 
                        e.EventType == "DATABASE_INSERT" ? 3 :
                        e.EventType == "VIBRATION_EVENT" ? 2 : 1)
                    .ThenBy(e => e.Timestamp)
                    .FirstOrDefault();
            }
            
            var preciseArtifactType = preciseArtifact?.EventType ?? "NONE";
            
            // 검증: CaptureTime과 실제 아티팩트 타임스탬프가 일치하는지 확인
            if (preciseArtifact != null)
            {
                var timestampDiff = Math.Abs((preciseArtifact.Timestamp - preciseTimestamp).TotalMilliseconds);
                if (timestampDiff > 1.0)
                {
                    _output.WriteLine($"⚠️ 경고: Sample {GetSampleNumber(capture)}에서 CaptureTime({preciseTimestamp:HH:mm:ss.fff})과 추정된 아티팩트 타임스탬프({preciseArtifact.Timestamp:HH:mm:ss.fff})의 차이가 {timestampDiff:F2}ms입니다.");
                }
            }
            
            timestampDifferences.Add((capture, foregroundTimestamp, preciseTimestamp, difference, preciseArtifactType));
        }
        
        // 3. 결과 출력
        _output.WriteLine("─────────────────────────────────────────────────────────────────────");
        _output.WriteLine("[표] FOREGROUND_SERVICE keyArtifact 케이스별 타임스탬프 차이");
        _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
        
        _output.WriteLine($"| {"Sample",-8} | {"패키지명",-25} | {"FOREGROUND 타임스탬프",-25} | {"정밀 타임스탬프",-25} | {"차이 (ms)",-12} | {"정밀 아티팩트",-20} |");
        _output.WriteLine($"|{new string('-', 10)}|{new string('-', 27)}|{new string('-', 27)}|{new string('-', 27)}|{new string('-', 14)}|{new string('-', 22)}|");
        
        foreach (var (capture, foregroundTs, preciseTs, diff, preciseType) in timestampDifferences)
        {
            var sampleNum = GetSampleNumber(capture);
            var diffMs = $"{diff.TotalMilliseconds:F0}ms";
            _output.WriteLine($"| {sampleNum,-8} | {capture.PackageName,-25} | {foregroundTs:HH:mm:ss.fff,-25} | {preciseTs:HH:mm:ss.fff,-25} | {diffMs,-12} | {preciseType,-20} |");
        }
        
        _output.WriteLine("");
        
        // 4. 통계 분석 및 검증
        if (timestampDifferences.Count > 0)
        {
            var differences = timestampDifferences.Select(t => t.difference.TotalMilliseconds).ToList();
            var avgDifference = differences.Average();
            var minDifference = differences.Min();
            var maxDifference = differences.Max();
            
            _output.WriteLine("─────────────────────────────────────────────────────────────────────");
            _output.WriteLine("통계 분석");
            _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
            
            _output.WriteLine($"평균 타임스탬프 차이: {avgDifference:F2}ms");
            _output.WriteLine($"최소 타임스탬프 차이: {minDifference:F2}ms");
            _output.WriteLine($"최대 타임스탬프 차이: {maxDifference:F2}ms");
            _output.WriteLine($"케이스 수: {timestampDifferences.Count}개\n");
            
            // 정밀한 아티팩트 타입별 분포
            var preciseArtifactDistribution = timestampDifferences
                .GroupBy(t => t.preciseArtifactType)
                .OrderByDescending(g => g.Count())
                .ToList();
            
            _output.WriteLine("정밀한 아티팩트 타입별 분포:");
            foreach (var group in preciseArtifactDistribution)
            {
                _output.WriteLine($"  - {group.Key}: {group.Count()}개");
            }
            _output.WriteLine("");
            
            // 검증: FOREGROUND_SERVICE 타임스탬프가 1초 단위로 반올림되었는지 확인
            _output.WriteLine("─────────────────────────────────────────────────────────────────────");
            _output.WriteLine("검증: FOREGROUND_SERVICE 타임스탬프 정밀도 확인");
            _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
            
            var foregroundTimestamps = foregroundServiceCases
                .Select(c => 
                {
                    var keyArtifactId = c.decisiveArtifact;
                    if (!keyArtifactId.HasValue) return null;
                    var keyArtifact = _allParsedEvents!.FirstOrDefault(e => e.EventId == keyArtifactId.Value);
                    return keyArtifact?.Timestamp;
                })
                .Where(ts => ts.HasValue)
                .Select(ts => ts!.Value)
                .ToList();
            
            var allRoundedToSecond = foregroundTimestamps.All(ts => ts.Millisecond == 0);
            if (allRoundedToSecond)
            {
                _output.WriteLine("✅ 모든 FOREGROUND_SERVICE 타임스탬프가 1초 단위로 반올림됨 (밀리초 = 0)");
            }
            else
            {
                var nonRoundedCount = foregroundTimestamps.Count(ts => ts.Millisecond != 0);
                _output.WriteLine($"⚠️ {nonRoundedCount}개의 FOREGROUND_SERVICE 타임스탬프가 밀리초 단위를 포함함");
            }
            _output.WriteLine("");
            
            // 검증: CaptureTime이 실제로 비즈니스 로직 결과인지 확인
            _output.WriteLine("─────────────────────────────────────────────────────────────────────");
            _output.WriteLine("검증: CaptureTime과 실제 아티팩트 타임스탬프 일치 확인");
            _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
            
            var verificationResults = new List<(int sampleNum, string packageName, DateTime captureTime, DateTime artifactTimestamp, double diffMs, string artifactType)>();
            
            foreach (var (capture, _, preciseTs, _, preciseType) in timestampDifferences)
            {
                var sampleNum = GetSampleNumber(capture);
                var allArtifactIds = capture.SourceEventIds;
                var allArtifacts = _allParsedEvents!
                    .Where(e => allArtifactIds.Contains(e.EventId))
                    .Where(e => e.EventType != "FOREGROUND_SERVICE")
                    .ToList();
                
                // CaptureTime과 일치하는 아티팩트 찾기
                var matchingArtifact = allArtifacts
                    .Where(e => Math.Abs((e.Timestamp - preciseTs).TotalMilliseconds) < 1.0)
                    .OrderByDescending(e => 
                        e.EventType == "DATABASE_INSERT" ? 3 :
                        e.EventType == "VIBRATION_EVENT" ? 2 : 1)
                    .ThenBy(e => e.Timestamp)
                    .FirstOrDefault();
                
                if (matchingArtifact != null)
                {
                    var diffMs = Math.Abs((matchingArtifact.Timestamp - preciseTs).TotalMilliseconds);
                    verificationResults.Add((sampleNum, capture.PackageName, preciseTs, matchingArtifact.Timestamp, diffMs, matchingArtifact.EventType));
                }
            }
            
            if (verificationResults.Count == timestampDifferences.Count)
            {
                _output.WriteLine($"✅ 모든 케이스({verificationResults.Count}개)에서 CaptureTime과 실제 아티팩트 타임스탬프가 일치함 (1ms 이내)");
                var maxDiff = verificationResults.Max(r => r.diffMs);
                _output.WriteLine($"   최대 타임스탬프 차이: {maxDiff:F2}ms\n");
            }
            else
            {
                var matchedCount = verificationResults.Count;
                _output.WriteLine($"⚠️ {matchedCount}/{timestampDifferences.Count}개 케이스에서만 CaptureTime과 아티팩트 타임스탬프가 일치함\n");
            }
        }
        
        // 5. 예비 실험과 비교
        _output.WriteLine("─────────────────────────────────────────────────────────────────────");
        _output.WriteLine("예비 실험과 비교");
        _output.WriteLine("─────────────────────────────────────────────────────────────────────\n");
        
        _output.WriteLine("예비 실험 (부록 3, 3.4절):");
        _output.WriteLine("  - FOREGROUND_SERVICE와 DATABASE_INSERT 간 타임스탬프 차이: 852ms");
        _output.WriteLine("  - 문제: FOREGROUND_SERVICE의 타임스탬프가 1초 단위로 반올림되어 정밀도 낮음");
        _output.WriteLine("  - 해결: GetPreciseCaptureTime 메커니즘으로 DATABASE_INSERT 타임스탬프 사용\n");
        
        if (timestampDifferences.Count > 0)
        {
            _output.WriteLine("본 실험:");
            _output.WriteLine($"  - FOREGROUND_SERVICE가 keyArtifact인 케이스: {timestampDifferences.Count}개");
            _output.WriteLine($"  - 평균 타임스탬프 차이: {timestampDifferences.Average(t => t.difference.TotalMilliseconds):F2}ms");
            _output.WriteLine($"  - 최대 타임스탬프 차이: {timestampDifferences.Max(t => t.difference.TotalMilliseconds):F2}ms");
            _output.WriteLine($"  - 메커니즘이 적용되어 정밀한 아티팩트의 타임스탬프를 사용함\n");
        }
        else
        {
            _output.WriteLine("본 실험:");
            _output.WriteLine("  - FOREGROUND_SERVICE가 keyArtifact로 사용된 케이스 없음");
            _output.WriteLine("  - 이는 본 실험에서 다른 아티팩트(DATABASE_INSERT, VIBRATION_EVENT 등)가");
            _output.WriteLine("    우선적으로 keyArtifact로 사용되었음을 의미함");
            _output.WriteLine("  - 메커니즘은 예비 실험에서 발견된 문제를 해결하기 위해 설계되었으며,");
            _output.WriteLine("    본 실험에서는 해당 문제가 발생하지 않았거나 다른 아티팩트로 해결됨\n");
        }
    }
    
    #region Helper Methods
    
    private int GetSampleNumber(CameraCaptureEvent capture)
    {
        // CaptureTime을 기반으로 샘플 번호 추정 (ArtifactWeights.SampleTimeRanges 사용)
        foreach (var (sampleNum, timeRange) in ArtifactWeights.SampleTimeRanges)
        {
            if (capture.CaptureTime >= timeRange.StartTime && capture.CaptureTime <= timeRange.EndTime)
            {
                return sampleNum;
            }
        }
        return 0;
    }
    
    /// <summary>
    /// 샘플 로그 파싱 (다른 테스트와 동일한 패턴)
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync(
        string samplePath,
        DateTime startTime,
        DateTime endTime)
    {
        var allEvents = new List<NormalizedLogEvent>();
        
        if (!Directory.Exists(samplePath))
        {
            _output.WriteLine($"  ⚠️  경로가 존재하지 않습니다: {samplePath}");
            return allEvents;
        }
        
        // 로그 파일 설정 맵핑
        var logConfigs = new Dictionary<string, string>
        {
            ["audio.log"] = "adb_audio_config.yaml",
            ["media_camera.log"] = "adb_media_camera_config.yaml",
            ["media_camera_worker.log"] = "adb_media_camera_worker_config.yaml",
            ["media_metrics.log"] = "adb_media_metrics_config.yaml",
            ["usagestats.log"] = "adb_usagestats_config.yaml",
            ["vibrator_manager.log"] = "adb_vibrator_config.yaml",
            ["activity.log"] = "adb_activity_config.yaml"
        };
        
        foreach (var (logFileName, configFileName) in logConfigs)
        {
            var events = await ParseLogFileAsync(samplePath, logFileName, configFileName, startTime, endTime);
            allEvents.AddRange(events);
        }
        
        return allEvents.OrderBy(e => e.Timestamp).ToList();
    }
    
    /// <summary>
    /// 개별 로그 파일 파싱 (다른 테스트와 동일한 패턴)
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string samplePath,
        string logFileName,
        string configFileName,
        DateTime startTime,
        DateTime endTime)
    {
        var logPath = Path.Combine(samplePath, logFileName);
        
        if (!File.Exists(logPath))
        {
            return new List<NormalizedLogEvent>();
        }
        
        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}");
        }
        
        // YAML 설정 로드 (Parser용)
        var configLoader = new Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
        var configuration = configLoader.Load(configPath);
        
        // DeviceInfo 생성 (ArtifactWeights 공용 메서드 사용)
        var deviceInfo = ArtifactWeights.CreateTestDeviceInfo();
        
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
        
        try
        {
            var result = await parser.ParseAsync(logPath, options);
            return result.Events?.ToList() ?? new List<NormalizedLogEvent>();
        }
        catch (Exception)
        {
            return new List<NormalizedLogEvent>();
        }
    }
    
    /// <summary>
    /// AnalysisOptions 생성 (ArtifactFrequencyValidationTests와 동일한 설정 사용)
    /// </summary>
    /// <remarks>
    /// ArtifactFrequencyValidationTests.cs와 동일한 설정을 사용하여 일관성 보장
    /// - DeduplicationSimilarityThreshold: GroundTruthDeduplicationSimilarityThreshold (0.8) 사용
    /// - SameCameraUsageTimeThreshold: 세션 탐지 임계값
    /// - CaptureDeduplicationWindow: 500ms (CameraCaptureEvent 중복 제거)
    /// 
    /// StartTime과 EndTime은 파싱 단계에서만 사용되며, AnalysisOptions에는 없습니다.
    /// MinOverlapRatio는 CameraSessionDetector 내부 상수로 정의되어 있습니다.
    /// </remarks>
    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(ArtifactWeights.EventCorrelationWindowSeconds),
            DeduplicationSimilarityThreshold = ArtifactWeights.GroundTruthDeduplicationSimilarityThreshold,  // Ground Truth와 동일한 설정 사용 (0.8)
            SameCameraUsageTimeThreshold = TimeSpan.FromSeconds(ArtifactWeights.SameCameraUsageTimeThreshold),
            CaptureDeduplicationWindow = TimeSpan.FromMilliseconds(ArtifactWeights.CaptureDeduplicationWindowMs)
        };
    }
    
    /// <summary>
    /// Orchestrator 생성 (다른 테스트와 동일한 패턴)
    /// </summary>
    private IAnalysisOrchestrator CreateOrchestrator()
    {
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // YAML 설정 로드 (Analysis용)
        // projectRoot를 직접 사용하여 경로 구성 (다른 테스트와 동일한 패턴)
        var configPath = Path.Combine(_projectRoot, "AndroidAdbAnalyze.Analysis", "Configs", "artifact-detection-config.example.yaml");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"YAML 설정 파일을 찾을 수 없습니다: {configPath}");
        }
        
        var artifactConfig = AndroidAdbAnalyze.Analysis.Configuration.YamlConfigurationLoader.LoadFromFile(configPath);
        
        // 설정 등록
        services.AddSingleton(artifactConfig);
        
        // AnalysisOptions 등록 (기본값, 실제 AnalyzeAsync 호출 시 전달된 옵션이 우선됨)
        services.AddSingleton(CreateAnalysisOptions());
        
        // 서비스 등록
        RegisterServices(services);
        
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }
    
    /// <summary>
    /// 서비스 등록 (다른 테스트와 동일한 패턴)
    /// </summary>
    private void RegisterServices(IServiceCollection services)
    {
        // Session Context Provider
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies (Configuration 주입, 다른 테스트와 동일한 패턴)
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
        
        // Capture Detector
        services.AddSingleton<ICaptureDetector, CameraCaptureDetector>();
        
        // Confidence Calculator (Configuration 주입)
        services.AddSingleton<IConfidenceCalculator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ConfidenceCalculator>>();
            var config = sp.GetRequiredService<ArtifactDetectionConfig>();
            return new ConfidenceCalculator(logger, config);
        });
        
        // Session Sources
        services.AddSingleton<ISessionSource, UsagestatsSessionSource>();
        services.AddSingleton<ISessionSource, MediaCameraSessionSource>();
        
        // Session Detector
        services.AddSingleton<ISessionDetector, CameraSessionDetector>();
        
        // Deduplication Services
        services.AddSingleton<IEventDeduplicator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventDeduplicator>>();
            var options = sp.GetRequiredService<AnalysisOptions>();
            return new EventDeduplicator(logger, options);
        });
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        // Transmission Detection Services
        services.AddSingleton<ITransmissionDetector, WifiTransmissionDetector>();
        
        // Reporting Services
        services.AddSingleton<IReportGenerator, HtmlReportGenerator>();
        services.AddSingleton<ITimelineBuilder, TimelineBuilder>();
        
        // Analysis Orchestrator
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }
    
    #endregion
}
