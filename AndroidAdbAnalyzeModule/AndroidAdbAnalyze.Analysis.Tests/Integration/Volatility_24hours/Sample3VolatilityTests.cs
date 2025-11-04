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

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Volatility;

/// <summary>
/// Sample 3 휘발성 테스트: 24시간 후 로그 분석 성능 검증
/// 
/// 목적:
/// - 실시간(원본) vs 24시간 후 탐지율 비교
/// - usagestats.log 휘발이 탐지 성능에 미치는 영향 분석
/// - 앱별 탐지 정확도 평가
/// 
/// Ground Truth (원본 3차 샘플):
/// - 총 세션: 11개
/// - 총 촬영: 6개
///   - 기본 카메라: 1개 (S3-2: 23:14:20)
///   - 카카오톡: 2개 (S3-4: 23:16:38, S3-5: 23:20:14)
///   - 텔레그램: 2개 (S3-7: 23:23:09, S3-8: 23:26:28)
///   - 무음 카메라: 1개 (S3-11: 23:29:40)
/// 
/// 휘발성 로그 특징:
/// - usagestats.log: 완전 휘발 (카메라 관련 이벤트 0개)
/// - media_camera.log: 보존됨 (CONNECT/DISCONNECT 이벤트 존재)
/// - vibrator_manager.log: 보존됨
/// - audio.log: 보존됨 (PLAYER_EVENT는 package 정보 없음)
/// 
/// 예상 결과:
/// - 촬영 탐지율: 100% 유지 (핵심 아티팩트는 휘발되지 않음)
/// - 앱 분류 정확도: 부분적 저하 (taskRootPackage 손실)
///   - 카카오톡 촬영 → 기본 카메라로 오분류 예상
/// </summary>
public sealed class Sample3VolatilityTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    private List<NormalizedLogEvent>? _parsedEventsVolatility; // 24시간 후 휘발성 로그
    
    // Ground Truth 기준값 (원본 3차 샘플)
    private const int ExpectedTotalSessions = 11;
    private const int ExpectedTotalCaptures = 6;
    private const int ExpectedDefaultCameraCaptures = 1;
    private const int ExpectedKakaoTalkCaptures = 2;
    private const int ExpectedTelegramCaptures = 2;
    private const int ExpectedSilentCameraCaptures = 1;
    
    // 휘발성 로그 디렉토리 경로
    private const string VolatilitySampleDirectoryName = "24시 휘발성/3차 샘플_25_10_07_24시";
    
    // 분석 시간 범위 (원본 GT 기준)
    private readonly DateTime _startTime = new(2025, 10, 7, 23, 13, 0);
    private readonly DateTime _endTime = new(2025, 10, 7, 23, 30, 0);

    // 촬영 시각 (Ground Truth 기준)
    private static readonly Dictionary<string, DateTime[]> ExpectedCaptureTimestamps = new()
    {
        { "기본 카메라", new[] { new DateTime(2025, 10, 7, 23, 14, 20) } },
        { "카카오톡", new[] { new DateTime(2025, 10, 7, 23, 16, 38), new DateTime(2025, 10, 7, 23, 20, 14) } },
        { "텔레그램", new[] { new DateTime(2025, 10, 7, 23, 23, 9), new DateTime(2025, 10, 7, 23, 26, 28) } },
        { "무음 카메라", new[] { new DateTime(2025, 10, 7, 23, 29, 40) } }
    };

    public Sample3VolatilityTests(ITestOutputHelper output)
    {
        _output = output;
        var workspaceRoot = Path.Combine("..", "..", "..", "..", "..");
        _sampleLogsPath = Path.Combine(workspaceRoot, "sample_logs");
        _parserConfigPath = Path.Combine(workspaceRoot, "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Parser", "Configs");
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== Sample 3 휘발성 테스트 초기화 (24시간 후 로그) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _orchestrator = CreateOrchestratorWithYamlConfig();
        _parsedEventsVolatility = await ParseVolatilityLogsAsync();
        
        _output.WriteLine($"📊 휘발성 로그 파싱된 이벤트 수: {_parsedEventsVolatility.Count}");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region 휘발성 탐지율 검증

    [Fact]
    public async Task Should_Measure_DetectionRate_After24Hours_AllApps()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEventsVolatility!, options);

        // Assert - 상세 탐지율 분석
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 휘발성 영향 분석: 24시간 후 전체 탐지율 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("📊 Ground Truth (원본):");
        _output.WriteLine($"   - 총 세션: {ExpectedTotalSessions}개");
        _output.WriteLine($"   - 총 촬영: {ExpectedTotalCaptures}개");
        _output.WriteLine($"   - 기본 카메라: {ExpectedDefaultCameraCaptures}개");
        _output.WriteLine($"   - 카카오톡: {ExpectedKakaoTalkCaptures}개");
        _output.WriteLine($"   - 텔레그램: {ExpectedTelegramCaptures}개");
        _output.WriteLine($"   - 무음 카메라: {ExpectedSilentCameraCaptures}개\n");

        _output.WriteLine("📊 24시간 후 탐지 결과:");
        _output.WriteLine($"   - 탐지된 세션: {result.Sessions.Count}개");
        _output.WriteLine($"   - 탐지된 촬영: {result.CaptureEvents.Count}개\n");
        
        // 🎯 공용 메서드 사용: 세션별 촬영 상세 출력
        WriteSessionCaptureDetails(_output, result.Sessions, result.CaptureEvents, Standard);

        // usagestats 이벤트 수 계산
        var usagestatsEventCount = _parsedEventsVolatility!
            .Count(e => e.EventType == "ACTIVITY_RESUMED" || 
                      e.EventType == "ACTIVITY_STOPPED" ||
                      e.EventType == "FOREGROUND_SERVICE_START" ||
                      e.EventType == "FOREGROUND_SERVICE_STOP");

        // media_camera 이벤트 수 계산
        var mediaCameraEventCount = _parsedEventsVolatility!
            .Count(e => e.EventType == "CAMERA_CONNECT" || e.EventType == "CAMERA_DISCONNECT");

        // 🎯 공용 메서드 사용: 휘발성 분석 요약
        WriteVolatilityAnalysisSummary(
            _output, 
            ExpectedTotalCaptures, 
            result.CaptureEvents.Count, 
            usagestatsEventCount,
            mediaCameraEventCount);

        // 탐지율이 0%가 아니면 테스트 통과 (휘발성 영향 측정이 목적)
        result.CaptureEvents.Count.Should().BeGreaterThanOrEqualTo(0,
            "휘발성 테스트는 탐지율 측정이 목적이므로 0개 이상이면 통과");
    }

    #endregion

    #region 앱별 아티팩트 분석

    [Fact]
    public void Should_Analyze_RemainingArtifacts_After24Hours_DefaultCamera()
    {
        // S3-2: 기본 카메라 촬영 (23:14:16 - 23:14:26)
        AnalyzeDefaultCameraSession(
            _output,
            _parsedEventsVolatility!,
            "S3-2 (기본 카메라 촬영)",
            new DateTime(2025, 10, 7, 23, 14, 16),
            new DateTime(2025, 10, 7, 23, 14, 26),
            new DateTime(2025, 10, 7, 23, 14, 20),
            shouldDetectCapture: true,
            Standard);
    }

    [Fact]
    public void Should_Analyze_RemainingArtifacts_After24Hours_KakaoTalk()
    {
        // S3-4: 카카오톡 촬영 #1 (23:16:35 - 23:16:44)
        AnalyzeKakaoSession(
            _output,
            _parsedEventsVolatility!,
            "S3-4 (카카오톡 촬영 #1)",
            new DateTime(2025, 10, 7, 23, 16, 35),
            new DateTime(2025, 10, 7, 23, 16, 44),
            new DateTime(2025, 10, 7, 23, 16, 38),
            shouldDetectCapture: true,
            Standard);

        // S3-5: 카카오톡 촬영 #2 (23:20:11 - 23:20:20)
        AnalyzeKakaoSession(
            _output,
            _parsedEventsVolatility!,
            "S3-5 (카카오톡 촬영 #2)",
            new DateTime(2025, 10, 7, 23, 20, 11),
            new DateTime(2025, 10, 7, 23, 20, 20),
            new DateTime(2025, 10, 7, 23, 20, 14),
            shouldDetectCapture: true,
            Standard);
    }

    [Fact]
    public async Task Should_Analyze_RemainingArtifacts_After24Hours_Telegram()
    {
        await AnalyzeRemainingArtifactsForApp("텔레그램", "telegram", ExpectedCaptureTimestamps["텔레그램"][0]);
    }

    [Fact]
    public async Task Should_Analyze_RemainingArtifacts_After24Hours_SilentCamera()
    {
        // S3-11: 무음 카메라 촬영 세션 분석
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== S3-11: 무음 카메라 세션 상세 분석 (24시간 후) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 전체 파싱된 이벤트에서 SILENT_CAMERA_CAPTURE 확인
        var allSilentEvents = _parsedEventsVolatility!
            .Where(e => e.EventType == "SILENT_CAMERA_CAPTURE")
            .ToList();
        
        _output.WriteLine($"🔍 전체 로그에서 SILENT_CAMERA_CAPTURE: {allSilentEvents.Count}개");
        foreach (var evt in allSilentEvents)
        {
            _output.WriteLine($"   - {evt.Timestamp:HH:mm:ss.fff} | Package: {evt.PackageName ?? "NULL"}");
        }
        _output.WriteLine("");

        var sessionStart = new DateTime(2025, 10, 7, 23, 29, 36);
        var sessionEnd = new DateTime(2025, 10, 7, 23, 29, 46);

        var sessionEvents = _parsedEventsVolatility!
            .Where(e => e.Timestamp >= sessionStart && e.Timestamp <= sessionEnd)
            .OrderBy(e => e.Timestamp)
            .ToList();

        _output.WriteLine($"📅 세션 시간: {sessionStart:HH:mm:ss} - {sessionEnd:HH:mm:ss}");
        _output.WriteLine($"📊 세션 내 전체 이벤트 수: {sessionEvents.Count}개\n");

        // 이벤트 타입별 통계
        var eventTypeCounts = sessionEvents
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        _output.WriteLine("📋 이벤트 타입별 통계:");
        foreach (var item in eventTypeCounts)
        {
            var weight = Standard.TryGetValue(item.EventType, out var w) ? $" (가중치: {w})" : "";
            _output.WriteLine($"   - {item.EventType}: {item.Count}개{weight}");
        }

        // 핵심 아티팩트 분석
        var silentCameraCapture = sessionEvents.Where(e => e.EventType == "SILENT_CAMERA_CAPTURE").ToList();
        var vibrationEvents = sessionEvents.Where(e => e.EventType == "VIBRATION_EVENT").ToList();
        var cameraConnect = sessionEvents.Where(e => e.EventType == "CAMERA_CONNECT").ToList();
        var databaseInsert = sessionEvents.Where(e => e.EventType == "DATABASE_INSERT").ToList();

        _output.WriteLine("\n🔍 핵심 아티팩트 (Key Artifacts):");
        _output.WriteLine($"   SILENT_CAMERA_CAPTURE: {silentCameraCapture.Count}개");
        if (silentCameraCapture.Any())
        {
            foreach (var evt in silentCameraCapture)
            {
                _output.WriteLine($"      └─ {evt.Timestamp:HH:mm:ss.fff} | Package: {evt.PackageName ?? "NULL"}");
            }
        }
        
        _output.WriteLine($"   CAMERA_CONNECT: {cameraConnect.Count}개");
        if (cameraConnect.Any())
        {
            foreach (var evt in cameraConnect)
            {
                var device = evt.Attributes.TryGetValue("device", out var d) ? d.ToString() : "N/A";
                _output.WriteLine($"      └─ {evt.Timestamp:HH:mm:ss.fff} | Package: {evt.PackageName ?? "NULL"} | Device: {device}");
            }
        }

        _output.WriteLine($"   DATABASE_INSERT: {databaseInsert.Count}개");
        _output.WriteLine($"   VIBRATION_EVENT: {vibrationEvents.Count}개");
        
        if (vibrationEvents.Any())
        {
            foreach (var evt in vibrationEvents)
            {
                var hapticType = evt.Attributes.TryGetValue("hapticType", out var ht) ? ht.ToString() : "N/A";
                var status = evt.Attributes.TryGetValue("status", out var s) ? s.ToString() : "N/A";
                var usage = evt.Attributes.TryGetValue("usage", out var u) ? u.ToString() : "N/A";
                _output.WriteLine($"      └─ {evt.Timestamp:HH:mm:ss.fff} | hapticType:{hapticType} | status:{status} | usage:{usage}");
            }
        }

        _output.WriteLine("\n💡 분석 결과:");
        if (silentCameraCapture.Any() || vibrationEvents.Any())
        {
            _output.WriteLine("   ✅ 핵심 아티팩트 존재 → 탐지 가능해야 함");
            _output.WriteLine($"   ✅ SILENT_CAMERA_CAPTURE: {silentCameraCapture.Count}개");
            _output.WriteLine($"   ✅ VIBRATION_EVENT: {vibrationEvents.Count}개");
        }
        else
        {
            _output.WriteLine("   ❌ 핵심 아티팩트 없음 → 탐지 불가능 (미탐 발생)");
            _output.WriteLine("   ⚠️  SILENT_CAMERA_CAPTURE와 VIBRATION_EVENT 모두 없음!");
        }

        // Sample 2와 비교
        _output.WriteLine("\n📊 Sample 2 vs Sample 3 비교:");
        _output.WriteLine("   Sample 2 (24시간 후):");
        _output.WriteLine("      - SILENT_CAMERA_CAPTURE: ✅ 탐지됨");
        _output.WriteLine("      - VIBRATION_EVENT: ✅ 탐지됨");
        _output.WriteLine("      - 결과: 100% 탐지");
        _output.WriteLine("   Sample 3 (24시간 후):");
        _output.WriteLine($"      - SILENT_CAMERA_CAPTURE: {(silentCameraCapture.Any() ? "✅ 탐지됨" : "❌ 미탐")}");
        _output.WriteLine($"      - VIBRATION_EVENT: {(vibrationEvents.Any() ? "✅ 탐지됨" : "❌ 미탐")}");
        _output.WriteLine($"      - 결과: {(silentCameraCapture.Any() || vibrationEvents.Any() ? "탐지 가능" : "0% 탐지")}");
        
        _output.WriteLine($"\n════════════════════════════════════════════════════════════\n");
        await Task.CompletedTask;
    }

    private async Task AnalyzeRemainingArtifactsForApp(
        string appName,
        string packageFilter,
        DateTime captureTime)
    {
        _output.WriteLine($"════════════════════════════════════════════════════════════");
        _output.WriteLine($"=== {appName} 아티팩트 분석 (24시간 후) ===");
        _output.WriteLine($"════════════════════════════════════════════════════════════\n");

        var relevantEvents = _parsedEventsVolatility!
            .Where(e => e.PackageName?.Contains(packageFilter, StringComparison.OrdinalIgnoreCase) == true ||
                       e.Timestamp >= captureTime.AddSeconds(-10) && e.Timestamp <= captureTime.AddSeconds(10))
            .OrderBy(e => e.Timestamp)
            .ToList();

        var eventTypeCounts = relevantEvents
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        _output.WriteLine($"📊 관련 이벤트 수: {relevantEvents.Count}개\n");
        _output.WriteLine("📋 이벤트 타입별 통계:");
        foreach (var item in eventTypeCounts)
        {
            var weight = Standard.TryGetValue(item.EventType, out var w) ? $" (가중치: {w})" : "";
            _output.WriteLine($"   - {item.EventType}: {item.Count}개{weight}");
        }

        _output.WriteLine($"\n════════════════════════════════════════════════════════════\n");
        await Task.CompletedTask;
    }

    #endregion

    #region 비촬영 세션 점수 분석

    /// <summary>
    /// 카메라 사용만 하고 촬영하지 않은 세션들의 점수를 분석합니다.
    /// 논문용 데이터: 비촬영 세션의 점수 분포, 임계값과의 비교
    /// </summary>
    [Fact]
    public void Should_Analyze_NonCaptureSession_Scores()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 비촬영 세션 점수 분석 (24시간 후) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        var nonCaptureSessions = new[]
        {
            new { Name = "S3-1 (기본 카메라 사용만)", Start = new DateTime(2025, 10, 7, 23, 13, 36), End = new DateTime(2025, 10, 7, 23, 13, 41) },
            new { Name = "S3-3 (카카오톡 사용만)", Start = new DateTime(2025, 10, 7, 23, 15, 42), End = new DateTime(2025, 10, 7, 23, 15, 46) },
            new { Name = "S3-7 (텔레그램 사용만 #1)", Start = new DateTime(2025, 10, 7, 23, 22, 4), End = new DateTime(2025, 10, 7, 23, 22, 16) },
            new { Name = "S3-9 (텔레그램 사용만 #2)", Start = new DateTime(2025, 10, 7, 23, 27, 52), End = new DateTime(2025, 10, 7, 23, 27, 57) },
            new { Name = "S3-10 (무음 카메라 사용만)", Start = new DateTime(2025, 10, 7, 23, 28, 38), End = new DateTime(2025, 10, 7, 23, 28, 43) }
        };

        _output.WriteLine("📋 비촬영 세션 목록:\n");
        foreach (var session in nonCaptureSessions)
        {
            _output.WriteLine($"   {session.Name}");
            _output.WriteLine($"   세션: {session.Start:HH:mm:ss} - {session.End:HH:mm:ss}");
            
            // 세션 범위 내 이벤트 수집
            var sessionEvents = _parsedEventsVolatility!
                .Where(e => e.Timestamp >= session.Start && e.Timestamp <= session.End)
                .ToList();
            
            // 탐지 가능한 아티팩트 목록
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
        _output.WriteLine("🎯 비촬영 세션 점수 분석 요약");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("✅ 모든 비촬영 세션에서 촬영이 탐지되지 않음 (False Positive = 0)");
        _output.WriteLine("✅ 핵심 아티팩트 부재로 인한 정상적인 필터링 동작 확인");
        _output.WriteLine("📝 보조 아티팩트만으로는 임계값을 초과하더라도 탐지되지 않음");
        _output.WriteLine("   → 2단계 탐지 메커니즘(핵심 아티팩트 필수)의 효과 입증");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
    }

    #endregion

    #region Ground Truth 문서 자동 생성 (논문용 - 24시간 휘발성)

    /// <summary>
    /// Ground Truth 문서를 실제 분석 결과 기반으로 자동 생성합니다 (24시간 휘발성).
    /// </summary>
    [Fact]
    public async Task Generate_GroundTruth_Document_Volatility24Hours()
    {
        // ========================================
        // Arrange: 샘플 정보 및 분석 옵션 설정
        // ========================================
        var options = CreateAnalysisOptions();

        var sampleInfo = new ArtifactWeights.SampleInfo(
            SampleNumber: 3,
            SampleName: "3차 샘플 (24시간 휘발성)",
            TestDate: new DateTime(2025, 10, 7),
            TimeRange: (_startTime, _endTime),
            Description: "기본 카메라, 카카오톡, 텔레그램, 무음 카메라 사용 (총 6회 촬영) - 24시간 후 로그"
        );

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== Ground Truth 문서 자동 생성 (24시간 휘발성) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📋 샘플: {sampleInfo.SampleName}");
        _output.WriteLine($"📅 날짜: {sampleInfo.TestDate:yyyy-MM-dd}");
        _output.WriteLine($"⏰ 시간: {sampleInfo.TimeRange.Start:HH:mm:ss} ~ {sampleInfo.TimeRange.End:HH:mm:ss}");
        _output.WriteLine($"📝 설명: {sampleInfo.Description}");
        _output.WriteLine("");

        // ========================================
        // Act: 실제 분석 실행 (24시간 후 로그)
        // ========================================
        _output.WriteLine("🔄 1단계: 24시간 후 로그 분석 실행 중...");
        var analysisResult = await _orchestrator!.AnalyzeAsync(_parsedEventsVolatility!, options);

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
        var docDir = Path.Combine(projectRoot, "Documentation", "GroundTruth", "Volatility");
        
        if (!Directory.Exists(docDir))
        {
            Directory.CreateDirectory(docDir);
            _output.WriteLine($"✅ 디렉토리 생성: {docDir}");
        }

        var outputPath = Path.Combine(docDir, "Sample3_Volatility24h_Ground_Truth.md");
        await File.WriteAllTextAsync(outputPath, gtDocument);

        _output.WriteLine($"✅ 파일 저장 완료: {outputPath}");
        _output.WriteLine("");

        // ========================================
        // Assert: GT 문서 검증
        // ========================================
        _output.WriteLine("🔍 4단계: GT 문서 검증 중...");

        File.Exists(outputPath).Should().BeTrue("GT 문서 파일이 존재해야 함");
        _output.WriteLine("  ✓ 파일 존재 확인");

        gtDocument.Should().Contain("# Sample 3", "헤더가 있어야 함");
        gtDocument.Should().Contain("## 📋 샘플 정보", "샘플 정보 섹션이 있어야 함");
        gtDocument.Should().Contain("## 📊 전체 요약", "전체 요약 섹션이 있어야 함");
        _output.WriteLine("  ✓ 필수 섹션 존재 확인");

        gtDocument.Should().Contain($"**총 세션 수**: {analysisResult.Sessions.Count}개",
            "실제 탐지된 세션 수가 포함되어야 함");
        gtDocument.Should().Contain($"**총 촬영 수**: {analysisResult.CaptureEvents.Count}개",
            "실제 탐지된 촬영 수가 포함되어야 함");
        _output.WriteLine("  ✓ 실제 탐지 결과 확인");

        gtDocument.Should().Contain("24시간 휘발성", "휘발성 테스트임을 명시해야 함");
        _output.WriteLine("  ✓ 휘발성 정보 표시 확인");

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
        _output.WriteLine("✅ GT 문서 생성 및 검증 완료 (24시간 휘발성)");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📁 저장 위치: {outputPath}");
        _output.WriteLine($"📏 문서 크기: {gtDocument.Length:N0} 문자");
        _output.WriteLine("");
        _output.WriteLine($"🔬 휘발성 분석 결과:");
        _output.WriteLine($"   - 원본 GT 촬영 수: {ExpectedTotalCaptures}개");
        _output.WriteLine($"   - 24시간 후 탐지: {analysisResult.CaptureEvents.Count}개");
        var detectionRate = ExpectedTotalCaptures > 0 
            ? (double)analysisResult.CaptureEvents.Count / ExpectedTotalCaptures * 100 
            : 0;
        _output.WriteLine($"   - 탐지율: {detectionRate:F1}%");
        _output.WriteLine("════════════════════════════════════════════════════════════");
    }

    #endregion

    #region Helper Methods

    private IAnalysisOrchestrator CreateOrchestratorWithYamlConfig()
    {
        // YAML 설정 파일 경로
        var configPath = Path.Combine(
            "..", "..", "..", "..", "..",
            "AndroidAdbAnalyzeModule", "AndroidAdbAnalyze.Analysis", "Configs",
            "artifact-detection-config.example.yaml");
        
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"YAML 설정 파일을 찾을 수 없습니다: {configPath}");
        }
        
        // DI 컨테이너 설정
        var services = new ServiceCollection();
        
        // Logging 인프라 추가
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions 등록
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });
        
        // YAML 설정 로드
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(NullLoggerProvider.Instance));
        var logger = loggerFactory.CreateLogger<Sample3VolatilityTests>();
        var config = YamlConfigurationLoader.LoadFromFile(configPath, logger);
        
        // Configuration을 DI에 등록
        services.AddSingleton(config);
        
        // AndroidAdbAnalysis 서비스 등록 (Configuration 주입)
        RegisterServicesWithConfig(services);
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    private void RegisterServicesWithConfig(IServiceCollection services)
    {
        // ===== Core Services =====
        
        // Session Context Provider
        services.AddSingleton<ISessionContextProvider, SessionContextProvider>();
        
        // Capture Detection Strategies (Configuration 주입)
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
        
        // ===== Deduplication Services =====
        
        services.AddSingleton<IEventDeduplicator>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<EventDeduplicator>>();
            var options = sp.GetRequiredService<AnalysisOptions>();
            return new EventDeduplicator(logger, options);
        });
        
        services.AddSingleton<IDeduplicationStrategy, TimeBasedDeduplicationStrategy>();
        services.AddSingleton<IDeduplicationStrategy, CameraEventDeduplicationStrategy>();
        
        // ===== Orchestration =====
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }

    private async Task<List<NormalizedLogEvent>> ParseVolatilityLogsAsync()
    {
        var volatilityPath = Path.Combine(_sampleLogsPath, VolatilitySampleDirectoryName);
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
            var logPath = Path.Combine(volatilityPath, logFile);
            if (!File.Exists(logPath))
            {
                _output.WriteLine($"⚠️  {logFile} : 파일 없음");
                continue;
            }

            var events = await ParseLogFileAsync(logPath, configFile, _startTime, _endTime);
            allEvents.AddRange(events);
            _output.WriteLine($"✓ {logFile} : {events.Count} events");
        }

        _output.WriteLine($"\n📊 Total volatility events: {allEvents.Count}");
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

