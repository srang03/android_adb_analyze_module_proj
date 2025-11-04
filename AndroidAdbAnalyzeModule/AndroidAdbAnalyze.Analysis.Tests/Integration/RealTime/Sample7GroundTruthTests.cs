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
/// Sample 7 (7차 샘플) Ground Truth 검증 테스트
/// </summary>
/// <remarks>
/// 실제 로그 기반 Ground Truth (2025-10-17 10:33:00 ~ 10:50:59):
/// 
/// 기본 카메라 (PID 26279):
/// - 10:33:21-10:33:26 device 20 (촬영 없음)
/// - 10:33:50-10:34:00 device 20 (촬영 1개)
/// 
/// 카카오톡 (taskRootPackage=com.kakao.talk, PID 26279):
/// - 10:35:03-10:35:07 device 20 (촬영 없음)
/// - 10:35:58-10:36:07 device 20 (촬영 1개)
/// 
/// 텔레그램 (PID 31129):
/// - 10:37:27-10:37:39 device 0 (촬영 없음)
/// - 10:48:28-10:48:47 device 0 (촬영 1개)
/// 
/// 무음 카메라 (PID 1454):
/// - 10:49:39-10:49:44 device 0 (촬영 없음)
/// - 10:50:08-10:50:19 device 0 (촬영 1개)
/// 
/// Ground Truth (실제 로그 기반):
/// - 총 세션: 8개 (기본 카메라 2 + 카카오톡 2 + 텔레그램 2 + 무음 카메라 2)
/// - 총 촬영: 4개 (기본 카메라 1 + 카카오톡 1 + 텔레그램 1 + 무음 카메라 1)
/// 
/// 참고:
/// - 모든 세션이 데이터 시트와 일치함 (시간 차이 ±5초 이내)
/// - 카카오톡에서 실행한 카메라는 물리적으로 com.sec.android.app.camera이지만,
///   usagestats.log의 taskRootPackage=com.kakao.talk으로 카카오톡 세션으로 분류됨
/// </remarks>
public sealed class Sample7GroundTruthTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    private IAnalysisOrchestrator? _orchestrator;
    private List<NormalizedLogEvent>? _parsedEvents;
    
    // Ground Truth 상수 (실제 로그 기반)
    private const int ExpectedTotalSessions = 8;
    private const int ExpectedTotalCaptures = 4;
    private const int ExpectedDefaultCameraCaptures = 1;
    private const int ExpectedKakaoTalkCaptures = 1;
    private const int ExpectedTelegramCaptures = 1;
    private const int ExpectedSilentCameraCaptures = 1;
    
    // 샘플 디렉토리 경로
    private const string SampleDirectoryName = "7차 샘플_25_10_16";
    
    // 분석 시간 범위 (실제 로그 기준)
    private readonly DateTime _startTime = new(2025, 10, 17, 10, 33, 0);
    private readonly DateTime _endTime = new(2025, 10, 17, 10, 50, 59);

    // 아티팩트 가중치 (TestConstants에서 참조)
    private static readonly IReadOnlyDictionary<string, double> Weights = ArtifactWeights.Standard;

    public Sample7GroundTruthTests(ITestOutputHelper output)
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
        _output.WriteLine("=== Sample 7 (7차 샘플) Ground Truth 테스트 초기화 ===");
        
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
            "실제 로그에 따르면 8개의 카메라 세션이 있어야 함 (기본 카메라 2 + 카카오톡 2 + 텔레그램 2 + 무음 카메라 2)");

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
            "데이터 시트에 따르면 4개의 사진 촬영이 있어야 함 (각 앱에서 1개씩)");

        _output.WriteLine($"✓ 총 촬영 수: {result.CaptureEvents.Count} (예상: {ExpectedTotalCaptures})");
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
            "데이터 시트에 따르면 기본 카메라 촬영이 1개 있어야 함 (10:33:55)");

        _output.WriteLine($"✓ 기본 카메라 촬영: {defaultCameraCaptures} (예상: {ExpectedDefaultCameraCaptures})");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_KakaoTalkCaptures()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var kakaoTalkCaptures = result.CaptureEvents
            .Count(c => c.PackageName?.Contains("kakao", StringComparison.OrdinalIgnoreCase) == true);

        kakaoTalkCaptures.Should().Be(ExpectedKakaoTalkCaptures,
            "데이터 시트에 따르면 카카오톡 촬영이 1개 있어야 함 (10:36:02)");

        _output.WriteLine($"✓ 카카오톡 촬영: {kakaoTalkCaptures} (예상: {ExpectedKakaoTalkCaptures})");
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
            "데이터 시트에 따르면 텔레그램 촬영이 1개 있어야 함 (10:48:38)");

        _output.WriteLine($"✓ 텔레그램 촬영: {telegramCaptures} (예상: {ExpectedTelegramCaptures})");
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
            .Count(c => c.PackageName?.Contains("SilentCamera", StringComparison.OrdinalIgnoreCase) == true);

        silentCameraCaptures.Should().Be(ExpectedSilentCameraCaptures,
            "데이터 시트에 따르면 무음 카메라 촬영이 1개 있어야 함 (10:50:14)");

        _output.WriteLine($"✓ 무음 카메라 촬영: {silentCameraCaptures} (예상: {ExpectedSilentCameraCaptures})");
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
        foreach (var session in result.Sessions)
        {
            session.StartTime.Should().BeOnOrAfter(_startTime)
                .And.BeOnOrBefore(_endTime);
            
            session.EndTime.Should().NotBeNull("모든 세션은 종료 시간이 있어야 함");
            session.EndTime!.Value.Should().BeOnOrAfter(session.StartTime,
                "종료 시간은 시작 시간과 같거나 이후여야 함");
            
            session.Duration.Should().NotBeNull();
            session.Duration!.Value.TotalSeconds.Should().BeGreaterThanOrEqualTo(0,
                "세션 Duration은 0초 이상이어야 함");
            session.Duration!.Value.Should().BeLessThanOrEqualTo(30.Minutes(),
                "카메라 세션은 일반적으로 30분을 초과하지 않음");
            
            session.PackageName.Should().NotBeNullOrEmpty();
            session.SessionCompletenessScore.Should().BeInRange(0.3, 1.5,
                "세션 완전성 점수는 0.3 이상이어야 함 (MaxConfidence 캡핑 제거 후 실제 범위)");
            
            session.CaptureEventIds.Count.Should().BeLessThanOrEqualTo(1,
                "이 시나리오에서 각 세션은 최대 1개의 촬영만 포함");
        }

        _output.WriteLine($"✓ 모든 세션의 데이터 품질 검증 통과 ({result.Sessions.Count}개 세션)");
    }

    [Fact]
    public async Task Should_HaveValidCaptureData()
    {
        // Arrange
        var options = CreateAnalysisOptions();

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        foreach (var capture in result.CaptureEvents)
        {
            capture.CaptureTime.Should().BeOnOrAfter(_startTime)
                .And.BeOnOrBefore(_endTime);
            
            capture.PackageName.Should().NotBeNullOrEmpty();
            capture.CaptureDetectionScore.Should().BeInRange(0.15, 2.5,
                "촬영 탐지 점수는 최소 가중치(0.15) 이상이어야 함 (MaxConfidence 캡핑 제거)");
            
            capture.ParentSessionId.Should().NotBeEmpty("모든 촬영은 세션과 연결되어야 함");
        }

        _output.WriteLine($"✓ 모든 촬영의 데이터 품질 검증 통과 ({result.CaptureEvents.Count}개 촬영)");
    }

    [Fact]
    public async Task Should_Have_ValidPackageNames()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        var expectedPackages = new[]
        {
            "com.sec.android.app.camera",
            "com.kakao.talk",
            "org.telegram.messenger",
            "com.peace.SilentCamera"
        };

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var actualPackages = result.Sessions
            .Select(s => s.PackageName)
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct()
            .ToList();

        foreach (var expectedPackage in expectedPackages)
        {
            actualPackages.Should().Contain(pkg => 
                pkg.Contains(expectedPackage, StringComparison.OrdinalIgnoreCase),
                $"예상 패키지 {expectedPackage}가 세션에 있어야 함");
        }

        _output.WriteLine($"✓ 패키지 검증 통과: {string.Join(", ", actualPackages)}");
    }

    [Fact]
    public async Task Should_DetectCapture_WithExpectedTimestamps()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        
        // 예상 촬영 시각 (데이터 시트 기준, ±30초 허용)
        var expectedCaptureTimestamps = new[]
        {
            new DateTime(2025, 10, 17, 10, 33, 55), // 기본 카메라
            new DateTime(2025, 10, 17, 10, 36, 2),  // 카카오톡
            new DateTime(2025, 10, 17, 10, 48, 38), // 텔레그램
            new DateTime(2025, 10, 17, 10, 50, 14)  // 무음 카메라
        };

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        result.CaptureEvents.Should().HaveCount(expectedCaptureTimestamps.Length);

        foreach (var expectedTime in expectedCaptureTimestamps)
        {
            var matchingCapture = result.CaptureEvents
                .FirstOrDefault(c => Math.Abs((c.CaptureTime - expectedTime).TotalSeconds) <= 30);

            matchingCapture.Should().NotBeNull(
                $"예상 시각 {expectedTime:HH:mm:ss} (±30초)에 촬영이 감지되어야 함");

            if (matchingCapture != null)
            {
                _output.WriteLine($"✓ 촬영 감지: {matchingCapture.CaptureTime:HH:mm:ss} " +
                                $"(예상: {expectedTime:HH:mm:ss}, 차이: {(matchingCapture.CaptureTime - expectedTime).TotalSeconds:F1}초)");
            }
        }
    }

    #endregion

    #region YAML Configuration Tests

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
        // 실제 계산값: DATABASE_INSERT(0.5) + VIBRATION(0.4) + FOREGROUND_SERVICE(0.3) + PLAYER_CREATED(0.25) + MEDIA_EXTRACTOR(0.2) + CAMERA_ACTIVITY_REFRESH(0.15) + PLAYER_RELEASED(0.15) = 1.95
        var expectedScore = 1.95;
        var tolerance = 0.15; // 실제 점수 범위: 1.80~2.10

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
            "(MaxConfidence 캡핑 제거, 실제 계산값 1.95)");

        // 주요 아티팩트 검증
        capture.ArtifactTypes.Should().Contain("DATABASE_INSERT", 
            "secmedia DB 저장 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("VIBRATION_EVENT",
            "셔터/촬영 진동 이벤트가 탐지되어야 함");
        // PLAYER_EVENT는 일부 샘플에서만 발생 (일관성 없음)
        capture.ArtifactTypes.Should().Contain("PLAYER_CREATED", 
            "촬영 사운드 재생을 위한 플레이어 생성 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("PLAYER_RELEASED", 
            "사용한 플레이어 해제 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("CAMERA_ACTIVITY_REFRESH", 
            "카메라 Activity 갱신 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("MEDIA_EXTRACTOR", 
            "미디어 추출기 이벤트가 탐지되어야 함");

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
            "카카오톡 촬영이 1개 있어야 함");

        var capture = capturesWithScore.First();
        
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 카카오톡 (Camera2+CUA) 촬영 점수 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📅 촬영 시각: {capture.CaptureTime:HH:mm:ss.fff}");
        _output.WriteLine($"📊 실제 점수: {capture.CaptureDetectionScore:F2}");
        _output.WriteLine($"🎯 예상 점수: {expectedScore:F2} (±{tolerance:F2})");
        _output.WriteLine($"📦 CaptureId: {capture.CaptureId}");
        
        // 공통 메서드 사용하여 아티팩트 및 점수 계산 출력
        ArtifactWeights.WriteScoreCalculation(_output, capture.ArtifactTypes, Weights);
        
        _output.WriteLine($"ℹ️  특징: DATABASE_INSERT 없음 (In-App Camera)");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // 점수 검증
        capture.CaptureDetectionScore.Should().BeInRange(expectedScore - tolerance, expectedScore + tolerance,
            $"카카오톡 촬영 점수는 {expectedScore:F2} ± {tolerance:F2} 범위여야 함 " +
            "(실제 계산값 1.50이지만 MaxConfidence=1.0으로 캡핑됨)");

        // 주요 아티팩트 검증
        capture.ArtifactTypes.Should().Contain("VIBRATION_EVENT",
            "셔터/촬영 진동 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("URI_PERMISSION_GRANT",
            "URI_PERMISSION_GRANT 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("PLAYER_CREATED",
            "PLAYER_CREATED 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("MEDIA_EXTRACTOR",
            "MEDIA_EXTRACTOR 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("CAMERA_ACTIVITY_REFRESH", 
            "카메라 Activity 갱신 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("PLAYER_RELEASED", 
            "미디어 추출기 이벤트가 탐지되어야 함");

        _output.WriteLine($"\n✅ 카카오톡 촬영 점수 검증 완료");
    }

    [Fact]
    public async Task Should_Match_GroundTruth_Telegram_CaptureScore()
    {
        // Arrange
        var options = CreateAnalysisOptions();
        var expectedScore = 0.75; // VIBRATION(0.4) + CAMERA_ACTIVITY_REFRESH(0.15) + MEDIA_EXTRACTOR(0.2)
        var tolerance = 0.1;

        // Act
        var result = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);

        // Assert
        var telegramSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("telegram", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        
        var capturesWithScore = result.CaptureEvents
            .Where(c => telegramSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)))
            .ToList();

        capturesWithScore.Should().HaveCount(ExpectedTelegramCaptures,
            "텔레그램 촬영이 1개 있어야 함");

        var capture = capturesWithScore.First();
        
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 텔레그램 (CameraX) 촬영 점수 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📅 촬영 시각: {capture.CaptureTime:HH:mm:ss.fff}");
        _output.WriteLine($"📊 실제 점수: {capture.CaptureDetectionScore:F2}");
        _output.WriteLine($"🎯 예상 점수: {expectedScore:F2} (±{tolerance:F2})");
        _output.WriteLine($"📦 CaptureId: {capture.CaptureId}");
        
        // 공통 메서드 사용하여 아티팩트 및 점수 계산 출력
        ArtifactWeights.WriteScoreCalculation(_output, capture.ArtifactTypes, Weights);
        
        _output.WriteLine($"ℹ️  특징: VIBRATION 'usage: TOUCH' + 공통 아티팩트만 탐지");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // 점수 검증
        capture.CaptureDetectionScore.Should().BeInRange(expectedScore - tolerance, expectedScore + tolerance,
            $"텔레그램 촬영 점수는 {expectedScore:F2} ± {tolerance:F2} 범위여야 함");

        // 주요 아티팩트 검증
        capture.ArtifactTypes.Should().Contain("VIBRATION_EVENT",
            "텔레그램 특유의 TOUCH 진동이 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("CAMERA_ACTIVITY_REFRESH", 
            "카메라 Activity 갱신 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("MEDIA_EXTRACTOR", 
            "미디어 추출기 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().NotContain("DATABASE_INSERT",
            "텔레그램은 DATABASE_INSERT가 없어야 함");
        capture.ArtifactTypes.Should().NotContain("PLAYER_EVENT",
            "텔레그램은 셔터음이 없어야 함");

        _output.WriteLine($"\n✅ 텔레그램 촬영 점수 검증 완료");
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
            "무음 카메라 특화 아티팩트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("CAMERA_ACTIVITY_REFRESH", 
            "카메라 Activity 갱신 이벤트가 탐지되어야 함");
        capture.ArtifactTypes.Should().Contain("VIBRATION_EVENT", 
            "Ground Truth에 따르면 예상치 못한 진동이 탐지되어야 함");
        capture.ArtifactTypes.Should().NotContain("DATABASE_INSERT",
            "무음 카메라는 DATABASE_INSERT가 없어야 함");
        capture.ArtifactTypes.Should().NotContain("PLAYER_EVENT",
            "무음 카메라는 셔터음이 없어야 함");

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
        _output.WriteLine($"=== 모든 촬영의 최소 임계값 검증 ===");
        _output.WriteLine($"최소 임계값: {minThreshold:F2}");
        _output.WriteLine($"총 촬영 수: {result.CaptureEvents.Count}");
        _output.WriteLine("");

        foreach (var capture in result.CaptureEvents.OrderBy(c => c.CaptureTime))
        {
            _output.WriteLine($"[{capture.PackageName}] {capture.CaptureTime:HH:mm:ss} - 점수: {capture.CaptureDetectionScore:F2}");
            
            capture.CaptureDetectionScore.Should().BeGreaterThanOrEqualTo(minThreshold,
                $"{capture.PackageName}의 촬영 점수는 최소 임계값 {minThreshold:F2} 이상이어야 함");
        }

        _output.WriteLine($"\n✅ 모든 촬영이 최소 임계값을 충족함");
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
        _output.WriteLine($"총 촬영 수: {result.CaptureEvents.Count}");
        _output.WriteLine("");

        var scoreGroups = result.CaptureEvents
            .GroupBy(c => c.CaptureDetectionScore >= 1.0 ? "높음(≥1.0)" :
                         c.CaptureDetectionScore >= 0.7 ? "중간(0.7-0.99)" :
                         c.CaptureDetectionScore >= 0.5 ? "보통(0.5-0.69)" : "낮음(<0.5)")
            .OrderByDescending(g => g.Key);

        foreach (var group in scoreGroups)
        {
            _output.WriteLine($"{group.Key}: {group.Count()}개");
            foreach (var capture in group.OrderBy(c => c.CaptureTime))
            {
                _output.WriteLine($"  - [{capture.PackageName}] {capture.CaptureTime:HH:mm:ss}: {capture.CaptureDetectionScore:F2}");
                _output.WriteLine($"    아티팩트: {string.Join(", ", capture.ArtifactTypes)}");
            }
            _output.WriteLine("");
        }

        // 통계 정보
        var avgScore = result.CaptureEvents.Average(c => c.CaptureDetectionScore);
        var maxScore = result.CaptureEvents.Max(c => c.CaptureDetectionScore);
        var minScore = result.CaptureEvents.Min(c => c.CaptureDetectionScore);

        _output.WriteLine($"📊 통계:");
        _output.WriteLine($"  평균 점수: {avgScore:F2}");
        _output.WriteLine($"  최고 점수: {maxScore:F2}");
        _output.WriteLine($"  최저 점수: {minScore:F2}");
        
        _output.WriteLine($"\n✅ 촬영 점수 분포 분석 완료");
    }

    #endregion

    #region Ground Truth 문서 자동 생성 (논문용)

    [Fact]
    public async Task Generate_GroundTruth_Document()
    {
        var options = CreateAnalysisOptions();
        var sampleInfo = new ArtifactWeights.SampleInfo(
            SampleNumber: 7,
            SampleName: "7차 샘플",
            TestDate: new DateTime(2025, 10, 17),
            TimeRange: (_startTime, _endTime),
            Description: "기본 카메라, 카카오톡, 텔레그램, 무음 카메라 사용 (총 4회 촬영)"
        );

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== Ground Truth 문서 자동 생성 (실제 분석 결과 기반) ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine($"📋 샘플: {sampleInfo.SampleName}");
        _output.WriteLine($"📅 날짜: {sampleInfo.TestDate:yyyy-MM-dd}");
        _output.WriteLine($"⏰ 시간: {sampleInfo.TimeRange.Start:HH:mm:ss} ~ {sampleInfo.TimeRange.End:HH:mm:ss}");
        _output.WriteLine("");

        _output.WriteLine("🔄 1단계: 실제 로그 분석 실행 중...");
        var analysisResult = await _orchestrator!.AnalyzeAsync(_parsedEvents!, options);
        analysisResult.Should().NotBeNull("분석 결과가 반환되어야 함");
        analysisResult.Success.Should().BeTrue("분석이 성공해야 함");
        _output.WriteLine($"✅ 분석 완료: 세션 {analysisResult.Sessions.Count}개, 촬영 {analysisResult.CaptureEvents.Count}개");
        _output.WriteLine("");

        _output.WriteLine("📄 2단계: GT 문서 생성 중...");
        var gtDocument = ArtifactWeights.GroundTruthDocumentGenerator.GenerateDocument(analysisResult, sampleInfo, Weights);
        gtDocument.Should().NotBeNullOrEmpty("GT 문서가 생성되어야 함");
        _output.WriteLine($"✅ GT 문서 생성 완료: {gtDocument.Length} 문자");
        _output.WriteLine("");

        _output.WriteLine("💾 3단계: 파일 저장 중...");
        var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".."));
        var docDir = Path.Combine(projectRoot, "Documentation", "GroundTruth");
        if (!Directory.Exists(docDir)) Directory.CreateDirectory(docDir);
        var outputPath = Path.Combine(docDir, $"Sample{sampleInfo.SampleNumber}_Ground_Truth.md");
        await File.WriteAllTextAsync(outputPath, gtDocument);
        _output.WriteLine($"✅ 파일 저장 완료: {outputPath}");

        _output.WriteLine("🔍 4단계: GT 문서 검증 중...");
        File.Exists(outputPath).Should().BeTrue("GT 문서 파일이 존재해야 함");
        gtDocument.Should().Contain($"# Sample {sampleInfo.SampleNumber}", "헤더가 있어야 함");
        gtDocument.Should().Contain($"**총 세션 수**: {ExpectedTotalSessions}개", "실제 세션 수가 Ground Truth와 일치해야 함");
        gtDocument.Should().Contain($"**총 촬영 수**: {ExpectedTotalCaptures}개", "실제 촬영 수가 Ground Truth와 일치해야 함");
        _output.WriteLine("  ✓ 검증 완료");

        _output.WriteLine("");
        _output.WriteLine("✅ GT 문서 생성 및 검증 완료");
        _output.WriteLine($"📁 저장 위치: {outputPath}");
        _output.WriteLine("════════════════════════════════════════════════════════════");
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
        
        // AndroidAdbAnalysis 서비스 등록 (기본 설정)
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
        var logger = loggerFactory.CreateLogger<Sample7GroundTruthTests>();
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

