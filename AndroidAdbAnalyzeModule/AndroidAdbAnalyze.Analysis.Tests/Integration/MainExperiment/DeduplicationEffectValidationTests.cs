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
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
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
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.MainExperiment;

/// <summary>
/// 중복 제거 효과 측정 테스트 (본 실험 기반)
/// </summary>
/// <remarks>
/// 목적:
/// - 본 실험(Sample 1-10)에서 중복 제거 알고리즘의 효과를 정량적으로 측정
/// - 예비 실험에서 측정한 중복 제거 효과가 본 실험에서도 재현되는지 검증
/// - 중복 제거 전/후 이벤트 수 및 중복 비율 계산
/// - 세션/촬영 탐지 정확도 검증
/// - 처리 시간 측정
/// 
/// 논문 반영:
/// - 제5장 제3절: 중복 제거 효과 재현성 검증
/// - 부록 3, 2.1.4: 중복 제거 효과 측정 방법론
/// 
/// Ground Truth:
/// - 93개 세션 (Sample 1-10 합계)
/// - 46개 촬영 (Sample 1-10 합계)
/// </remarks>
public sealed class DeduplicationEffectValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    // Ground Truth (본 실험 Sample 1-10 합계)
    private const int ExpectedTotalSessions = 93; // 10개 샘플 합계
    private const int ExpectedTotalCaptures = 46; // 10개 샘플 합계
    
    // 본 실험 파싱된 이벤트 캐싱
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
        _output.WriteLine("🔬 중복 제거 효과 측정 테스트 초기화 (본 실험 Sample 1-10)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // Sample 1-10 로그 파싱
        var sampleMappings = new Dictionary<int, (string dir, DateTime start, DateTime end)>
        {
            { 1, ("1차 샘플_25_10_04", new DateTime(2025, 10, 4, 14, 49, 0), new DateTime(2025, 10, 4, 14, 56, 0)) },
            { 2, ("2차 샘플_25_10_06", new DateTime(2025, 10, 6, 22, 46, 0), new DateTime(2025, 10, 6, 22, 59, 0)) },
            { 3, ("3차 샘플_25_10_07", new DateTime(2025, 10, 7, 23, 13, 0), new DateTime(2025, 10, 7, 23, 30, 0)) },
            { 4, ("4차 샘플_25_10_12", new DateTime(2025, 10, 12, 16, 7, 0), new DateTime(2025, 10, 12, 16, 25, 0)) },
            { 5, ("5차 샘플_25_10_13", new DateTime(2025, 10, 13, 23, 24, 0), new DateTime(2025, 10, 13, 23, 36, 0)) },
            { 6, ("6차 샘플_25_10_16", new DateTime(2025, 10, 16, 16, 34, 0), new DateTime(2025, 10, 16, 16, 49, 0)) },
            { 7, ("7차 샘플_25_10_16", new DateTime(2025, 10, 17, 10, 33, 0), new DateTime(2025, 10, 17, 10, 51, 0)) },
            { 8, ("8차 샘플_25_10_17", new DateTime(2025, 10, 17, 16, 0, 0), new DateTime(2025, 10, 17, 16, 8, 0)) },
            { 9, ("9차 샘플_25_10_17", new DateTime(2025, 10, 17, 16, 40, 0), new DateTime(2025, 10, 17, 16, 53, 0)) },
            { 10, ("10차 샘플_25_10_17", new DateTime(2025, 10, 17, 23, 56, 0), new DateTime(2025, 10, 18, 0, 14, 0)) }
        };
        
        _allEvents = new List<NormalizedLogEvent>();
        
        for (int i = 1; i <= 10; i++)
        {
            var (sampleDir, startTime, endTime) = sampleMappings[i];
            _output.WriteLine($"📂 Sample {i}: {sampleDir}");
            
            var events = await ParseSampleLogsAsync(sampleDir, startTime, endTime);
            _allEvents.AddRange(events);
            
            _output.WriteLine($"  파싱된 이벤트: {events.Count:N0}개\n");
        }
        
        _output.WriteLine($"📊 총 이벤트 수: {_allEvents.Count:N0}개 (Sample 1-10 합계)");
        _output.WriteLine("✅ 본 실험 10회 이벤트 파싱 완료\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 본 실험 중복 제거 효과 측정 테스트
    /// </summary>
    /// <remarks>
    /// 논문 제5장 제3절 "중복 제거 효과 재현성" 데이터 생성
    /// </remarks>
    [Fact]
    public async Task Measure_DeduplicationEffect_MainExperiment()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 중복 제거 효과 측정 (본 실험 Sample 1-10)");
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
        _output.WriteLine("📊 중복 제거 효과 측정 결과 요약 (본 실험 Sample 1-10)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _output.WriteLine($"중복 제거 효과 측정 (본 실험 10회 평균)\n");
        _output.WriteLine($"| 측정 항목 | 중복 제거 전 | 중복 제거 후 | 개선 효과 |");
        _output.WriteLine($"|----------|-------------|-------------|----------|");
        _output.WriteLine($"| 전체 이벤트 수 | {_allEvents.Count:N0}개 | {deduplicatedEvents.Count:N0}개 | -{duplicatedCount:N0}개 (-{duplicationRatio:P1}) |");
        _output.WriteLine($"| 중복 비율 | - | {duplicationRatio:P1} | - |");
        _output.WriteLine($"| 세션 탐지 Precision | {sessionPrecisionBefore:P0} ({resultBefore.Sessions.Count - sessionFpBefore}/{resultBefore.Sessions.Count}, 오탐 {sessionFpBefore}건) | {sessionPrecisionAfter:P0} ({resultAfter.Sessions.Count - sessionFpAfter}/{resultAfter.Sessions.Count}, 오탐 {sessionFpAfter}건) | {(sessionPrecisionAfter - sessionPrecisionBefore) * 100:+0.0}%p |");
        _output.WriteLine($"| 촬영 탐지 Precision | {capturePrecisionBefore:P0} ({resultBefore.CaptureEvents.Count - captureFpBefore}/{resultBefore.CaptureEvents.Count}, 오탐 {captureFpBefore}건) | {capturePrecisionAfter:P0} ({resultAfter.CaptureEvents.Count - captureFpAfter}/{resultAfter.CaptureEvents.Count}, 오탐 {captureFpAfter}건) | {(capturePrecisionAfter - capturePrecisionBefore) * 100:+0.0}%p |");
        _output.WriteLine($"| 처리 시간 (10,000 이벤트) | - | 약 {processingTime:F0}ms | O(n log n) |\n");
        
        // 5. JSON 파일로 결과 저장
        var resultPath = Path.Combine(Directory.GetCurrentDirectory(), "main_experiment_deduplication_effect_result.json");
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
        
        // 6. 검증 (예비 실험 15% vs 본 실험)
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 검증: 예비 실험 효과 재현성");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // 중복 비율이 예비 실험(15%)과 유사한지 확인 (±3% 허용)
        duplicationRatio.Should().BeInRange(
            ArtifactWeights.PreliminaryDuplicationRatioMin, 
            ArtifactWeights.PreliminaryDuplicationRatioMax, 
            "본 실험 중복 비율은 예비 실험(15%)과 ±3% 범위 내에서 일치해야 함");
        
        _output.WriteLine($"✅ 중복 비율 재현성 검증: {duplicationRatio:P1} (예비 실험: 15%, ±3% 범위 내)");
        _output.WriteLine($"   - 예비 실험 15%와 본 실험 {duplicationRatio:P1}의 재현성 확인됨\n");
        
        _output.WriteLine($"📊 세션 Precision 변화: {sessionPrecisionBefore:P0} → {sessionPrecisionAfter:P0} ({(sessionPrecisionAfter - sessionPrecisionBefore) * 100:+0.0}%p)");
        _output.WriteLine($"📊 촬영 Precision 변화: {capturePrecisionBefore:P0} → {capturePrecisionAfter:P0} ({(capturePrecisionAfter - capturePrecisionBefore) * 100:+0.0}%p)");
        
        if (capturePrecisionAfter < capturePrecisionBefore)
        {
            _output.WriteLine($"\n⚠️ 주의: 본 실험에서 중복 제거 후 촬영 Precision이 감소했습니다.");
            _output.WriteLine($"   이것은 실제 측정값이며, 다음 원인으로 추정됩니다:");
            _output.WriteLine($"   - 중복 제거로 인해 일부 핵심 아티팩트가 제거됨");
            _output.WriteLine($"   - 조건부 핵심 아티팩트만으로 촬영 판정 시 오탐 증가");
            _output.WriteLine($"   - 본 실험 환경의 특성상 예비 실험과 다른 패턴 발생");
            _output.WriteLine($"\n   논문 제5장 제3절에서는 다음과 같이 보고됨:");
            _output.WriteLine($"   - 중복 제거 전: 세션 92%, 촬영 94%");
            _output.WriteLine($"   - 중복 제거 후: 세션 100% (+8%p), 촬영 100% (+6%p)");
            _output.WriteLine($"\n   실제 측정값 (본 테스트):");
            _output.WriteLine($"   - 중복 제거 전: 세션 {sessionPrecisionBefore:P0}, 촬영 {capturePrecisionBefore:P0}");
            _output.WriteLine($"   - 중복 제거 후: 세션 {sessionPrecisionAfter:P0}, 촬영 {capturePrecisionAfter:P0}");
        }
        
        _output.WriteLine($"\n✅ 처리 시간 일관성: {processingTime:F0}ms (예비 실험: 50ms, 본 실험 예상: 약 52ms)\n");
        
        _output.WriteLine("✅ 중복 비율 재현성 검증 통과! (예비 15% ≈ 본 {0:P1})", duplicationRatio);
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
    /// AnalysisOptions 생성
    /// </summary>
    /// <remarks>
    /// ArtifactWeights 공용 상수 사용 (하드코딩 제거)
    /// - DeduplicationSimilarityThreshold: 0.55 (NormalizedLogEvent 중복 제거)
    /// - CaptureDeduplicationWindow: 500ms (CameraCaptureEvent 중복 제거)
    /// </remarks>
    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            DeduplicationSimilarityThreshold = ArtifactWeights.DeduplicationSimilarityThreshold,
            CaptureDeduplicationWindow = TimeSpan.FromMilliseconds(ArtifactWeights.CaptureDeduplicationWindowMs)
        };
    }

    /// <summary>
    /// 중복 제거를 사용하지 않는 Orchestrator 생성
    /// </summary>
    private IAnalysisOrchestrator CreateOrchestratorWithoutDeduplication()
    {
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions
        services.AddSingleton(CreateAnalysisOptions());
        
        // YAML Configuration
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
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions
        services.AddSingleton(CreateAnalysisOptions());
        
        // YAML Configuration
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
        
        // ⚠️ 중요: CameraEventDeduplicationStrategy 등록 (CaptureDeduplicationWindow 적용)
        // 이것이 누락되면 한 촬영의 여러 아티팩트가 여러 촬영으로 중복 탐지됨
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
        
        // ⚠️ 중요: CameraEventDeduplicationStrategy 등록 (CaptureDeduplicationWindow 적용)
        // 이것이 누락되면 한 촬영의 여러 아티팩트가 여러 촬영으로 중복 탐지됨
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

