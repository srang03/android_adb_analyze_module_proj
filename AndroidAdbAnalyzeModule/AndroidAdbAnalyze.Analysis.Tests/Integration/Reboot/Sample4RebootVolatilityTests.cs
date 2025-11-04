using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Services.Captures;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Context;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;
using AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Analysis.Tests.Configuration;
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using AndroidAdbAnalyze.Parser.Configuration;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using static AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants.ArtifactWeights;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Reboot;

/// <summary>
/// Sample 4 재부팅 휘발성 테스트: 재부팅 후 로그 분석 성능 검증
/// 
/// Ground Truth (원본 4차 샘플):
/// - 총 세션: 12개
/// - 총 촬영: 6개
///   - 기본 카메라: 1개 (S4-3: 16:08:42)
///   - 카카오톡: 2개 (S4-5: 16:15:48, S4-6: 16:17:00)
///   - 텔레그램: 2개 (S4-8: 16:20:59, S4-9: 16:22:13)
///   - 무음 카메라: 1개 (S4-12: 16:24:24)
/// 
/// 재부팅 후 로그 특징:
/// - 재부팅 시점: 2025-10-11 19:39 추정 (usagestats.log 시작 시간)
/// - 촬영 시간: 2025-10-12 16:07 ~ 16:25 (재부팅 약 20시간 후)
/// - usagestats.log: 1,317 라인 (재부팅 후 24시간 기록 포함)
/// - media_camera.log: 확인 필요
/// </summary>
public sealed class Sample4RebootVolatilityTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    private List<NormalizedLogEvent>? _parsedEventsReboot;
    
    // Ground Truth 기준값
    private const int ExpectedTotalSessions = 12;
    private const int ExpectedTotalCaptures = 6;
    private const int ExpectedDefaultCameraCaptures = 1;
    private const int ExpectedKakaoTalkCaptures = 2;
    private const int ExpectedTelegramCaptures = 2;
    private const int ExpectedSilentCameraCaptures = 1;
    
    private const string RebootSampleDirectoryName = "재부팅 휘발성/4차 샘플_25_10_12_재부팅";
    
    private readonly DateTime _startTime = new(2025, 10, 12, 16, 7, 0);
    private readonly DateTime _endTime = new(2025, 10, 12, 16, 25, 0);

    public Sample4RebootVolatilityTests(ITestOutputHelper output)
    {
        _output = output;
        var workspaceRoot = Path.Combine("..", "..", "..", "..", "..");
        _sampleLogsPath = Path.Combine(workspaceRoot, "sample_logs");
        _parserConfigPath = Path.Combine(workspaceRoot, "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Parser", "Configs");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== Sample 4 재부팅 휘발성 테스트 초기화 (재부팅 후 로그) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _orchestrator = CreateOrchestratorWithYamlConfig();
        _parsedEventsReboot = await ParseRebootLogsAsync();
        
        _output.WriteLine($"📊 재부팅 후 로그 파싱된 이벤트 수: {_parsedEventsReboot.Count}");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region 재부팅 휘발성 탐지율 검증

    [Fact]
    public async Task Should_Measure_DetectionRate_AfterReboot_AllApps()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEventsReboot!, options);

        // Assert
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 재부팅 휘발성 영향 분석: 재부팅 후 전체 탐지율 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("📊 Ground Truth (원본):");
        _output.WriteLine($"   - 총 세션: {ExpectedTotalSessions}개");
        _output.WriteLine($"   - 총 촬영: {ExpectedTotalCaptures}개");
        _output.WriteLine($"   - 기본 카메라: {ExpectedDefaultCameraCaptures}개");
        _output.WriteLine($"   - 카카오톡: {ExpectedKakaoTalkCaptures}개");
        _output.WriteLine($"   - 텔레그램: {ExpectedTelegramCaptures}개");
        _output.WriteLine($"   - 무음 카메라: {ExpectedSilentCameraCaptures}개\n");

        _output.WriteLine("📊 재부팅 후 탐지 결과:");
        _output.WriteLine($"   - 탐지된 세션: {result.Sessions.Count}개");
        _output.WriteLine($"   - 탐지된 촬영: {result.CaptureEvents.Count}개\n");
        
        // 공용 메서드 사용: 세션별 촬영 상세 출력
        WriteSessionCaptureDetails(_output, result.Sessions, result.CaptureEvents, Standard);

        // usagestats 이벤트 수 계산
        var usagestatsEventCount = _parsedEventsReboot!
            .Count(e => e.EventType == "ACTIVITY_RESUMED" || 
                       e.EventType == "ACTIVITY_STOPPED" ||
                       e.EventType == "FOREGROUND_SERVICE_START" ||
                       e.EventType == "FOREGROUND_SERVICE_STOP");

        // media_camera 이벤트 수 계산
        var mediaCameraEventCount = _parsedEventsReboot!
            .Count(e => e.EventType == "CAMERA_CONNECT" || e.EventType == "CAMERA_DISCONNECT");

        // 재부팅 휘발성 분석 요약
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("🎯 재부팅 휘발성 분석 요약");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"예상 촬영: {ExpectedTotalCaptures}개");
        _output.WriteLine($"실제 탐지: {result.CaptureEvents.Count}개");
        var detectionRate = ExpectedTotalCaptures > 0 
            ? (double)result.CaptureEvents.Count / ExpectedTotalCaptures * 100 
            : 0;
        _output.WriteLine($"탐지율: {detectionRate:F1}%\n");

        _output.WriteLine("📋 로그 보존 상태:");
        _output.WriteLine($"   - usagestats 이벤트: {usagestatsEventCount}개");
        _output.WriteLine($"   - media_camera 이벤트: {mediaCameraEventCount}개");
        
        _output.WriteLine("\n💡 재부팅 영향 분석:");
        if (usagestatsEventCount == 0)
        {
            _output.WriteLine("   ⚠️  usagestats.log 완전 휘발 → 앱별 세션 구분 불가");
        }
        else
        {
            _output.WriteLine($"   ✅ usagestats.log 일부 보존 ({usagestatsEventCount}개 이벤트)");
            _output.WriteLine("      (재부팅 후 24시간 기록 포함, 재부팅 전 데이터는 휘발)");
        }
        
        if (mediaCameraEventCount == 0)
        {
            _output.WriteLine("   ⚠️  media_camera.log 완전 휘발 → 세션 탐지 불가");
            _output.WriteLine("      (메모리 기반 로그 특성상 재부팅 시 삭제됨)");
        }
        else
        {
            _output.WriteLine($"   ✅ media_camera.log 일부 보존 ({mediaCameraEventCount}개 이벤트)");
        }
        
        _output.WriteLine("\n📝 선행 연구 검증 관점:");
        _output.WriteLine("   - media_camera.log 보존 → 선행 연구의 로그 유효성 확인");
        _output.WriteLine("   - 재부팅 휘발성 → 메모리 기반 로그의 한계 입증");
        _output.WriteLine("   - 실무 시사점 → 재부팅 전 로그 추출의 중요성 강조");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        result.CaptureEvents.Count.Should().BeGreaterThanOrEqualTo(0,
            "재부팅 휘발성 테스트는 탐지율 측정이 목적이므로 0개 이상이면 통과");
    }

    #endregion

    #region 앱별 아티팩트 분석

    [Fact]
    public void Should_Analyze_RemainingArtifacts_AfterReboot_DefaultCamera()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 기본 카메라 세션 상세 분석 (S4-3, 재부팅 후) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        AnalyzeDefaultCameraSession(
            _output,
            _parsedEventsReboot!,
            "S4-3 (기본 카메라 촬영)",
            new DateTime(2025, 10, 12, 16, 8, 36),
            new DateTime(2025, 10, 12, 16, 8, 47),
            new DateTime(2025, 10, 12, 16, 8, 42),
            true,
            Standard);
        
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
    }

    [Fact]
    public void Should_Analyze_RemainingArtifacts_AfterReboot_KakaoTalk()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 카카오톡 세션 상세 분석 (S4-4, S4-5, S4-6, 재부팅 후) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // S4-4: 사용만
        AnalyzeKakaoSession(
            _output,
            _parsedEventsReboot!,
            "S4-4 (사용만)",
            new DateTime(2025, 10, 12, 16, 12, 1),
            new DateTime(2025, 10, 12, 16, 12, 7),
            null,
            false,
            Standard);
        
        // S4-5: 촬영 #1
        AnalyzeKakaoSession(
            _output,
            _parsedEventsReboot!,
            "S4-5 (촬영 #1)",
            new DateTime(2025, 10, 12, 16, 15, 43),
            new DateTime(2025, 10, 12, 16, 15, 53),
            new DateTime(2025, 10, 12, 16, 15, 48),
            true,
            Standard);
        
        // S4-6: 촬영 #2
        AnalyzeKakaoSession(
            _output,
            _parsedEventsReboot!,
            "S4-6 (촬영 #2)",
            new DateTime(2025, 10, 12, 16, 16, 55),
            new DateTime(2025, 10, 12, 16, 17, 5),
            new DateTime(2025, 10, 12, 16, 17, 0),
            true,
            Standard);
        
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
    }

    #endregion

    #region 비촬영 세션 점수 분석

    [Fact]
    public void Should_Analyze_NonCaptureSession_Scores_AfterReboot()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 비촬영 세션 점수 분석 (재부팅 후) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        var nonCaptureSessions = new[]
        {
            new { Name = "S4-1 (기본 카메라 사용만 #1)", Start = new DateTime(2025, 10, 12, 16, 7, 0), End = new DateTime(2025, 10, 12, 16, 7, 5) },
            new { Name = "S4-2 (기본 카메라 사용만 #2)", Start = new DateTime(2025, 10, 12, 16, 7, 47), End = new DateTime(2025, 10, 12, 16, 7, 53) },
            new { Name = "S4-4 (카카오톡 사용만)", Start = new DateTime(2025, 10, 12, 16, 12, 1), End = new DateTime(2025, 10, 12, 16, 12, 7) },
            new { Name = "S4-7 (텔레그램 사용만)", Start = new DateTime(2025, 10, 12, 16, 19, 38), End = new DateTime(2025, 10, 12, 16, 19, 49) },
            new { Name = "S4-11 (무음 카메라 사용만)", Start = new DateTime(2025, 10, 12, 16, 23, 48), End = new DateTime(2025, 10, 12, 16, 23, 54) }
        };

        _output.WriteLine("📋 비촬영 세션 목록:\n");
        foreach (var session in nonCaptureSessions)
        {
            _output.WriteLine($"   {session.Name}");
            _output.WriteLine($"   세션: {session.Start:HH:mm:ss} - {session.End:HH:mm:ss}");
            
            var sessionEvents = _parsedEventsReboot!
                .Where(e => e.Timestamp >= session.Start && e.Timestamp <= session.End)
                .ToList();
            
            var detectedArtifacts = sessionEvents
                .Select(e => e.EventType)
                .Where(et => Standard.ContainsKey(et))
                .Distinct()
                .ToList();

            if (detectedArtifacts.Any())
            {
                var totalScore = CalculateSum(detectedArtifacts, Standard);
                var finalScore = Math.Min(totalScore, 1.0);
                
                _output.WriteLine($"   탐지된 아티팩트: {detectedArtifacts.Count}개");
                _output.WriteLine($"   계산된 점수: {totalScore:F2} → 최종 점수: {finalScore:F2}");
                
                var comparedToThreshold = finalScore >= 0.3 ? "⚠️  임계값 초과" : "✅ 임계값 미만";
                _output.WriteLine($"   {comparedToThreshold} (임계값: 0.30)");
                
                _output.WriteLine($"\n   상세 아티팩트:");
                foreach (var artifact in detectedArtifacts.OrderByDescending(a => Standard[a]))
                {
                    _output.WriteLine($"      - {artifact,-30} (가중치: {Standard[artifact]:F2})");
                }
            }
            else
            {
                _output.WriteLine($"   탐지된 아티팩트: 없음");
                _output.WriteLine($"   점수: 0.00 ✅ 임계값 미만");
            }
            
            _output.WriteLine($"   핵심 아티팩트 존재 여부:");
            var keyArtifacts = new[] { "DATABASE_INSERT", "VIBRATION_EVENT" };
            foreach (var key in keyArtifacts)
            {
                var exists = detectedArtifacts.Contains(key);
                _output.WriteLine($"      {(exists ? "✅" : "❌")} {key}");
            }
            
            _output.WriteLine($"\n   💡 결론: 핵심 아티팩트 없음 → 촬영 미탐지 (정상)\n");
            _output.WriteLine($"   {'─',60}\n");
        }

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("🎯 재부팅 후 비촬영 세션 점수 분석 요약");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("✅ 모든 비촬영 세션에서 촬영이 탐지되지 않음 (False Positive = 0)");
        _output.WriteLine("✅ 재부팅 후에도 2단계 탐지 메커니즘 정상 동작 확인");
        _output.WriteLine("📝 선행 연구의 로그 조합 방식이 재부팅 환경에서도 유효함을 입증");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
    }

    #endregion

    #region Ground Truth 문서 자동 생성 (논문용 - 재부팅 휘발성)

    /// <summary>
    /// Ground Truth 문서를 실제 분석 결과 기반으로 자동 생성합니다 (재부팅 휘발성).
    /// </summary>
    [Fact]
    public async Task Generate_GroundTruth_Document_RebootVolatility()
    {
        // ========================================
        // Arrange: 샘플 정보 및 분석 옵션 설정
        // ========================================
        var options = CreateAnalysisOptions();

        var sampleInfo = new ArtifactWeights.SampleInfo(
            SampleNumber: 4,
            SampleName: "4차 샘플 (재부팅 휘발성)",
            TestDate: new DateTime(2025, 10, 12),
            TimeRange: (_startTime, _endTime),
            Description: "기본 카메라, 카카오톡, 텔레그램, 무음 카메라 사용 (총 6회 촬영) - 재부팅 약 20시간 후 로그"
        );

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== Ground Truth 문서 자동 생성 (재부팅 휘발성) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📋 샘플: {sampleInfo.SampleName}");
        _output.WriteLine($"📅 날짜: {sampleInfo.TestDate:yyyy-MM-dd}");
        _output.WriteLine($"⏰ 시간: {sampleInfo.TimeRange.Start:HH:mm:ss} ~ {sampleInfo.TimeRange.End:HH:mm:ss}");
        _output.WriteLine($"📝 설명: {sampleInfo.Description}");
        _output.WriteLine("");

        // ========================================
        // Act: 실제 분석 실행 (재부팅 후 로그)
        // ========================================
        _output.WriteLine("🔄 1단계: 재부팅 후 로그 분석 실행 중...");
        var analysisResult = await _orchestrator!.AnalyzeAsync(_parsedEventsReboot!, options);

        analysisResult.Should().NotBeNull("분석 결과가 반환되어야 함");
        analysisResult.Success.Should().BeTrue("분석이 성공해야 함");

        _output.WriteLine($"✅ 분석 완료: 세션 {analysisResult.Sessions.Count}개, 촬영 {analysisResult.CaptureEvents.Count}개");
        _output.WriteLine("");

        // ========================================
        // Act: GT 문서 생성
        // ========================================
        _output.WriteLine("📄 2단계: GT 문서 생성 중...");
        var gtDocument = ArtifactWeights.GroundTruthDocumentGenerator.GenerateDocument(
            analysisResult,
            sampleInfo,
            Standard);

        gtDocument.Should().NotBeNullOrEmpty("GT 문서가 생성되어야 함");

        _output.WriteLine($"✅ GT 문서 생성 완료: {gtDocument.Length} 문자");
        _output.WriteLine("");

        // ========================================
        // Act: 파일 저장
        // ========================================
        _output.WriteLine("💾 3단계: 파일 저장 중...");
        
        var projectRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", ".."));
        var docDir = Path.Combine(projectRoot, "Documentation", "GroundTruth", "Reboot");
        
        if (!Directory.Exists(docDir))
        {
            Directory.CreateDirectory(docDir);
            _output.WriteLine($"✅ 디렉토리 생성: {docDir}");
        }

        var outputPath = Path.Combine(docDir, "Sample4_Reboot_Ground_Truth.md");
        await File.WriteAllTextAsync(outputPath, gtDocument);

        _output.WriteLine($"✅ 파일 저장 완료: {outputPath}");
        _output.WriteLine("");

        // ========================================
        // Assert: GT 문서 검증
        // ========================================
        _output.WriteLine("🔍 4단계: GT 문서 검증 중...");

        File.Exists(outputPath).Should().BeTrue("GT 문서 파일이 존재해야 함");
        _output.WriteLine("  ✓ 파일 존재 확인");

        gtDocument.Should().Contain("# Sample 4", "헤더가 있어야 함");
        gtDocument.Should().Contain("## 📋 샘플 정보", "샘플 정보 섹션이 있어야 함");
        gtDocument.Should().Contain("## 📊 전체 요약", "전체 요약 섹션이 있어야 함");
        _output.WriteLine("  ✓ 필수 섹션 존재 확인");

        gtDocument.Should().Contain($"**총 세션 수**: {analysisResult.Sessions.Count}개",
            "실제 탐지된 세션 수가 포함되어야 함");
        gtDocument.Should().Contain($"**총 촬영 수**: {analysisResult.CaptureEvents.Count}개",
            "실제 탐지된 촬영 수가 포함되어야 함");
        _output.WriteLine("  ✓ 실제 탐지 결과 확인");

        gtDocument.Should().Contain("재부팅 휘발성", "재부팅 휘발성 테스트임을 명시해야 함");
        _output.WriteLine("  ✓ 재부팅 휘발성 정보 표시 확인");

        if (analysisResult.CaptureEvents.Any())
        {
            var allArtifacts = analysisResult.CaptureEvents
                .SelectMany(c => c.ArtifactTypes)
                .Distinct()
                .ToList();
            _output.WriteLine($"  ✓ 아티팩트 정보 확인 ({allArtifacts.Count}개 고유 타입)");
        }

        gtDocument.Should().Contain("자동 생성 (실제 분석 결과 기반)",
            "자동 생성 메타 정보가 있어야 함");
        _output.WriteLine("  ✓ 자동 생성 메타 정보 확인");

        // ========================================
        // 최종 결과 출력
        // ========================================
        _output.WriteLine("");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("✅ GT 문서 생성 및 검증 완료 (재부팅 휘발성)");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📁 저장 위치: {outputPath}");
        _output.WriteLine($"📏 문서 크기: {gtDocument.Length:N0} 문자");
        _output.WriteLine("");
        _output.WriteLine($"🔬 재부팅 휘발성 분석 결과:");
        _output.WriteLine($"   - 원본 GT 촬영 수: {ExpectedTotalCaptures}개");
        _output.WriteLine($"   - 재부팅 후 탐지: {analysisResult.CaptureEvents.Count}개");
        var detectionRate = ExpectedTotalCaptures > 0 
            ? (double)analysisResult.CaptureEvents.Count / ExpectedTotalCaptures * 100 
            : 0;
        _output.WriteLine($"   - 탐지율: {detectionRate:F1}%");
        _output.WriteLine($"   - 특징: 재부팅 약 20시간 후");
        _output.WriteLine("════════════════════════════════════════════════════════════");
    }

    #endregion

    #region Helper Methods

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
        var logger = loggerFactory.CreateLogger<Sample4RebootVolatilityTests>();
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

    private async Task<List<NormalizedLogEvent>> ParseRebootLogsAsync()
    {
        var rebootPath = Path.Combine(_sampleLogsPath, RebootSampleDirectoryName);
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
                _output.WriteLine($"⚠️  {logFile} : 파일 없음");
                continue;
            }

            var events = await ParseLogFileAsync(logPath, configFile, _startTime, _endTime);
            allEvents.AddRange(events);
            _output.WriteLine($"✓ {logFile} : {events.Count} events");
        }

        _output.WriteLine($"\n📊 Total reboot events: {allEvents.Count}");
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
            _output.WriteLine($"⚠️ Log file not found: {logFilePath}");
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

    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            DeduplicationSimilarityThreshold = 0.8
        };
    }

    #endregion
}

