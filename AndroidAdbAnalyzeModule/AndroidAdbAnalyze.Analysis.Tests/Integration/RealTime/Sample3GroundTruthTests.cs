using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Options;
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
using AndroidAdbAnalyze.Parser.Configuration;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.RealTime;

/// <summary>
/// Sample 3 (3차 샘플) Ground Truth 검증 테스트
/// </summary>
/// <remarks>
/// 실제 로그 기반 Ground Truth (2025-10-07 23:13:00 ~ 23:30:00):
/// 
/// 기본 카메라 (PID 22548):
/// - 23:13:36-23:13:41 device 20 (촬영 없음)
/// - 23:14:16-23:14:26 device 20 (촬영 1개)
/// 
/// 카카오톡 (taskRootPackage=com.kakao.talk, PID 22548):
/// - 23:15:42-23:15:46 device 20 (촬영 없음)
/// - 23:16:35-23:16:44 device 20 (촬영 1개)
/// - 23:20:11-23:20:20 device 20 (촬영 1개)
/// 
/// 텔레그램 (PID 28971, 30147):
/// - 23:22:04-23:22:16 device 0 (후면, 촬영 없음)
/// - 23:22:59-23:23:41 device 0 (후면, 촬영 1개)
/// - 23:26:19-23:26:34 device 0 (후면, 촬영 1개)
/// - 23:27:52-23:27:57 device 0 (후면, 촬영 없음 - 앨범 전송)
/// 
/// 무음 카메라 (PID 31581):
/// - 23:28:38-23:28:43 device 0 (후면, 촬영 없음)
/// - 23:29:36-23:29:46 device 0 (후면, 촬영 1개)
/// 
/// 참고: 이 Samsung Galaxy 기기에서 device 0과 device 20은 모두 후면 카메라입니다.
/// 
/// Ground Truth (실제 로그 기반):
/// - 총 세션: 11개 (기본 카메라 2 + 카카오톡 3 + 텔레그램 4 + 무음 카메라 2)
/// - 총 촬영: 6개 (기본 카메라 1 + 카카오톡 2 + 텔레그램 2 + 무음 카메라 1)
/// </remarks>
public sealed class Sample3GroundTruthTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    private List<NormalizedLogEvent>? _parsedEvents;
    
    // Ground Truth 상수 (실제 로그 기반)
    private const int ExpectedTotalSessions = 11;
    private const int ExpectedTotalCaptures = 6;
    private const int ExpectedDefaultCameraCaptures = 1;
    private const int ExpectedKakaoTalkCaptures = 2;
    private const int ExpectedTelegramCaptures = 2;
    private const int ExpectedSilentCameraCaptures = 1;

    // 샘플 디렉토리 경로
    private const string SampleDirectoryName = "3차 샘플_25_10_07";
    
    // 분석 시간 범위
    private readonly DateTime _startTime = new(2025, 10, 7, 23, 13, 0);
    private readonly DateTime _endTime = new(2025, 10, 7, 23, 30, 0);

    // 아티팩트 가중치 (TestConstants에서 참조)
    private static readonly IReadOnlyDictionary<string, double> Weights = ArtifactWeights.Standard;
    
    public Sample3GroundTruthTests(ITestOutputHelper output)
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
        _output.WriteLine("=== Sample 3 (3차 샘플) Ground Truth 테스트 초기화 ===");
        
        // Orchestrator 생성 (YAML 설정 사용)
        _orchestrator = CreateOrchestratorWithYamlConfig();
        
        // 로그 파싱
        _parsedEvents = await ParseSampleLogsAsync();
        
        _output.WriteLine($"파싱된 이벤트 수: {_parsedEvents.Count}");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Ground Truth 검증

    [Fact]
    public async Task Should_Match_GroundTruth_TotalSessions()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        result.Sessions.Should().HaveCount(ExpectedTotalSessions,
            "실제 로그에 따르면 11개의 카메라 세션이 있어야 함 (기본 카메라 2 + 카카오톡 3 + 텔레그램 4 + 무음 카메라 2)");
        
        _output.WriteLine($"✓ 총 세션 수: {result.Sessions.Count} (예상: {ExpectedTotalSessions})");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_TotalCaptures()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        result.CaptureEvents.Should().HaveCount(ExpectedTotalCaptures,
            "데이터 시트에 따르면 6개의 사진 촬영이 있어야 함 (기본 카메라 1 + 카카오톡 2 + 텔레그램 2 + 무음 카메라 1)");
        
        _output.WriteLine($"✓ 총 촬영 수: {result.CaptureEvents.Count} (예상: {ExpectedTotalCaptures})");
        
        // 앱별 촬영 횟수 출력
        _output.WriteLine($"\n📊 앱별 촬영 횟수:");
        var capturesByApp = result.CaptureEvents
            .GroupBy(c => c.PackageName)
            .OrderByDescending(g => g.Count());
        
        foreach (var group in capturesByApp)
        {
            _output.WriteLine($"  {group.Key}: {group.Count()}개");
        }
    }

    [Fact]
    public async Task Should_Match_GroundTruth_DefaultCameraCaptures()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var defaultCameraCaptures = result.CaptureEvents
            .Count(c => c.PackageName?.Contains("com.sec.android.app.camera", StringComparison.OrdinalIgnoreCase) == true
                     && !c.PackageName.Contains("kakao", StringComparison.OrdinalIgnoreCase));
        
        defaultCameraCaptures.Should().Be(ExpectedDefaultCameraCaptures,
            "데이터 시트에 따르면 기본 카메라 촬영이 1개 있어야 함 (23:14:20)");

        _output.WriteLine($"✓ 기본 카메라 촬영 수: {defaultCameraCaptures} (예상: {ExpectedDefaultCameraCaptures})");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_KakaoTalkCaptures()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var kakaoCaptures = result.CaptureEvents
            .Count(c => c.PackageName?.Contains("kakao", StringComparison.OrdinalIgnoreCase) == true);
        
        kakaoCaptures.Should().Be(ExpectedKakaoTalkCaptures,
            "데이터 시트에 따르면 카카오톡 촬영이 2개 있어야 함 (23:16:38, 23:20:14)");

        _output.WriteLine($"✓ 카카오톡 촬영 수: {kakaoCaptures} (예상: {ExpectedKakaoTalkCaptures})");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_TelegramCaptures()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var telegramCaptures = result.CaptureEvents
            .Count(c => c.PackageName?.Contains("telegram", StringComparison.OrdinalIgnoreCase) == true);
        
        telegramCaptures.Should().Be(ExpectedTelegramCaptures,
            "데이터 시트에 따르면 텔레그램 촬영이 2개 있어야 함 (23:23:09, 23:26:28)");

        _output.WriteLine($"✓ 텔레그램 촬영 수: {telegramCaptures} (예상: {ExpectedTelegramCaptures})");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_SilentCameraCaptures()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var silentCameraCaptures = result.CaptureEvents
            .Count(c => c.PackageName?.Contains("Silent", StringComparison.OrdinalIgnoreCase) == true);
        
        silentCameraCaptures.Should().Be(ExpectedSilentCameraCaptures,
            "데이터 시트에 따르면 무음 카메라 촬영이 1개 있어야 함 (23:29:40)");

        _output.WriteLine($"✓ 무음 카메라 촬영 수: {silentCameraCaptures} (예상: {ExpectedSilentCameraCaptures})");
    }

    #endregion

    #region 데이터 품질 검증

    [Fact]
    public async Task Should_HaveValidSessionData()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        result.Success.Should().BeTrue();
        result.Sessions.Should().NotBeEmpty("최소 1개 이상의 세션이 탐지되어야 함");

        _output.WriteLine($"=== 세션 데이터 품질 검증 ===");
        _output.WriteLine($"총 세션 수: {result.Sessions.Count}");

        foreach (var session in result.Sessions)
        {
            // 세션 ID 검증
            session.SessionId.Should().NotBeEmpty("모든 세션은 유효한 ID를 가져야 함");

            // 시작 시간 검증
            session.StartTime.Should().NotBe(default, "시작 시간이 유효해야 함");

            // 종료 시간 검증 (전면/후면 전환 시 동일 초에 발생 가능)
            session.EndTime!.Value.Should().BeOnOrAfter(session.StartTime,
                "종료 시간은 시작 시간과 같거나 이후여야 함 (전면/후면 전환 시 동일 초에 발생 가능)");

            // Duration 검증 (전면/후면 전환 시 0초 가능)
            session.Duration!.Value.TotalSeconds.Should().BeGreaterThanOrEqualTo(0,
                "세션 Duration은 0초 이상이어야 함 (전면/후면 전환 시 0초 가능)");

            // PackageName 검증
            session.PackageName.Should().NotBeNullOrEmpty("모든 세션은 패키지명을 가져야 함");

            // SourceLogTypes 검증
            session.SourceLogTypes.Should().NotBeEmpty("모든 세션은 최소 1개 이상의 소스 로그를 가져야 함");
        }

        _output.WriteLine($"✓ 모든 세션 데이터가 유효함");
    }

    [Fact]
    public async Task Should_HaveValidCaptureData()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        result.Success.Should().BeTrue();
        result.CaptureEvents.Should().NotBeEmpty("최소 1개 이상의 촬영이 탐지되어야 함");

        _output.WriteLine($"=== 촬영 데이터 품질 검증 ===");
        _output.WriteLine($"총 촬영 수: {result.CaptureEvents.Count}");

        foreach (var capture in result.CaptureEvents)
        {
            // CaptureId 검증
            capture.CaptureId.Should().NotBeEmpty("모든 촬영은 유효한 ID를 가져야 함");

            // CaptureTime 검증
            capture.CaptureTime.Should().NotBe(default, "촬영 시간이 유효해야 함");

            // CaptureDetectionScore 검증
            capture.CaptureDetectionScore.Should().BeInRange(0.15, 2.5,
                "촬영 탐지 점수는 최소 가중치(0.15) 이상이어야 함 (MaxConfidence 캡핑 제거)");

            // ParentSessionId 검증
            capture.ParentSessionId.Should().NotBeEmpty("모든 촬영은 세션과 연결되어야 함");

            // PackageName 검증
            capture.PackageName.Should().NotBeNullOrEmpty("모든 촬영은 패키지명을 가져야 함");

            // ArtifactTypes 검증
            capture.ArtifactTypes.Should().NotBeEmpty("모든 촬영은 최소 1개 이상의 아티팩트를 가져야 함");
        }

        _output.WriteLine($"✓ 모든 촬영 데이터가 유효함");
    }

    #endregion

    #region 패키지명 검증

    [Fact]
    public async Task Should_Have_ValidPackageNames()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        result.Success.Should().BeTrue();
        
        _output.WriteLine($"=== 패키지명 추출 검증 ===");
        
        // 모든 세션이 패키지명을 가지고 있어야 함
        var sessionsWithoutPackage = result.Sessions.Where(s => string.IsNullOrEmpty(s.PackageName)).ToList();
        
        _output.WriteLine($"전체 세션 수: {result.Sessions.Count}");
        _output.WriteLine($"패키지명 없는 세션: {sessionsWithoutPackage.Count}개");
        
        if (sessionsWithoutPackage.Any())
        {
            _output.WriteLine($"\n⚠️ 패키지명이 없는 세션들:");
            foreach (var session in sessionsWithoutPackage)
            {
                _output.WriteLine($"  - {session.StartTime:HH:mm:ss} ~ {session.EndTime?.ToString("HH:mm:ss") ?? "N/A"}");
                _output.WriteLine($"    SourceLogs: {string.Join(", ", session.SourceLogTypes)}");
            }
        }
        
        // 주요 패키지 검증
        var packageCounts = result.Sessions
            .Where(s => !string.IsNullOrEmpty(s.PackageName))
            .GroupBy(s => s.PackageName)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        _output.WriteLine($"\n📊 탐지된 패키지별 세션 수:");
        foreach (var group in packageCounts)
        {
            _output.WriteLine($"  {group.Key}: {group.Count()}개");
        }
        
        // 예상 패키지들이 존재하는지 확인
        var expectedPackages = new[] { "camera", "kakao", "telegram", "SilentCamera" };
        
        _output.WriteLine($"\n✅ 예상 패키지 검증:");
        foreach (var expected in expectedPackages)
        {
            var found = packageCounts.Any(g => 
                g.Key.Contains(expected, StringComparison.OrdinalIgnoreCase));
            _output.WriteLine($"  {expected}: {(found ? "✓ 발견" : "✗ 미발견")}");
        }
        
        // 최소한 2개 이상의 다른 패키지가 있어야 함
        packageCounts.Should().HaveCountGreaterThan(1, "여러 앱의 카메라 사용이 탐지되어야 함");
    }

    #endregion

    #region 촬영 시간 정확성 검증

    [Fact]
    public async Task Should_DetectCapture_WithExpectedTimestamps()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        
        // 예상 촬영 시간 (데이터 시트 기준)
        var expectedCaptures = new Dictionary<string, DateTime[]>
        {
            ["camera"] = new[] { new DateTime(2025, 10, 7, 23, 14, 20) },
            ["kakao"] = new[] 
            { 
                new DateTime(2025, 10, 7, 23, 16, 38),
                new DateTime(2025, 10, 7, 23, 20, 14)
            },
            ["telegram"] = new[] 
            { 
                new DateTime(2025, 10, 7, 23, 23, 9),
                new DateTime(2025, 10, 7, 23, 26, 28)
            },
            ["Silent"] = new[] { new DateTime(2025, 10, 7, 23, 29, 40) }
        };

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine($"=== 촬영 시간 정확성 검증 ===");
        
        foreach (var (appKey, expectedTimes) in expectedCaptures)
        {
            var captures = result.CaptureEvents
                .Where(c => c.PackageName?.Contains(appKey, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(c => c.CaptureTime)
            .ToList();
        
            _output.WriteLine($"\n{appKey}:");
            _output.WriteLine($"  예상 촬영 수: {expectedTimes.Length}");
            _output.WriteLine($"  실제 촬영 수: {captures.Count}");
            
            for (int i = 0; i < Math.Min(expectedTimes.Length, captures.Count); i++)
            {
                var expectedTime = expectedTimes[i];
                var actualTime = captures[i].CaptureTime;
                var timeDiff = Math.Abs((actualTime - expectedTime).TotalSeconds);
                
                _output.WriteLine($"  촬영 #{i + 1}:");
                _output.WriteLine($"    예상: {expectedTime:HH:mm:ss}");
                _output.WriteLine($"    실제: {actualTime:HH:mm:ss}");
                _output.WriteLine($"    차이: {timeDiff:F1}초");
                
                timeDiff.Should().BeLessThanOrEqualTo(5, 
                    $"{appKey} 촬영 #{i + 1}의 시간은 5초 이내 오차 허용");
            }
        }
    }

    #endregion

    #region YAML 설정 검증

    [Fact]
    public async Task Should_Produce_Same_Results_With_YAML_Config()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        
        // 기본 설정으로 분석
        var defaultOrchestrator = CreateOrchestratorWithDefaultConfig();
        var defaultResult = await defaultOrchestrator.AnalyzeAsync(_parsedEvents!, options);
        
        // YAML 설정으로 분석
        var yamlOrchestrator = CreateOrchestratorWithYamlConfig();
        var yamlResult = await yamlOrchestrator.AnalyzeAsync(_parsedEvents!, options);
        
        // Assert: 세션 수 동일
        yamlResult.Sessions.Should().HaveCount(defaultResult.Sessions.Count,
            "YAML 설정과 기본 설정은 동일한 수의 세션을 탐지해야 함");
        
        // Assert: 촬영 수 동일
        yamlResult.CaptureEvents.Should().HaveCount(defaultResult.CaptureEvents.Count,
            "YAML 설정과 기본 설정은 동일한 수의 촬영을 탐지해야 함");
        
        // 결과 출력
        _output.WriteLine("=== Configuration Comparison ===");
        _output.WriteLine($"기본 설정 - 세션: {defaultResult.Sessions.Count}, 촬영: {defaultResult.CaptureEvents.Count}");
        _output.WriteLine($"YAML 설정 - 세션: {yamlResult.Sessions.Count}, 촬영: {yamlResult.CaptureEvents.Count}");
        
        // 세션별 비교
        for (int i = 0; i < Math.Min(defaultResult.Sessions.Count, yamlResult.Sessions.Count); i++)
        {
            var defaultSession = defaultResult.Sessions[i];
            var yamlSession = yamlResult.Sessions[i];
            
            _output.WriteLine($"\nSession {i + 1}:");
            _output.WriteLine($"  기본 - {defaultSession.PackageName}: {defaultSession.StartTime:HH:mm:ss} - {defaultSession.EndTime:HH:mm:ss}");
            _output.WriteLine($"  YAML - {yamlSession.PackageName}: {yamlSession.StartTime:HH:mm:ss} - {yamlSession.EndTime:HH:mm:ss}");
            
            // 패키지명 비교
            yamlSession.PackageName.Should().Be(defaultSession.PackageName,
                $"세션 {i + 1}의 패키지명이 동일해야 함");
            
            // 시작 시간 비교 (±1초 허용)
            Math.Abs((yamlSession.StartTime - defaultSession.StartTime).TotalSeconds).Should().BeLessThanOrEqualTo(1,
                $"세션 {i + 1}의 시작 시간이 거의 동일해야 함");
        }
        
        _output.WriteLine("\n✅ YAML 설정과 기본 설정이 동일한 결과를 생성함");
    }

    #endregion

    #region 가중치 점수 검증 (논문용)

    [Fact]
    public async Task Should_Match_GroundTruth_DefaultCamera_CaptureScore()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        // 실제 계산값: DATABASE_INSERT(0.5) + VIBRATION(0.4) + FOREGROUND_SERVICE(0.3) + PLAYER_CREATED(0.25) + MEDIA_EXTRACTOR(0.2) + PLAYER_RELEASED(0.15) = 1.80
        var expectedScore = 1.80;
        var tolerance = 0.15; // 실제 점수 범위: 1.65~1.95

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var defaultCameraSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("com.sec.android.app.camera", StringComparison.OrdinalIgnoreCase) == true
                     && !s.PackageName.Contains("kakao", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var captureWithScore = result.CaptureEvents
            .Where(c => defaultCameraSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)))
            .ToList();

        captureWithScore.Should().HaveCount(ExpectedDefaultCameraCaptures,
            "기본 카메라 촬영이 1개 있어야 함");

        var capture = captureWithScore.First();
        
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 기본 카메라 (Camera API) 촬영 점수 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📅 촬영 시각: {capture.CaptureTime:HH:mm:ss.fff}");
        _output.WriteLine($"📊 실제 점수: {capture.CaptureDetectionScore:F2}");
        _output.WriteLine($"🎯 예상 점수: {expectedScore:F2} (±{tolerance:F2})");
        _output.WriteLine($"📦 CaptureId: {capture.CaptureId}");
        
        // 공통 메서드 사용하여 아티팩트 및 점수 계산 출력
        ArtifactWeights.WriteScoreCalculation(_output, capture.ArtifactTypes, Weights);
        
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // 점수 검증
        capture.CaptureDetectionScore.Should().BeInRange(expectedScore - tolerance, expectedScore + tolerance,
            $"기본 카메라 촬영 점수는 {expectedScore:F2} ± {tolerance:F2} 범위여야 함 " +
            "(MaxConfidence 캡핑 제거, 실제 계산값 1.80)");

        // 주요 아티팩트 검증
        capture.ArtifactTypes.Should().Contain("DATABASE_INSERT",
            "DATABASE_INSERT 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("PLAYER_CREATED",
            "PLAYER_CREATED 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("VIBRATION_EVENT",
            "VIBRATION_EVENT 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("MEDIA_EXTRACTOR",
            "MEDIA_EXTRACTOR 이벤트가 탐지되어야 함");
        // PLAYER_EVENT는 일부 샘플에서만 발생 (일관성 없음)
        capture.ArtifactTypes.Should().Contain("PLAYER_RELEASED",
            "PLAYER_RELEASED 이벤트가 탐지되어야 함");

        _output.WriteLine($"\n✅ 기본 카메라 촬영 점수 검증 완료");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_KakaoTalk_CaptureScore()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        // 실제 계산값: VIBRATION(0.4) + PLAYER_EVENT(0.35) + URI_PERMISSION_GRANT(0.3) + FOREGROUND_SERVICE(0.3) + PLAYER_CREATED(0.25) + URI_PERMISSION_REVOKE(0.22) + MEDIA_EXTRACTOR(0.2) + CAMERA_ACTIVITY_REFRESH(0.15) + PLAYER_RELEASED(0.15) = 2.32
        var expectedScore = 2.32;
        var tolerance = 0.15; // 실제 점수 범위: 2.17~2.47

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var kakaoSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("kakao", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        
        var capturesWithScore = result.CaptureEvents
            .Where(c => kakaoSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)))
            .ToList();

        capturesWithScore.Should().HaveCount(ExpectedKakaoTalkCaptures,
            "카카오톡 촬영이 2개 있어야 함");

        foreach (var (capture, index) in capturesWithScore.Select((c, i) => (c, i)))
        {
            _output.WriteLine("════════════════════════════════════════════════════════════");
            _output.WriteLine($"=== 카카오톡 (Camera2+CUA) 촬영 #{index + 1} 점수 검증 ===");
            _output.WriteLine("════════════════════════════════════════════════════════════");
            _output.WriteLine($"📅 촬영 시각: {capture.CaptureTime:HH:mm:ss.fff}");
            _output.WriteLine($"📊 실제 점수: {capture.CaptureDetectionScore:F2}");
            _output.WriteLine($"🎯 예상 점수: {expectedScore:F2} (±{tolerance:F2})");
            _output.WriteLine($"📦 CaptureId: {capture.CaptureId}");
            
            // 공통 메서드 사용하여 아티팩트 및 점수 계산 출력
            ArtifactWeights.WriteScoreCalculation(_output, capture.ArtifactTypes, Weights);
            
            _output.WriteLine($"\nℹ️  특징: DATABASE_INSERT 없음 (Camera2+CUA 특성)");
            _output.WriteLine("════════════════════════════════════════════════════════════");

            // 점수 검증
            capture.CaptureDetectionScore.Should().BeInRange(expectedScore - tolerance, expectedScore + tolerance,
                $"카카오톡 촬영 #{index + 1} 점수는 {expectedScore:F2} ± {tolerance:F2} 범위여야 함");

            // 주요 아티팩트 검증
            capture.ArtifactTypes.Should().Contain("VIBRATION_EVENT",
                "카카오톡 특유의 진동이 탐지되어야 함");
            capture.ArtifactTypes.Should().Contain("PLAYER_CREATED",
                "촬영 사운드 재생을 위한 플레이어 생성 이벤트가 탐지되어야 함");
            capture.ArtifactTypes.Should().Contain("PLAYER_RELEASED",
                "사용한 플레이어 해제 이벤트가 탐지되어야 함");
            capture.ArtifactTypes.Should().Contain("CAMERA_ACTIVITY_REFRESH",
                "카메라 Activity 갱신 이벤트가 탐지되어야 함");
            capture.ArtifactTypes.Should().Contain("MEDIA_EXTRACTOR",
                "미디어 추출기 이벤트가 탐지되어야 함");
            capture.ArtifactTypes.Should().NotContain("DATABASE_INSERT",
                "카카오톡은 DATABASE_INSERT가 없어야 함 (Camera2+CUA 특성)");
        }

        _output.WriteLine($"\n✅ 카카오톡 촬영 점수 검증 완료");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_Telegram_CaptureScore()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        var expectedScoreCapture1 = 0.55; // VIBRATION(0.4) + CAMERA_ACTIVITY_REFRESH(0.15) - MEDIA_EXTRACTOR 미탐지
        var expectedScoreCapture2 = 1.00; // MaxConfidence 캡핑 (실제 계산값 1.20)
        var tolerance = 0.1;

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var telegramSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("telegram", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        
        var capturesWithScore = result.CaptureEvents
            .Where(c => telegramSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)))
            .OrderBy(c => c.CaptureTime)
            .ToList();

        capturesWithScore.Should().HaveCount(ExpectedTelegramCaptures,
            "텔레그램 촬영이 2개 있어야 함");

        // 첫 번째 촬영 (MEDIA_EXTRACTOR 미탐지)
        var capture1 = capturesWithScore[0];
        
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 텔레그램 (CameraX) 촬영 #1 점수 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📅 촬영 시각: {capture1.CaptureTime:HH:mm:ss.fff}");
        _output.WriteLine($"📊 실제 점수: {capture1.CaptureDetectionScore:F2}");
        _output.WriteLine($"🎯 예상 점수: {expectedScoreCapture1:F2} (±{tolerance:F2})");
        _output.WriteLine($"📦 CaptureId: {capture1.CaptureId}");
        
        // 공통 메서드 사용하여 아티팩트 및 점수 계산 출력
        ArtifactWeights.WriteScoreCalculation(_output, capture1.ArtifactTypes, Weights);
        
        _output.WriteLine($"\nℹ️  특징: MEDIA_EXTRACTOR 미탐지 (변동 가능)");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        capture1.CaptureDetectionScore.Should().BeInRange(expectedScoreCapture1 - tolerance, expectedScoreCapture1 + tolerance,
            $"텔레그램 촬영 #1 점수는 {expectedScoreCapture1:F2} ± {tolerance:F2} 범위여야 함");

        capture1.ArtifactTypes.Should().Contain("VIBRATION_EVENT",
            "텔레그램 특유의 TOUCH 진동이 탐지되어야 함");
        capture1.ArtifactTypes.Should().Contain("CAMERA_ACTIVITY_REFRESH", 
            "카메라 Activity 갱신 이벤트가 탐지되어야 함");
        capture1.ArtifactTypes.Should().NotContain("MEDIA_EXTRACTOR", 
            "Ground Truth에 따르면 첫 번째 촬영에서는 MEDIA_EXTRACTOR가 미탐지되어야 함");
        capture1.ArtifactTypes.Should().NotContain("DATABASE_INSERT",
            "텔레그램은 DATABASE_INSERT가 없어야 함");

        // 두 번째 촬영 (PLAYER_CREATED 추가 탐지, MaxConfidence 캡핑)
        var capture2 = capturesWithScore[1];
        
        _output.WriteLine("\n════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 텔레그램 (CameraX) 촬영 #2 점수 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📅 촬영 시각: {capture2.CaptureTime:HH:mm:ss.fff}");
        _output.WriteLine($"📊 실제 점수: {capture2.CaptureDetectionScore:F2}");
        _output.WriteLine($"🎯 예상 점수: {expectedScoreCapture2:F2} (±{tolerance:F2})");
        _output.WriteLine($"📦 CaptureId: {capture2.CaptureId}");
        
        // 공통 메서드 사용하여 아티팩트 및 점수 계산 출력
        ArtifactWeights.WriteScoreCalculation(_output, capture2.ArtifactTypes, Weights);
        
        _output.WriteLine($"\nℹ️  특징: PLAYER_CREATED + MEDIA_EXTRACTOR 탐지 (MaxConfidence 캡핑)");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        capture2.CaptureDetectionScore.Should().BeInRange(expectedScoreCapture2 - tolerance, expectedScoreCapture2 + tolerance,
            $"텔레그램 촬영 #2 점수는 {expectedScoreCapture2:F2} ± {tolerance:F2} 범위여야 함");

        capture2.ArtifactTypes.Should().Contain("VIBRATION_EVENT",
            "텔레그램 특유의 TOUCH 진동이 탐지되어야 함");
        capture2.ArtifactTypes.Should().Contain("CAMERA_ACTIVITY_REFRESH", 
            "카메라 Activity 갱신 이벤트가 탐지되어야 함");
        capture2.ArtifactTypes.Should().Contain("MEDIA_EXTRACTOR", 
            "Ground Truth에 따르면 두 번째 촬영에서는 MEDIA_EXTRACTOR가 탐지되어야 함");
        capture2.ArtifactTypes.Should().Contain("PLAYER_CREATED", 
            "Ground Truth에 따르면 두 번째 촬영에서는 PLAYER_CREATED가 탐지되어야 함");
        capture2.ArtifactTypes.Should().NotContain("DATABASE_INSERT",
            "텔레그램은 DATABASE_INSERT가 없어야 함");

        _output.WriteLine($"\n✅ 텔레그램 촬영 점수 검증 완료");
        _output.WriteLine($"💡 텔레그램은 촬영마다 탐지되는 아티팩트가 다를 수 있음 (앱 내부 동작의 가변성)");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_SilentCamera_CaptureScore()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        var expectedScore = 1.00; // MaxConfidence 캡핑 (실제 계산값: 1.05)
        var tolerance = 0.1;

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var silentCameraSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("Silent", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        
        var capturesWithScore = result.CaptureEvents
            .Where(c => silentCameraSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)))
            .ToList();

        capturesWithScore.Should().HaveCount(ExpectedSilentCameraCaptures,
            "무음 카메라 촬영이 1개 있어야 함");

        var capture = capturesWithScore.First();
        
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 무음 카메라 (CameraX) 촬영 점수 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📅 촬영 시각: {capture.CaptureTime:HH:mm:ss.fff}");
        _output.WriteLine($"📊 실제 점수: {capture.CaptureDetectionScore:F2}");
        _output.WriteLine($"🎯 예상 점수: {expectedScore:F2} (±{tolerance:F2})");
        _output.WriteLine($"📦 CaptureId: {capture.CaptureId}");
        
        // 공통 메서드 사용하여 아티팩트 및 점수 계산 출력
        ArtifactWeights.WriteScoreCalculation(_output, capture.ArtifactTypes, Weights);
        
        _output.WriteLine($"ℹ️  특징: CONNECT 이벤트를 촬영 신호로 간주 + 햅틱 피드백");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // 점수 검증
        capture.CaptureDetectionScore.Should().BeInRange(expectedScore - tolerance, expectedScore + tolerance,
            $"무음 카메라 촬영 점수는 {expectedScore:F2} ± {tolerance:F2} 범위여야 함 " +
            "(실제 계산값 1.05이지만 MaxConfidence=1.0으로 캡핑됨)");

        // 주요 아티팩트 검증
        capture.ArtifactTypes.Should().Contain("SILENT_CAMERA_CAPTURE",
            "무음 카메라 특수 아티팩트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("CAMERA_ACTIVITY_REFRESH",
            "카메라 Activity 갱신 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("VIBRATION_EVENT",
            "Ground Truth에 따르면 예상치 못한 진동이 탐지되어야 함");

        _output.WriteLine($"\n✅ 무음 카메라 촬영 점수 검증 완료");
    }

    [Fact]
    public async Task Should_Verify_AllCaptures_MeetMinimumThreshold()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        var minThreshold = 0.3;

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine($"=== 최소 임계값 검증 (MinConfidenceThreshold={minThreshold:F1}) ===");
        _output.WriteLine($"총 촬영 수: {result.CaptureEvents.Count}\n");

        foreach (var capture in result.CaptureEvents.OrderBy(c => c.CaptureTime))
        {
            _output.WriteLine($"{capture.PackageName} ({capture.CaptureTime:HH:mm:ss}): {capture.CaptureDetectionScore:F2}");
            
            capture.CaptureDetectionScore.Should().BeGreaterThanOrEqualTo(minThreshold,
                $"{capture.PackageName}의 촬영은 최소 임계값 {minThreshold:F1}을 충족해야 함");
        }

        _output.WriteLine($"\n✅ 모든 촬영이 최소 임계값({minThreshold:F1})을 충족함");
    }

    [Fact]
    public async Task Should_Verify_CaptureScore_Distribution()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        _output.WriteLine($"=== 촬영 점수 분포 분석 (논문용) ===");
        _output.WriteLine($"총 촬영 수: {result.CaptureEvents.Count}\n");

        var scoresByApp = result.CaptureEvents
            .GroupBy(c => c.PackageName)
            .OrderByDescending(g => g.Average(c => c.CaptureDetectionScore));

        foreach (var group in scoresByApp)
        {
            var captures = group.OrderBy(c => c.CaptureTime).ToList();
            var avgScore = captures.Average(c => c.CaptureDetectionScore);
            var minScore = captures.Min(c => c.CaptureDetectionScore);
            var maxScore = captures.Max(c => c.CaptureDetectionScore);

            _output.WriteLine($"{group.Key}:");
            _output.WriteLine($"  촬영 수: {captures.Count}");
            _output.WriteLine($"  평균 점수: {avgScore:F2}");
            _output.WriteLine($"  점수 범위: {minScore:F2} ~ {maxScore:F2}");
            _output.WriteLine($"  촬영 상세:");

            foreach (var capture in captures)
            {
                _output.WriteLine($"    - {capture.CaptureTime:HH:mm:ss}: {capture.CaptureDetectionScore:F2} " +
                                $"(아티팩트 {capture.ArtifactTypes.Count}개: {string.Join(", ", capture.ArtifactTypes)})");
            }
            _output.WriteLine("");
        }

        // 점수 범위별 분포
        var scoreRanges = new[]
        {
            (Min: 0.0, Max: 0.5, Label: "낮음 (0.0-0.5)"),
            (Min: 0.5, Max: 0.8, Label: "중간 (0.5-0.8)"),
            (Min: 0.8, Max: 1.0, Label: "높음 (0.8-1.0)")
        };

        _output.WriteLine($"📊 점수 범위별 분포:");
        foreach (var range in scoreRanges)
        {
            var count = result.CaptureEvents.Count(c => 
                c.CaptureDetectionScore >= range.Min && c.CaptureDetectionScore < range.Max);
            _output.WriteLine($"  {range.Label}: {count}개");
        }
        
        // MaxConfidence=1.0인 촬영 수
        var maxConfidenceCount = result.CaptureEvents.Count(c => 
            Math.Abs(c.CaptureDetectionScore - 1.0) < 0.01);
        _output.WriteLine($"\n💡 MaxConfidence(1.0)로 캡핑된 촬영: {maxConfidenceCount}개 / {result.CaptureEvents.Count}개");

        result.CaptureEvents.Should().NotBeEmpty("최소 1개 이상의 촬영이 탐지되어야 함");
    }

    #endregion

    #region Ground Truth 문서 자동 생성 (논문용)

    /// <summary>
    /// Ground Truth 문서를 실제 분석 결과 기반으로 자동 생성합니다.
    /// </summary>
    /// <remarks>
    /// 이 테스트는 논문 작성을 위한 GT 문서를 자동 생성합니다:
    /// - 실제 분석 실행 (하드코딩 없음)
    /// - 결과 데이터로 마크다운 문서 생성
    /// - 파일 저장 및 검증
    /// - 데이터 정확성 보장
    /// 
    /// 목적:
    /// - 수동 작성 오류 제거
    /// - 일관성 있는 데이터 표현
    /// - 재현 가능한 실험 결과
    /// - 논문 직접 활용 가능
    /// </remarks>
    [Fact]
    public async Task Generate_GroundTruth_Document()
    {
        // ========================================
        // Arrange: 샘플 정보 및 분석 옵션 설정
        // ========================================
        var options = CreateAnalysisOptions();

        var sampleInfo = new ArtifactWeights.SampleInfo(
            SampleNumber: 3,
            SampleName: "3차 샘플",
            TestDate: new DateTime(2025, 10, 7),
            TimeRange: (_startTime, _endTime),
            Description: "기본 카메라, 카카오톡, 텔레그램, 무음 카메라 사용 (총 6회 촬영)"
        );

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== Ground Truth 문서 자동 생성 (실제 분석 결과 기반) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📋 샘플: {sampleInfo.SampleName}");
        _output.WriteLine($"📅 날짜: {sampleInfo.TestDate:yyyy-MM-dd}");
        _output.WriteLine($"⏰ 시간: {sampleInfo.TimeRange.Start:HH:mm:ss} ~ {sampleInfo.TimeRange.End:HH:mm:ss}");
        _output.WriteLine($"📝 설명: {sampleInfo.Description}");
        _output.WriteLine("");

        // ========================================
        // Act: 실제 분석 실행
        // ========================================
        _output.WriteLine("🔄 1단계: 실제 로그 분석 실행 중...");
        var analysisResult = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

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
            Weights);

        gtDocument.Should().NotBeNullOrEmpty("GT 문서가 생성되어야 함");

        _output.WriteLine($"✅ GT 문서 생성 완료: {gtDocument.Length} 문자");
        _output.WriteLine("");

        // ========================================
        // Act: 파일 저장
        // ========================================
        _output.WriteLine("💾 3단계: 파일 저장 중...");
        
        // 저장 경로: 테스트 프로젝트/Documentation/GroundTruth/
        var projectRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(), "..", "..", ".."));
        var docDir = Path.Combine(projectRoot, "Documentation", "GroundTruth");
        
        // 디렉토리 생성 (없으면)
        if (!Directory.Exists(docDir))
        {
            Directory.CreateDirectory(docDir);
            _output.WriteLine($"✅ 디렉토리 생성: {docDir}");
        }

        var outputPath = Path.Combine(docDir, $"Sample{sampleInfo.SampleNumber}_Ground_Truth.md");
        await File.WriteAllTextAsync(outputPath, gtDocument);

        _output.WriteLine($"✅ 파일 저장 완료: {outputPath}");
        _output.WriteLine("");

        // ========================================
        // Assert: GT 문서 검증
        // ========================================
        _output.WriteLine("🔍 4단계: GT 문서 검증 중...");

        // 4.1 파일 존재 확인
        File.Exists(outputPath).Should().BeTrue("GT 문서 파일이 존재해야 함");
        _output.WriteLine("  ✓ 파일 존재 확인");

        // 4.2 기본 섹션 존재 확인
        gtDocument.Should().Contain("# Sample 3", "헤더가 있어야 함");
        gtDocument.Should().Contain("## 📋 샘플 정보", "샘플 정보 섹션이 있어야 함");
        gtDocument.Should().Contain("## 📊 전체 요약", "전체 요약 섹션이 있어야 함");
        gtDocument.Should().Contain("## 📝 세션별 상세 정보", "세션 상세 섹션이 있어야 함");
        gtDocument.Should().Contain("## 🎯 촬영별 상세 정보", "촬영 상세 섹션이 있어야 함");
        gtDocument.Should().Contain("## 🔍 아티팩트 분석", "아티팩트 분석 섹션이 있어야 함");
        gtDocument.Should().Contain("## 📈 통계 데이터", "통계 섹션이 있어야 함");
        _output.WriteLine("  ✓ 필수 섹션 존재 확인");

        // 4.3 실제 데이터 검증 (Ground Truth와 일치 여부)
        gtDocument.Should().Contain($"**총 세션 수**: {ExpectedTotalSessions}개",
            "실제 세션 수가 Ground Truth와 일치해야 함");
        gtDocument.Should().Contain($"**총 촬영 수**: {ExpectedTotalCaptures}개",
            "실제 촬영 수가 Ground Truth와 일치해야 함");
        _output.WriteLine("  ✓ Ground Truth 일치 확인");

        // 4.4 앱명 검증
        gtDocument.Should().Contain("기본 카메라", "기본 카메라 정보가 포함되어야 함");
        gtDocument.Should().Contain("카카오톡", "카카오톡 정보가 포함되어야 함");
        gtDocument.Should().Contain("텔레그램", "텔레그램 정보가 포함되어야 함");
        gtDocument.Should().Contain("무음 카메라", "무음 카메라 정보가 포함되어야 함");
        _output.WriteLine("  ✓ 앱명 정보 확인");

        // 4.5 점수 정보 검증
        foreach (var capture in analysisResult.CaptureEvents)
        {
            gtDocument.Should().Contain($"{capture.CaptureDetectionScore:F2}",
                $"촬영 점수 {capture.CaptureDetectionScore:F2}가 문서에 포함되어야 함");
        }
        _output.WriteLine($"  ✓ 촬영 점수 정보 확인 ({analysisResult.CaptureEvents.Count}개)");

        // 4.6 아티팩트 정보 검증
        var allArtifacts = analysisResult.CaptureEvents
            .SelectMany(c => c.ArtifactTypes)
            .Distinct()
            .ToList();

        foreach (var artifact in allArtifacts)
        {
            gtDocument.Should().Contain(artifact,
                $"아티팩트 {artifact}가 문서에 포함되어야 함");
        }
        _output.WriteLine($"  ✓ 아티팩트 정보 확인 ({allArtifacts.Count}개 고유 타입)");

        // 4.7 시간 정보 검증
        gtDocument.Should().Contain(sampleInfo.TimeRange.Start.ToString("HH:mm:ss"),
            "시작 시간이 문서에 포함되어야 함");
        gtDocument.Should().Contain(sampleInfo.TimeRange.End.ToString("HH:mm:ss"),
            "종료 시간이 문서에 포함되어야 함");
        _output.WriteLine("  ✓ 시간 정보 확인");

        // 4.8 통계 정보 검증
        if (analysisResult.CaptureEvents.Any())
        {
            var scores = analysisResult.CaptureEvents.Select(c => c.CaptureDetectionScore).ToList();
            var avgScore = scores.Average();
            var maxScore = scores.Max();
            var minScore = scores.Min();

            gtDocument.Should().Contain("평균 점수", "평균 점수 통계가 있어야 함");
            gtDocument.Should().Contain("최고 점수", "최고 점수 통계가 있어야 함");
            gtDocument.Should().Contain("최저 점수", "최저 점수 통계가 있어야 함");
            gtDocument.Should().Contain($"{avgScore:F2}", "실제 평균 점수가 포함되어야 함");
            gtDocument.Should().Contain($"{maxScore:F2}", "실제 최고 점수가 포함되어야 함");
            gtDocument.Should().Contain($"{minScore:F2}", "실제 최저 점수가 포함되어야 함");
            _output.WriteLine("  ✓ 통계 정보 확인");
        }

        // 4.9 하드코딩 없음 검증 (메타 정보 확인)
        gtDocument.Should().Contain("자동 생성 (실제 분석 결과 기반)",
            "자동 생성 메타 정보가 있어야 함");
        gtDocument.Should().Contain("AnalysisResult (하드코딩 없음)",
            "데이터 소스 정보가 명시되어야 함");
        _output.WriteLine("  ✓ 자동 생성 메타 정보 확인");

        // ========================================
        // 최종 결과 출력
        // ========================================
        _output.WriteLine("");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("✅ GT 문서 생성 및 검증 완료");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📁 저장 위치: {outputPath}");
        _output.WriteLine($"📏 문서 크기: {gtDocument.Length:N0} 문자");
        _output.WriteLine($"📊 검증 항목: 9개 전체 통과");
        _output.WriteLine("");
        _output.WriteLine("💡 사용 방법:");
        _output.WriteLine("   1. 생성된 GT 문서를 열어 내용 확인");
        _output.WriteLine("   2. 논문 작성 시 해당 데이터 직접 활용");
        _output.WriteLine("   3. 테스트 재실행 시 항상 최신 데이터로 갱신");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // ========================================
        // 문서 미리보기 출력 (처음 500자)
        // ========================================
        _output.WriteLine("");
        _output.WriteLine("📄 GT 문서 미리보기 (처음 500자):");
        _output.WriteLine("────────────────────────────────────────────────────────────");
        var preview = gtDocument.Length > 500 ? gtDocument.Substring(0, 500) + "..." : gtDocument;
        _output.WriteLine(preview);
        _output.WriteLine("────────────────────────────────────────────────────────────");
    }

    #endregion

    #region Helper Methods

    private IAnalysisOrchestrator CreateOrchestratorWithDefaultConfig()
    {
        // DI 컨테이너 설정
        var services = new ServiceCollection();
        
        // Logging 인프라 추가
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        
        // AnalysisOptions 등록 (EventDeduplicator 의존성)
        services.AddSingleton(new AnalysisOptions { DeduplicationSimilarityThreshold = 0.8 });
        
        // AndroidAdbAnalysis 서비스 등록 (기본 설정 사용)
        services.AddAndroidAdbAnalysis();
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        // IAnalysisOrchestrator 해결
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

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
        var logger = loggerFactory.CreateLogger<Sample3GroundTruthTests>();
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
        
        // ===== Transmission Detection Services =====
        
        services.AddSingleton<ITransmissionDetector, WifiTransmissionDetector>();
        
        // ===== Reporting Services =====
        
        services.AddSingleton<IReportGenerator, HtmlReportGenerator>();
        services.AddSingleton<ITimelineBuilder, TimelineBuilder>();
        
        // ===== Orchestration =====
        
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }

    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync()
    {
        var samplePath = Path.Combine(_sampleLogsPath, SampleDirectoryName);
        
        if (!Directory.Exists(samplePath))
        {
            throw new DirectoryNotFoundException($"샘플 로그 디렉토리를 찾을 수 없습니다: {samplePath}");
        }

        var allEvents = new List<NormalizedLogEvent>();

        // 로그 파일 매핑 (실제 파일명 → 설정 파일)
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
            var events = await ParseLogFileAsync(logPath, configFileName, _startTime, _endTime);
            allEvents.AddRange(events);
        }

        _output.WriteLine($"📊 Total events: {allEvents.Count:N0}");
        
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
            var events = result.Events?.ToList() ?? new List<NormalizedLogEvent>();
            
            _output.WriteLine($"✓ {Path.GetFileName(logFilePath),-30} : {events.Count,6:N0} events");
            return events;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"✗ Error parsing {Path.GetFileName(logFilePath)}: {ex.Message}");
            return new List<NormalizedLogEvent>();
        }
    }

    private AnalysisOptions CreateAnalysisOptions()
    {
        return new AnalysisOptions
        {
            MinConfidenceThreshold = 0.3,
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MaxSessionGap = TimeSpan.FromMinutes(5),
            EnableIncompleteSessionHandling = true,
            DeduplicationSimilarityThreshold = 0.8
        };
    }

    #endregion
}
