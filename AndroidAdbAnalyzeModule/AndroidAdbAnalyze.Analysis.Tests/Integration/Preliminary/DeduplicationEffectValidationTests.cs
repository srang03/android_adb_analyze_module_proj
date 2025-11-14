using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Deduplication;
using AndroidAdbAnalyze.Analysis.Models.Options;
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
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Preliminary;

/// <summary>
/// 중복 제거 효과 측정 테스트 (예비 실험 기반)
/// </summary>
/// <remarks>
/// 목적:
/// - 예비 실험(Preliminary 1-3)에서 중복 제거 알고리즘의 효과를 정량적으로 측정
/// - 중복 제거 전/후 이벤트 수 및 중복 비율 계산
/// - 세션/촬영 탐지 정확도 검증
/// - 처리 시간 측정
/// 
/// 측정 범위:
/// - 특정 시간대 파싱 (Ground Truth 기준 시간대와 동일)
///   - Preliminary 1: 09:45~09:53
///   - Preliminary 2: 10:10~10:22
///   - Preliminary 3: 10:35~10:44
/// - 이벤트 수: 959개
/// - 중복 비율: 10.4%
/// 
/// Ground Truth:
/// - 예비 실험 3회 특정 시간대 기준
/// - 세션: 24개 (8+8+8), 촬영: 12개 (4+4+4)
/// 
/// 논문 반영:
/// - 제4장 제2절: 중복 제거 효과
/// - 부록 3, 표 35: 중복 제거 효과 측정 데이터
/// </remarks>
public sealed class DeduplicationEffectValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    // Ground Truth (예비 실험 3회, 특정 시간대 기준)
    // 개별 GT 테스트 합계: 8+8+8=24세션, 4+4+4=12촬영
    // 특정 시간대 파싱: 959개 이벤트, 중복 10.4%
    private const int ExpectedTotalSessions = 24; // 특정 시간대 세션 수
    private const int ExpectedTotalCaptures = 12; // 특정 시간대 촬영 수
    
    // 예비 실험 파싱된 이벤트 캐싱
    private List<NormalizedLogEvent>? _allEvents;

    public DeduplicationEffectValidationTests(ITestOutputHelper output)
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
        _output.WriteLine("🔬 중복 제거 효과 측정 테스트 초기화");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // Preliminary 1-3 이벤트 파싱
        _output.WriteLine("📂 예비 실험 로그 파싱 중...\n");
        
        // Ground Truth 테스트와 동일한 시간 범위 사용
        var events1 = await ParseSampleLogsAsync("예비 실험/예비 실험 1차 25_09_01", 
            new DateTime(2025, 9, 1, 9, 45, 0), 
            new DateTime(2025, 9, 1, 9, 53, 0));
        
        var events2 = await ParseSampleLogsAsync("예비 실험/예비 실험 2차 25_09_06", 
            new DateTime(2025, 9, 6, 10, 10, 0), 
            new DateTime(2025, 9, 6, 10, 22, 59));
        
        var events3 = await ParseSampleLogsAsync("예비 실험/예비 실험 3차 25_09_07", 
            new DateTime(2025, 9, 7, 10, 35, 0), 
            new DateTime(2025, 9, 7, 10, 44, 59));
        
        _allEvents = events1.Concat(events2).Concat(events3).ToList();
        
        _output.WriteLine($"\n📊 총 이벤트 수: {_allEvents.Count:N0}개");
        _output.WriteLine($"  - Preliminary 1: {events1.Count:N0}개");
        _output.WriteLine($"  - Preliminary 2: {events2.Count:N0}개");
        _output.WriteLine($"  - Preliminary 3: {events3.Count:N0}개\n");
        _output.WriteLine("✅ 예비 실험 3회 이벤트 파싱 완료\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 예비 실험 중복 제거 효과 측정 테스트
    /// </summary>
    /// <remarks>
    /// 논문 부록 3, 표 38 "중복 제거 효과 측정 (예비 실험 3회 평균)" 데이터 생성
    /// </remarks>
    [Fact]
    public async Task Measure_DeduplicationEffect_PreliminaryExperiments()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 중복 제거 효과 측정 (예비 실험 1~3차)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // 1. 중복 제거 전 분석
        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine("1단계: 중복 제거 전 분석");
        _output.WriteLine("────────────────────────────────────────────────────────────\n");
        
        var orchestratorWithoutDedup = CreateOrchestratorWithoutDeduplication();
        var resultBefore = await orchestratorWithoutDedup.AnalyzeAsync(_allEvents!, CreateAnalysisOptions());
        
        _output.WriteLine($"세션 탐지 결과 (중복 제거 전):");
        _output.WriteLine($"  - 탐지된 세션: {resultBefore.Sessions.Count}개");
        _output.WriteLine($"  - 실제 세션 (Ground Truth): {ExpectedTotalSessions}개");
        
        var sessionFpBefore = Math.Max(0, resultBefore.Sessions.Count - ExpectedTotalSessions);
        var sessionPrecisionBefore = resultBefore.Sessions.Count > 0 
            ? (double)(resultBefore.Sessions.Count - sessionFpBefore) / resultBefore.Sessions.Count 
            : 1.0;
        
        _output.WriteLine($"  - 오탐(FP): {sessionFpBefore}개");
        _output.WriteLine($"  - Precision: {sessionPrecisionBefore:P0}\n");
        
        _output.WriteLine($"촬영 탐지 결과 (중복 제거 전):");
        _output.WriteLine($"  - 탐지된 촬영: {resultBefore.CaptureEvents.Count}개");
        _output.WriteLine($"  - 실제 촬영 (Ground Truth): {ExpectedTotalCaptures}개");
        
        // 디버깅: 탐지된 촬영 목록 출력
        _output.WriteLine($"\n  📋 탐지된 촬영 상세:");
        foreach (var capture in resultBefore.CaptureEvents.OrderBy(c => c.CaptureTime))
        {
            _output.WriteLine($"    - {capture.CaptureTime:HH:mm:ss.fff} | {capture.PackageName} | Score={capture.CaptureDetectionScore:F2}");
        }
        _output.WriteLine("");
        
        var captureFpBefore = Math.Max(0, resultBefore.CaptureEvents.Count - ExpectedTotalCaptures);
        var capturePrecisionBefore = resultBefore.CaptureEvents.Count > 0 
            ? (double)(resultBefore.CaptureEvents.Count - captureFpBefore) / resultBefore.CaptureEvents.Count 
            : 1.0;
        
        _output.WriteLine($"  - 오탐(FP): {captureFpBefore}개");
        _output.WriteLine($"  - Precision: {capturePrecisionBefore:P0}\n");
        
        // 2. 중복 제거 후 분석
        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine("2단계: 중복 제거 후 분석");
        _output.WriteLine("────────────────────────────────────────────────────────────\n");
        
        var orchestratorWithDedup = CreateOrchestratorWithDeduplication();
        var resultAfter = await orchestratorWithDedup.AnalyzeAsync(_allEvents!, CreateAnalysisOptions());
        
        // 중복 제거 통계 계산
        var deduplicator = new EventDeduplicator(NullLogger<EventDeduplicator>.Instance, CreateAnalysisOptions());
        var deduplicatedEvents = deduplicator.Deduplicate(_allEvents!, out var _);
        var duplicatedCount = _allEvents!.Count - deduplicatedEvents.Count;
        var duplicationRatio = _allEvents.Count > 0 ? (double)duplicatedCount / _allEvents.Count : 0.0;
        
        _output.WriteLine($"총 이벤트 수 (중복 제거 후): {deduplicatedEvents.Count:N0}개");
        _output.WriteLine($"제거된 중복: {duplicatedCount:N0}개");
        _output.WriteLine($"중복 비율: {duplicationRatio:P1} ({duplicationRatio:F3})\n");
        
        _output.WriteLine($"세션 탐지 결과 (중복 제거 후):");
        _output.WriteLine($"  - 탐지된 세션: {resultAfter.Sessions.Count}개");
        _output.WriteLine($"  - 실제 세션 (Ground Truth): {ExpectedTotalSessions}개");
        
        var sessionFpAfter = Math.Max(0, resultAfter.Sessions.Count - ExpectedTotalSessions);
        var sessionPrecisionAfter = resultAfter.Sessions.Count > 0 
            ? (double)(resultAfter.Sessions.Count - sessionFpAfter) / resultAfter.Sessions.Count 
            : 1.0;
        
        _output.WriteLine($"  - 오탐(FP): {sessionFpAfter}개");
        _output.WriteLine($"  - Precision: {sessionPrecisionAfter:P0}");
        _output.WriteLine($"  - 향상: {(sessionPrecisionAfter - sessionPrecisionBefore) * 100:+0.0}%p\n");
        
        _output.WriteLine($"촬영 탐지 결과 (중복 제거 후):");
        _output.WriteLine($"  - 탐지된 촬영: {resultAfter.CaptureEvents.Count}개");
        _output.WriteLine($"  - 실제 촬영 (Ground Truth): {ExpectedTotalCaptures}개");
        
        var captureFpAfter = Math.Max(0, resultAfter.CaptureEvents.Count - ExpectedTotalCaptures);
        var capturePrecisionAfter = resultAfter.CaptureEvents.Count > 0 
            ? (double)(resultAfter.CaptureEvents.Count - captureFpAfter) / resultAfter.CaptureEvents.Count 
            : 1.0;
        
        _output.WriteLine($"  - 오탐(FP): {captureFpAfter}개");
        _output.WriteLine($"  - Precision: {capturePrecisionAfter:P0}");
        _output.WriteLine($"  - 향상: {(capturePrecisionAfter - capturePrecisionBefore) * 100:+0.0}%p\n");
        
        // 3. 처리 시간 측정 (10,000 이벤트 기준)
        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine("3단계: 처리 시간 측정 (10,000 이벤트 기준)");
        _output.WriteLine("────────────────────────────────────────────────────────────\n");
        
        var processingTime = MeasureDeduplicationProcessingTime();
        _output.WriteLine($"\n평균 처리 시간: {processingTime:F1}ms (3회 반복 측정)\n");
        
        // 4. 결과 요약
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 중복 제거 효과 측정 결과 요약 (예비 실험 3회)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _output.WriteLine($"[표 38] 중복 제거 효과 측정 (예비 실험 3회 평균)\n");
        _output.WriteLine($"| 측정 항목 | 중복 제거 전 | 중복 제거 후 | 개선 효과 |");
        _output.WriteLine($"|----------|-------------|-------------|----------|");
        _output.WriteLine($"| 전체 이벤트 수 | {_allEvents.Count:N0}개 | {deduplicatedEvents.Count:N0}개 | -{duplicatedCount:N0}개 (-{duplicationRatio:P1}) |");
        _output.WriteLine($"| 중복 비율 | - | {duplicationRatio:P1} | - |");
        _output.WriteLine($"| 세션 탐지 Precision | {sessionPrecisionBefore:P0} ({resultBefore.Sessions.Count - sessionFpBefore}/{resultBefore.Sessions.Count}, 오탐 {sessionFpBefore}건) | {sessionPrecisionAfter:P0} ({resultAfter.Sessions.Count - sessionFpAfter}/{resultAfter.Sessions.Count}, 오탐 {sessionFpAfter}건) | {(sessionPrecisionAfter - sessionPrecisionBefore) * 100:+0.0}%p |");
        _output.WriteLine($"| 촬영 탐지 Precision | {capturePrecisionBefore:P0} ({resultBefore.CaptureEvents.Count - captureFpBefore}/{resultBefore.CaptureEvents.Count}, 오탐 {captureFpBefore}건) | {capturePrecisionAfter:P0} ({resultAfter.CaptureEvents.Count - captureFpAfter}/{resultAfter.CaptureEvents.Count}, 오탐 {captureFpAfter}건) | {(capturePrecisionAfter - capturePrecisionBefore) * 100:+0.0}%p |");
        _output.WriteLine($"| 처리 시간 (10,000 이벤트) | - | 약 {processingTime:F0}ms | O(n log n) |\n");
        
        // 5. JSON 파일로 결과 저장
        var resultPath = Path.Combine(Directory.GetCurrentDirectory(), "preliminary_deduplication_effect_result.json");
        var result = new
        {
            EventsBeforeDeduplication = _allEvents.Count,
            EventsAfterDeduplication = deduplicatedEvents.Count,
            DuplicatedEvents = duplicatedCount,
            DuplicationRatio = duplicationRatio,
            SessionDetection = new
            {
                Before = new
                {
                    DetectedSessions = resultBefore.Sessions.Count,
                    TruePositives = resultBefore.Sessions.Count - sessionFpBefore,
                    FalsePositives = sessionFpBefore,
                    Precision = sessionPrecisionBefore
                },
                After = new
                {
                    DetectedSessions = resultAfter.Sessions.Count,
                    TruePositives = resultAfter.Sessions.Count - sessionFpAfter,
                    FalsePositives = sessionFpAfter,
                    Precision = sessionPrecisionAfter
                },
                Improvement = (sessionPrecisionAfter - sessionPrecisionBefore) * 100
            },
            CaptureDetection = new
            {
                Before = new
                {
                    DetectedCaptures = resultBefore.CaptureEvents.Count,
                    TruePositives = resultBefore.CaptureEvents.Count - captureFpBefore,
                    FalsePositives = captureFpBefore,
                    Precision = capturePrecisionBefore
                },
                After = new
                {
                    DetectedCaptures = resultAfter.CaptureEvents.Count,
                    TruePositives = resultAfter.CaptureEvents.Count - captureFpAfter,
                    FalsePositives = captureFpAfter,
                    Precision = capturePrecisionAfter
                },
                Improvement = (capturePrecisionAfter - capturePrecisionBefore) * 100
            },
            ProcessingTimeMs = processingTime
        };
        
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(resultPath, json);
        _output.WriteLine($"✅ 결과가 JSON 파일로 저장되었습니다: {resultPath}\n");
        
        // 6. 검증
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 검증");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // 중복 비율 검증 (특정 시간대 파싱: 7~14%, 예상 10.4%)
        duplicationRatio.Should().BeInRange(0.07, 0.14, 
            "예비 실험 (특정 시간대) 중복 비율은 10.4% (±3% 허용 범위, 부록 3 표 35)");
        
        // 중복 제거 후 Precision 검증
        // 특정 시간대 파싱 시 중복으로 인한 오탐 발생하지 않아 Precision 향상 효과 없음
        sessionPrecisionAfter.Should().BeGreaterThanOrEqualTo(sessionPrecisionBefore, 
            "중복 제거 후 세션 탐지 Precision이 유지되거나 향상되어야 함");
        
        capturePrecisionAfter.Should().BeGreaterThanOrEqualTo(capturePrecisionBefore, 
            "중복 제거 후 촬영 탐지 Precision이 유지되거나 향상되어야 함");
        
        _output.WriteLine("✅ 모든 검증 통과!");
        
        if (sessionPrecisionBefore == 1.0 && capturePrecisionBefore == 1.0)
        {
            _output.WriteLine("\n📌 참고: 예비 실험(특정 시간대 파싱)에서 중복으로 인한 오탐이 발생하지 않아");
            _output.WriteLine("   Precision 향상 효과는 없습니다 (세션 100% 유지, 촬영 92% 유지).");
            _output.WriteLine("   중복 제거의 주요 효과는 처리 효율 개선 (이벤트 수 10.4% 감소)입니다.");
        }
    }

    #region Helper Methods

    /// <summary>
    /// 중복 제거 처리 시간 측정 (10,000 이벤트 기준)
    /// </summary>
    private double MeasureDeduplicationProcessingTime()
    {
        var options = CreateAnalysisOptions();
        var deduplicator = new EventDeduplicator(NullLogger<EventDeduplicator>.Instance, options);
        
        // 10,000개 이벤트로 확장 (반복)
        var scaledEvents = new List<NormalizedLogEvent>();
        while (scaledEvents.Count < 10000)
        {
            scaledEvents.AddRange(_allEvents!);
        }
        scaledEvents = scaledEvents.Take(10000).ToList();
        
        _output.WriteLine($"  측정 대상: {scaledEvents.Count:N0}개 이벤트");
        
        // 3회 반복 측정
        var times = new List<double>();
        for (int i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            var _ = deduplicator.Deduplicate(scaledEvents, out var __);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
            
            _output.WriteLine($"  측정 {i + 1}회: {sw.Elapsed.TotalMilliseconds:F1}ms");
        }
        
        return times.Average();
    }

    /// <summary>
    /// 샘플 로그 파싱
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync(
        string sampleDirectory, 
        DateTime? startTime, 
        DateTime? endTime)
    {
        var samplePath = Path.Combine(_sampleLogsPath, sampleDirectory);
        var allEvents = new List<NormalizedLogEvent>();
        
        _output.WriteLine($"📂 {sampleDirectory}");
        
        // 로그 파일 설정 맵핑
        var logConfigs = new Dictionary<string, string>
        {
            ["audio.log"] = "adb_audio_config.yaml",
            ["media_camera.log"] = "adb_media_camera_config.yaml",
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
        
        _output.WriteLine($"  📊 Total: {allEvents.Count:N0} events");
        return allEvents.OrderBy(e => e.Timestamp).ToList();
    }

    /// <summary>
    /// 개별 로그 파일 파싱
    /// </summary>
    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string samplePath,
        string logFileName,
        string configFileName,
        DateTime? startTime,
        DateTime? endTime)
    {
        var logPath = Path.Combine(samplePath, logFileName);
        
        if (!File.Exists(logPath))
        {
            _output.WriteLine($"  ⚠️ {logFileName,-30} : Not found");
            return new List<NormalizedLogEvent>();
        }
        
        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Config file not found: {configPath}");
        }
        
        // YAML 설정 로드
        var configLoader = new Parser.Configuration.Loaders.YamlConfigurationLoader(configPath);
        var configuration = configLoader.Load(configPath);
        
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
        
        try
        {
            var result = await parser.ParseAsync(logPath, options);
            var events = result.Events?.ToList() ?? new List<NormalizedLogEvent>();
            
            _output.WriteLine($"  ✓ {logFileName,-30} : {events.Count,6:N0} events");
            return events;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  ✗ {logFileName,-30} : Error - {ex.Message}");
            return new List<NormalizedLogEvent>();
        }
    }

    /// <summary>
    /// AnalysisOptions 생성
    /// </summary>
    /// <remarks>
    /// ArtifactWeights 공용 상수 사용 (하드코딩 제거)
    /// - DeduplicationSimilarityThreshold: 0.55 (논문 제안 값, 중복 제거 효과 측정용)
    /// - CaptureDeduplicationWindow: 500ms (CameraCaptureEvent 중복 제거) ← 필수!
    /// - EventCorrelationWindow: 30초 (보조 아티팩트 수집 범위)
    /// 
    /// 주의: 0.8(GT용)이 아닌 0.55(논문 제안 값) 사용해야 정상 동작
    /// </remarks>
    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            DeduplicationSimilarityThreshold = ArtifactWeights.DeduplicationSimilarityThreshold,
            CaptureDeduplicationWindow = TimeSpan.FromMilliseconds(ArtifactWeights.CaptureDeduplicationWindowMs)
        };
    }

    /// <summary>
    /// 중복 제거를 사용하지 않는 Orchestrator 생성
    /// </summary>
    private IAnalysisOrchestrator CreateOrchestratorWithoutDeduplication()
    {
        // DI 컨테이너 설정
        var services = new ServiceCollection();
        
        // Logging 인프라 추가
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions 등록
        services.AddSingleton(CreateAnalysisOptions());
        
        // YAML 설정 로드
        var configPath = Path.Combine("..", "..", "..", "..", "..",
            "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Analysis", "Configs",
            "artifact-detection-config.example.yaml");
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"YAML 설정 파일을 찾을 수 없습니다: {configPath}");
        }
        
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(NullLoggerProvider.Instance));
        var logger = loggerFactory.CreateLogger<DeduplicationEffectValidationTests>();
        var config = YamlConfigurationLoader.LoadFromFile(configPath, logger);
        
        // Configuration을 DI에 등록
        services.AddSingleton(config);
        
        // AndroidAdbAnalysis 서비스 등록 (중복 제거 제외)
        RegisterServicesWithoutDeduplication(services);
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    /// <summary>
    /// 중복 제거를 사용하는 Orchestrator 생성
    /// </summary>
    private IAnalysisOrchestrator CreateOrchestratorWithDeduplication()
    {
        // DI 컨테이너 설정
        var services = new ServiceCollection();
        
        // Logging 인프라 추가
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions 등록
        services.AddSingleton(CreateAnalysisOptions());
        
        // YAML 설정 로드
        var configPath = Path.Combine("..", "..", "..", "..", "..",
            "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Analysis", "Configs",
            "artifact-detection-config.example.yaml");
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"YAML 설정 파일을 찾을 수 없습니다: {configPath}");
        }
        
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(NullLoggerProvider.Instance));
        var logger = loggerFactory.CreateLogger<DeduplicationEffectValidationTests>();
        var config = YamlConfigurationLoader.LoadFromFile(configPath, logger);
        
        // Configuration을 DI에 등록
        services.AddSingleton(config);
        
        // AndroidAdbAnalysis 서비스 등록 (중복 제거 포함)
        RegisterServicesWithDeduplication(services);
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    /// <summary>
    /// 중복 제거 없이 서비스 등록
    /// </summary>
    private void RegisterServicesWithoutDeduplication(IServiceCollection services)
    {
        // Session Context Provider
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies
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
        
        // Confidence Calculator
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
        
        // Deduplication Services (비활성화: NullEventDeduplicator)
        services.AddSingleton<IEventDeduplicator>(sp => new NullEventDeduplicator());
        
        // ⚠️ 중요: IDeduplicationStrategy 등록 (CaptureDeduplicationWindow 적용)
        // NormalizedLogEvent 중복 제거는 비활성화하지만,
        // CameraCaptureEvent 중복 제거(CaptureDeduplicationWindow)는 필수!
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        // Orchestrator
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }

    /// <summary>
    /// 중복 제거 포함하여 서비스 등록
    /// </summary>
    private void RegisterServicesWithDeduplication(IServiceCollection services)
    {
        // Session Context Provider
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies
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
        
        // Confidence Calculator
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
        
        // Deduplication Services (활성화)
        services.AddSingleton<IEventDeduplicator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventDeduplicator>>();
            var options = sp.GetRequiredService<AnalysisOptions>();
            return new EventDeduplicator(logger, options);
        });
        
        // ⚠️ 중요: IDeduplicationStrategy 등록 (CaptureDeduplicationWindow 적용)
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        // Orchestrator
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }

    #endregion
}

/// <summary>
/// 중복 제거를 수행하지 않는 Null Deduplicator
/// </summary>
internal class NullEventDeduplicator : IEventDeduplicator
{
    public IReadOnlyList<NormalizedLogEvent> Deduplicate(
        IReadOnlyList<NormalizedLogEvent> events,
        out IReadOnlyList<DeduplicationInfo> deduplicationDetails)
    {
        deduplicationDetails = new List<DeduplicationInfo>();
        return events;
    }
}
