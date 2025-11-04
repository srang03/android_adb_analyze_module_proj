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
using static AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants.ArtifactWeights;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Preliminary;

/// <summary>
/// Preliminary Test 1 (예비 실험 1차) Ground Truth 검증 테스트
/// </summary>
/// <remarks>
/// 실제 로그 기반 Ground Truth (2025-09-01 09:45:00 ~ 09:53:00):
/// 
/// 기본 카메라:
/// - 09:45:32-09:45:37 (촬영 없음)
/// - 09:46:22-09:46:32 (촬영 1개, 09:46:26)
/// 
/// 카카오톡:
/// - 09:47:27-09:47:31 (촬영 없음)
/// - 09:48:29-09:48:38 (촬영 1개, 09:48:32)
/// 
/// 텔레그램:
/// - 09:49:24-09:49:36 (촬영 없음)
/// - 09:50:27-09:50:46 (촬영 1개, 09:50:36)
/// 
/// 무음 카메라:
/// - 09:51:22-09:51:27 (촬영 없음)
/// - 09:52:04-09:52:14 (촬영 1개, 09:52:08)
/// 
/// Ground Truth (실제 로그 기반):
/// - 총 세션: 8개 (기본 카메라 2 + 카카오톡 2 + 텔레그램 2 + 무음 카메라 2)
/// - 총 촬영: 4개 (기본 카메라 1 + 카카오톡 1 + 텔레그램 1 + 무음 카메라 1)
/// </remarks>
public sealed class Preliminary1_25_09_01_GroundTruthTests : IAsyncLifetime
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
    private const string SampleDirectoryName = "예비 실험/예비 실험 1차 25_09_01";
    
    // 분석 시간 범위
    private readonly DateTime _startTime = new(2025, 9, 1, 9, 45, 0);
    private readonly DateTime _endTime = new(2025, 9, 1, 9, 53, 0);

    public Preliminary1_25_09_01_GroundTruthTests(ITestOutputHelper output)
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
        _output.WriteLine("=== Preliminary Test 1 (예비 실험 1차) Ground Truth 테스트 초기화 ===");
        
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
        
        // 세션별 상세 정보 출력
        _output.WriteLine($"\n📋 탐지된 세션 목록:");
        foreach (var session in result.Sessions.OrderBy(s => s.StartTime))
        {
            var captureCount = session.CaptureEventIds.Count;
            var captureIndicator = captureCount > 0 ? $"📸 {captureCount}개 촬영" : "촬영 없음";
            _output.WriteLine($"  {session.StartTime:HH:mm:ss} - {session.EndTime:HH:mm:ss} | {session.PackageName} | {captureIndicator}");
        }
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
            "데이터 시트에 따르면 4개의 사진 촬영이 있어야 함 (기본 카메라 1 + 카카오톡 1 + 텔레그램 1 + 무음 카메라 1)");

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
        // 세션 기반으로 정확히 분류 (In-App Camera 제외)
        var defaultCameraSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("com.sec.android.app.camera", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        
        var defaultCameraCaptures = result.CaptureEvents
            .Count(c => defaultCameraSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)));
        
        defaultCameraCaptures.Should().Be(ExpectedDefaultCameraCaptures,
            "데이터 시트에 따르면 기본 카메라 촬영이 1개 있어야 함 (09:46:26)");

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
        // 세션 기반으로 정확히 분류 (taskRootPackage 고려)
        var kakaoSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("kakao", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        
        var kakaoCaptures = result.CaptureEvents
            .Count(c => kakaoSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)));
        
        kakaoCaptures.Should().Be(ExpectedKakaoTalkCaptures,
            "데이터 시트에 따르면 카카오톡 촬영이 1개 있어야 함 (09:48:32)");

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
        // 세션 기반으로 정확히 분류
        var telegramSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("telegram", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        
        var telegramCaptures = result.CaptureEvents
            .Count(c => telegramSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)));
        
        telegramCaptures.Should().Be(ExpectedTelegramCaptures,
            "데이터 시트에 따르면 텔레그램 촬영이 1개 있어야 함 (09:50:36)");

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
        // 세션 기반으로 정확히 분류
        var silentCameraSessions = result.Sessions
            .Where(s => s.PackageName?.Contains("Silent", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        
        var silentCameraCaptures = result.CaptureEvents
            .Count(c => silentCameraSessions.Any(s => s.CaptureEventIds.Contains(c.CaptureId)));
        
        silentCameraCaptures.Should().Be(ExpectedSilentCameraCaptures,
            "데이터 시트에 따르면 무음 카메라 촬영이 1개 있어야 함 (09:52:08)");

        _output.WriteLine($"✓ 무음 카메라 촬영 수: {silentCameraCaptures} (예상: {ExpectedSilentCameraCaptures})");
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
            SampleNumber: 0,  // 예비 실험은 0으로 표시
            SampleName: "예비 실험 1차",
            TestDate: new DateTime(2025, 9, 1),
            TimeRange: (_startTime, _endTime),
            Description: "기본 카메라, 카카오톡, 텔레그램, 무음 카메라 사용 (총 4회 촬영) - 예비 실험"
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
            Standard);

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

        var outputPath = Path.Combine(docDir, "Preliminary1_Ground_Truth.md");
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
        gtDocument.Should().Contain("# Sample 0", "헤더가 있어야 함");
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
            session.StartTime.Should().BeOnOrAfter(_startTime)
                .And.BeOnOrBefore(_endTime);

            // 종료 시간 검증
            session.EndTime!.Value.Should().BeOnOrAfter(session.StartTime,
                "종료 시간은 시작 시간과 같거나 이후여야 함");

            // Duration 검증
            session.Duration!.Value.TotalSeconds.Should().BeGreaterThanOrEqualTo(0,
                "세션 Duration은 0초 이상이어야 함");

            // PackageName 검증
            session.PackageName.Should().NotBeNullOrEmpty("모든 세션은 패키지명을 가져야 함");

            // SessionCompletenessScore 검증
            session.SessionCompletenessScore.Should().BeInRange(0.3, 1.5,
                "세션 완전성 점수는 0.3 이상이어야 함");
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
            capture.CaptureTime.Should().BeOnOrAfter(_startTime)
                .And.BeOnOrBefore(_endTime);

            // CaptureDetectionScore 검증 (핵심 아티팩트 존재 기반, 임계값 제거)
            capture.CaptureDetectionScore.Should().BeGreaterThan(0,
                "촬영 탐지 점수는 0보다 커야 함");

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
        var logger = loggerFactory.CreateLogger<Preliminary1_25_09_01_GroundTruthTests>();
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
        
        // Orchestration
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();
    }

    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync()
    {
        var samplePath = Path.Combine(_sampleLogsPath, SampleDirectoryName);
        
        if (!Directory.Exists(samplePath))
        {
            throw new DirectoryNotFoundException($"Sample logs directory not found: {samplePath}");
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
            EventCorrelationWindow = TimeSpan.FromSeconds(30),
            MaxSessionGap = TimeSpan.FromMinutes(5),
            EnableIncompleteSessionHandling = true,
            DeduplicationSimilarityThreshold = 0.8
        };
    }

    #endregion
}

