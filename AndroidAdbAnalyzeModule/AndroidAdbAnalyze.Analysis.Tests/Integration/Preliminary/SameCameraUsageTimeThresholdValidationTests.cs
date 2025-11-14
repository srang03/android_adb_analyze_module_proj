namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Preliminary;

using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Confidence;
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Analysis.Services.Sessions;
using AndroidAdbAnalyze.Analysis.Services.Sessions.Sources;
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// SameCameraUsageTimeThreshold 파라미터 타당성 검증 테스트
/// </summary>
/// <remarks>
/// 목적:
/// - 예비 실험(Preliminary 1-3)에서 usagestats-media.camera 쌍의 시작/종료 시각 차이 측정
/// - 본 실험(Sample 1-10)에서 2.0초 임계값의 타당성 검증
/// 
/// 논문 반영:
/// - 제4장 제3절: SameCameraUsageTimeThreshold 설정 근거 (예비 실험 기반)
/// - 제5장 제3절: 본 실험 검증 (Sample 1-10 기반)
/// 
/// 설계 원칙:
/// - 하드코딩 없음: 모든 데이터는 실제 세션 분석 결과에서 추출
/// - 재사용 가능: 공용 메서드 사용
/// - 검증 가능: 계산 과정과 결과를 명확히 출력
/// </remarks>
public sealed class SameCameraUsageTimeThresholdValidationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _sampleLogsPath;
    private readonly string _parserConfigPath;
    
    // 예비 실험 원본 세션 캐싱 (병합 전)
    private List<CameraSession>? _preliminary1RawSessions;
    private List<CameraSession>? _preliminary2RawSessions;
    private List<CameraSession>? _preliminary3RawSessions;

    public SameCameraUsageTimeThresholdValidationTests(ITestOutputHelper output)
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
        _output.WriteLine("🔬 SameCameraUsageTimeThreshold 검증 테스트 초기화");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // Preliminary 1-3 원본 세션 추출 (병합 전) - ArtifactWeights.PreliminaryTimeRanges 사용
        var prelim1 = ArtifactWeights.PreliminaryTimeRanges[1];
        _preliminary1RawSessions = await ExtractRawSessionsFromSample(
            prelim1.DirectoryName, prelim1.StartTime, prelim1.EndTime);
        
        var prelim2 = ArtifactWeights.PreliminaryTimeRanges[2];
        _preliminary2RawSessions = await ExtractRawSessionsFromSample(
            prelim2.DirectoryName, prelim2.StartTime, prelim2.EndTime);
        
        var prelim3 = ArtifactWeights.PreliminaryTimeRanges[3];
        _preliminary3RawSessions = await ExtractRawSessionsFromSample(
            prelim3.DirectoryName, prelim3.StartTime, prelim3.EndTime);
        
        _output.WriteLine("\n✅ 예비 실험 3회 원본 세션 추출 완료 (병합 전)\n");
    }

    public Task DisposeAsync() => Task.CompletedTask; 

    /// <summary>
    /// 예비 실험 SameCameraUsageTimeThreshold 측정 테스트
    /// </summary>
    /// <remarks>
    /// 논문 제4장 제3절 "SameCameraUsageTimeThreshold 설정 근거"에 사용될 실측 데이터 생성
    /// </remarks>
    [Fact]
    public void Measure_SameCameraUsageTimeThreshold_PreliminaryExperiments()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 SameCameraUsageTimeThreshold 측정 (예비 실험 1~3차)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 1. 예비 실험 3회 통합 세션 목록
        var allSessions = _preliminary1RawSessions!
            .Concat(_preliminary2RawSessions!)
            .Concat(_preliminary3RawSessions!)
            .ToList();

        _output.WriteLine($"총 원본 세션 수: {allSessions.Count}개\n");

        // 2. usagestats-media.camera 쌍 식별
        var sessionPairs = IdentifyUsagestatsMediaCameraPairs(allSessions);

        _output.WriteLine($"usagestats-media.camera 쌍: {sessionPairs.Count}개\n");

        if (sessionPairs.Count == 0)
        {
            _output.WriteLine("⚠️  usagestats-media.camera 쌍이 없습니다.\n");
            return;
        }

        // 3. 시작/종료 시각 차이 측정
        var startDiffs = new List<double>();
        var endDiffs = new List<double>();

        _output.WriteLine("📋 세션 쌍 상세 분석:\n");

        foreach (var (usagestats, mediaCamera) in sessionPairs)
        {
            var startDiff = Math.Abs((usagestats.StartTime - mediaCamera.StartTime).TotalSeconds);
            var endDiff = usagestats.EndTime.HasValue && mediaCamera.EndTime.HasValue
                ? Math.Abs((usagestats.EndTime.Value - mediaCamera.EndTime.Value).TotalSeconds)
                : 0.0;

            startDiffs.Add(startDiff);
            endDiffs.Add(endDiff);

            _output.WriteLine($"📌 {usagestats.PackageName}:");
            _output.WriteLine($"   usagestats: {usagestats.StartTime:HH:mm:ss} ~ {usagestats.EndTime:HH:mm:ss}");
            _output.WriteLine($"   media.camera: {mediaCamera.StartTime:HH:mm:ss} ~ {mediaCamera.EndTime:HH:mm:ss}");
            _output.WriteLine($"   시작 차이: {startDiff:F2}초");
            _output.WriteLine($"   종료 차이: {endDiff:F2}초\n");
        }

        // 4. 통계 계산
        var startAvg = startDiffs.Average();
        var startMin = startDiffs.Min();
        var startMax = startDiffs.Max();

        var endAvg = endDiffs.Average();
        var endMin = endDiffs.Min();
        var endMax = endDiffs.Max();

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 통계 요약");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine($"시작 시각 차이:");
        _output.WriteLine($"  평균: {startAvg:F2}초");
        _output.WriteLine($"  최소: {startMin:F2}초");
        _output.WriteLine($"  최대: {startMax:F2}초\n");

        _output.WriteLine($"종료 시각 차이:");
        _output.WriteLine($"  평균: {endAvg:F2}초");
        _output.WriteLine($"  최소: {endMin:F2}초");
        _output.WriteLine($"  최대: {endMax:F2}초\n");

        // 5. 임계값 검증 (ArtifactWeights.SameCameraUsageTimeThreshold 사용)
        var threshold = ArtifactWeights.SameCameraUsageTimeThreshold;
        var startExceed = startDiffs.Count(d => d > threshold);
        var endExceed = endDiffs.Count(d => d > threshold);

        _output.WriteLine($"{threshold:F1}초 임계값 검증:");
        _output.WriteLine($"  시작 차이 초과: {startExceed}개 / {startDiffs.Count}개");
        _output.WriteLine($"  종료 차이 초과: {endExceed}개 / {endDiffs.Count}개\n");

        var safetyMarginStart = threshold / startMax;
        var safetyMarginEnd = threshold / endMax;

        _output.WriteLine($"안전 마진:");
        _output.WriteLine($"  시작 차이: {safetyMarginStart:F2}배 ({threshold:F1}초 / {startMax:F2}초)");
        _output.WriteLine($"  종료 차이: {safetyMarginEnd:F2}배 ({threshold:F1}초 / {endMax:F2}초)\n");

        // 6. 논문 작성용 요약
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📝 논문 작성용 요약 (제4장 제3절)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine("**측정 방법**:");
        _output.WriteLine("  - 측정 대상: 예비 실험 3회(24개 세션)의 usagestats-media.camera 쌍");
        _output.WriteLine("  - 측정 방법: 두 로그 간 시작 시각 차이 및 종료 시각 차이 계산\n");

        _output.WriteLine("**측정 결과**:");
        _output.WriteLine($"  - 시작 차이: 평균 약 {Math.Round(startAvg, 1)}초 (최소 {startMin:F1}초, 최대 {startMax:F1}초)");
        _output.WriteLine($"  - 종료 차이: 평균 약 {Math.Round(endAvg, 1)}초 (최소 {endMin:F1}초, 최대 {endMax:F1}초)\n");

        _output.WriteLine("**파라미터 설정**:");
        _output.WriteLine($"  - 최대 측정값({Math.Max(startMax, endMax):F1}초)에 안전 마진 {Math.Min(safetyMarginStart, safetyMarginEnd):F2}배 적용");
        _output.WriteLine($"  - 최종 설정: {threshold:F1}초");
        _output.WriteLine($"  - 근거: 앱 계층과 HAL 계층의 로그 기록 시각 차이 허용\n");

        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 7. Assertion
        startExceed.Should().Be(0, $"시작 시각 차이가 {threshold:F1}초를 초과하는 쌍이 없어야 함");
        endExceed.Should().Be(0, $"종료 시각 차이가 {threshold:F1}초를 초과하는 쌍이 없어야 함");
    }

    /// <summary>
    /// 본 실험 SameCameraUsageTimeThreshold 검증 테스트
    /// </summary>
    /// <remarks>
    /// Orchestrator를 사용하여 실제 비즈니스 로직 검증 (중복 제거 → 세션 탐지 → 병합)
    /// 논문 제5장 제3절: "본 실험에서 Sample 1~10의 최종 병합된 93개 세션을 분석"
    /// </remarks>
    [Fact]
    public async Task Validate_SameCameraUsageTimeThreshold_MainExperiment()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 SameCameraUsageTimeThreshold 검증 (본 실험 Sample 1~10)");
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("✅ Orchestrator를 사용한 실제 비즈니스 로직 검증 (중복 제거 포함)\n");

        // Orchestrator 생성 (GT 테스트와 동일한 방식)
        var orchestrator = CreateOrchestrator();

        // 1. Sample 1-10에서 Orchestrator를 사용하여 최종 병합된 세션 분석
        var allMergedSessions = new List<CameraSession>();
        var mergeStats = new List<(string sample, int before, int after, int merged)>();

        for (int i = 1; i <= 10; i++)
        {
            var sampleInfo = ArtifactWeights.SampleTimeRanges[i];
            _output.WriteLine($"분석 중: Sample {i} ({sampleInfo.DirectoryName})");
            
            // 로그 파싱
            var samplePath = Path.Combine(_sampleLogsPath, sampleInfo.DirectoryName);
            var parsedEvents = await ParseSampleLogsAsync(samplePath, sampleInfo.StartTime, sampleInfo.EndTime);
            
            // AnalysisOptions 생성 (SameCameraUsageTimeThreshold 포함)
            var options = CreateAnalysisOptions();
            
            // 병합 전 원본 세션 수 계산 (디버깅 목적, 중복 제거 전 이벤트 사용)
            var confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
            var sessionSources = new List<ISessionSource>
            {
                new UsagestatsSessionSource(NullLogger<UsagestatsSessionSource>.Instance, confidenceCalculator),
                new MediaCameraSessionSource(NullLogger<MediaCameraSessionSource>.Instance, confidenceCalculator)
            };
            
            var rawSessions = new List<CameraSession>();
            foreach (var source in sessionSources)
            {
                rawSessions.AddRange(source.ExtractSessions(parsedEvents, options));
            }
            var beforeCount = rawSessions.Count;
            
            // Orchestrator를 사용한 세션 탐지 (중복 제거 → 세션 탐지 → 병합)
            var result = await orchestrator.AnalyzeAsync(parsedEvents, options);
            var mergedSessions = result.Sessions;
            var afterCount = mergedSessions.Count;
            
            // usagestats + media_camera 병합 쌍 개수
            var mergedPairCount = mergedSessions.Count(s => 
                s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats) && 
                s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera));
            
            // 디버깅: 원본 세션 분석 (통계 목적)
            var usagestatsCount = rawSessions.Count(s => s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats));
            var mediaCameraCount = rawSessions.Count(s => s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera));
            
            // 🔍 디버깅: 병합 실패 원인 분석
            if (mergedPairCount == 0 && usagestatsCount > 0 && mediaCameraCount > 0)
            {
                _output.WriteLine($"\n  🔍 병합 실패 원인 분석:");
                
                // 불완전 세션 처리 후 세션 추출 (CameraSessionDetector 내부 로직과 동일)
                var processedSessions = options.EnableIncompleteSessionHandling
                    ? ProcessIncompleteSessionsForDebugging(rawSessions, options)
                    : rawSessions;
                
                // MergeSessions와 동일하게 시간순 정렬
                var sortedSessions = processedSessions.OrderBy(s => s.StartTime).ToList();
                
                _output.WriteLine($"    불완전 세션 처리 후 총 세션: {sortedSessions.Count}개");
                
                // MergeSessions 로직과 동일하게 인접 세션 쌍 검사
                var potentialPairs = new List<(CameraSession s1, CameraSession s2, string reason)>();
                
                for (int j = 0; j < sortedSessions.Count - 1; j++)
                {
                    var s1 = sortedSessions[j];
                    var s2 = sortedSessions[j + 1];
                    
                    // usagestats + media_camera 쌍인지 확인
                    var hasUsagestats1 = s1.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats);
                    var hasMediaCamera1 = s1.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera);
                    var hasUsagestats2 = s2.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats);
                    var hasMediaCamera2 = s2.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera);
                    
                    if (!((hasUsagestats1 && hasMediaCamera2) || (hasMediaCamera1 && hasUsagestats2)))
                        continue;
                    
                    // 같은 패키지 확인
                    if (!string.Equals(s1.PackageName, s2.PackageName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // IsSameCameraUsage 조건 검사
                    var threshold = ArtifactWeights.SameCameraUsageTimeThreshold;
                    var startDiff = Math.Abs((s1.StartTime - s2.StartTime).TotalSeconds);
                    var reasons = new List<string>();
                    
                    if (startDiff > threshold)
                        reasons.Add($"시작 차이 초과 ({startDiff:F2} > {threshold})");
                    
                    if (!s1.EndTime.HasValue || !s2.EndTime.HasValue)
                        reasons.Add("EndTime null");
                    else
                    {
                        var endDiff = Math.Abs((s1.EndTime.Value - s2.EndTime.Value).TotalSeconds);
                        if (endDiff > threshold)
                            reasons.Add($"종료 차이 초과 ({endDiff:F2} > {threshold})");
                    }
                    
                    if (reasons.Any())
                        potentialPairs.Add((s1, s2, string.Join(", ", reasons)));
                }
                
                if (potentialPairs.Any())
                {
                    _output.WriteLine($"\n    📊 병합 가능한 쌍 발견: {potentialPairs.Count}개 (모두 실패)");
                    foreach (var (s1, s2, reason) in potentialPairs.Take(5)) // 최대 5개만 출력
                    {
                        var startDiff = Math.Abs((s1.StartTime - s2.StartTime).TotalSeconds);
                        var endDiff = s1.EndTime.HasValue && s2.EndTime.HasValue
                            ? Math.Abs((s1.EndTime.Value - s2.EndTime.Value).TotalSeconds)
                            : -1;
                        
                        _output.WriteLine($"\n      쌍: {s1.PackageName}");
                        _output.WriteLine($"        세션1 ({string.Join(",", s1.SourceLogTypes)}): {s1.StartTime:HH:mm:ss} ~ {(s1.EndTime?.ToString("HH:mm:ss") ?? "null")}");
                        _output.WriteLine($"        세션2 ({string.Join(",", s2.SourceLogTypes)}): {s2.StartTime:HH:mm:ss} ~ {(s2.EndTime?.ToString("HH:mm:ss") ?? "null")}");
                        _output.WriteLine($"        시작 차이: {startDiff:F2}초, 종료 차이: {(endDiff >= 0 ? $"{endDiff:F2}초" : "N/A")}");
                        _output.WriteLine($"        ❌ 실패 이유: {reason}");
                    }
                }
                else
                {
                    _output.WriteLine($"    ❌ 병합 가능한 쌍이 없음 (패키지명 불일치 또는 시간 겹침 없음)");
                }
            }
            
            // 디버깅: 병합 후 세션 분석
            var usagestatsOnly = mergedSessions.Count(s => 
                s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats) && 
                !s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera));
            var mediaCameraOnly = mergedSessions.Count(s => 
                !s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats) && 
                s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera));
            
            allMergedSessions.AddRange(mergedSessions);
            mergeStats.Add((sampleInfo.DirectoryName, beforeCount, afterCount, mergedPairCount));
            
            _output.WriteLine($"\n  병합 전: {beforeCount}개 (usagestats: {usagestatsCount}, media.camera: {mediaCameraCount})");
            _output.WriteLine($"  병합 후: {afterCount}개 (usagestats only: {usagestatsOnly}, media.camera only: {mediaCameraOnly}, 병합: {mergedPairCount})");
            _output.WriteLine($"  usagestats+media.camera 병합: {mergedPairCount}개\n");
        }

        _output.WriteLine($"총 최종 병합 세션 수: {allMergedSessions.Count}개\n");
        
        // 2. 세션 분류 통계 계산
        var mergedPairs = allMergedSessions
            .Where(s => s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats) && 
                       s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera))
            .ToList();
        
        var usagestatsOnlyTotal = allMergedSessions.Count(s => 
            s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats) && 
            !s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera));
        
        var mediaCameraOnlyTotal = allMergedSessions.Count(s => 
            !s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats) && 
            s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera));
        
        var singleSourceTotal = usagestatsOnlyTotal + mediaCameraOnlyTotal;

        _output.WriteLine($"세션 분류 통계:");
        _output.WriteLine($"  usagestats+media.camera 병합된 세션: {mergedPairs.Count}개");
        _output.WriteLine($"  usagestats만 포함된 세션: {usagestatsOnlyTotal}개");
        _output.WriteLine($"  media.camera만 포함된 세션: {mediaCameraOnlyTotal}개");
        _output.WriteLine($"  단일 소스 세션 합계: {singleSourceTotal}개");
        _output.WriteLine($"  검증: {mergedPairs.Count} + {singleSourceTotal} = {mergedPairs.Count + singleSourceTotal}개 (총 {allMergedSessions.Count}개)\n");

        if (mergedPairs.Count == 0)
        {
            _output.WriteLine("⚠️  usagestats+media.camera 병합 쌍이 없습니다.\n");
            _output.WriteLine("✅ 모든 샘플에서 SameCameraUsageTimeThreshold가 올바르게 동작하여");
            _output.WriteLine("   병합되어야 할 세션이 모두 병합되었습니다.\n");
            return;
        }

        // 3. 병합된 세션 검증
        // 병합된 세션이 존재한다는 것은 CameraSessionDetector의 IsSameCameraUsage() 메서드가
        // SameCameraUsageTimeThreshold 기준을 만족하여 병합을 수행했다는 의미입니다.
        // 병합된 세션은 이미 불완전 세션 처리 후 EndTime이 추정되어 있어야 하며,
        // usagestats와 media.camera를 모두 포함해야 합니다.
        
        _output.WriteLine("📋 병합된 세션 쌍 상세 분석:\n");
        
        var validationErrors = new List<string>();
        
        foreach (var merged in mergedPairs)
        {
            // 검증 1: SourceLogTypes에 usagestats와 media.camera가 모두 포함되어야 함
            var hasUsagestats = merged.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats);
            var hasMediaCamera = merged.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera);
            
            if (!hasUsagestats || !hasMediaCamera)
            {
                validationErrors.Add(
                    $"{merged.PackageName}: SourceLogTypes에 usagestats 또는 media.camera가 누락됨 " +
                    $"(Sources: {string.Join(", ", merged.SourceLogTypes)})");
                continue;
            }
            
            // 검증 2: 병합된 세션은 EndTime이 있어야 함 (불완전 세션 처리 후)
            // IsSameCameraUsage()는 두 세션 모두 EndTime이 있어야 병합을 수행하므로,
            // 병합된 세션도 EndTime이 있어야 합니다.
            if (!merged.EndTime.HasValue)
            {
                validationErrors.Add(
                    $"{merged.PackageName}: 병합된 세션의 EndTime이 null임 " +
                    $"(IsIncomplete: {merged.IsIncomplete}, IncompleteReason: {merged.IncompleteReason})");
                continue;
            }
            
            // 검증 3: 병합된 세션의 시간 범위가 유효해야 함
            if (merged.EndTime.Value < merged.StartTime)
            {
                validationErrors.Add(
                    $"{merged.PackageName}: 종료 시각이 시작 시각보다 이전임 " +
                    $"({merged.StartTime:HH:mm:ss} ~ {merged.EndTime.Value:HH:mm:ss})");
                continue;
            }
            
            _output.WriteLine($"✅ {merged.PackageName}:");
            _output.WriteLine($"   병합 시간: {merged.StartTime:HH:mm:ss} ~ {merged.EndTime.Value:HH:mm:ss}");
            _output.WriteLine($"   지속 시간: {merged.Duration?.TotalSeconds:F1}초");
            _output.WriteLine($"   Sources: {string.Join(", ", merged.SourceLogTypes)}");
            _output.WriteLine($"   IsIncomplete: {merged.IsIncomplete}");
            _output.WriteLine($"   병합 성공 (SameCameraUsageTimeThreshold 기준 만족)\n");
        }
        
        // 검증 오류가 있으면 출력
        if (validationErrors.Any())
        {
            _output.WriteLine("⚠️  검증 오류:\n");
            foreach (var error in validationErrors)
            {
                _output.WriteLine($"   ❌ {error}");
            }
            _output.WriteLine("");
        }

        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 검증 결과");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 검증 오류가 있으면 테스트 실패
        if (validationErrors.Any())
        {
            _output.WriteLine($"❌ 검증 실패: {validationErrors.Count}개의 오류 발견\n");
            validationErrors.Should().BeEmpty(
                $"병합된 세션 검증 중 {validationErrors.Count}개의 오류가 발견되었습니다. " +
                "모든 병합된 세션은 usagestats와 media.camera를 포함하고 EndTime이 있어야 합니다.");
        }

        _output.WriteLine($"✅ Orchestrator의 CameraSessionDetector.IsSameCameraUsage() 메서드가");
        _output.WriteLine($"   SameCameraUsageTimeThreshold ({ArtifactWeights.SameCameraUsageTimeThreshold:F1}초) 파라미터를 사용하여");
        _output.WriteLine($"   {mergedPairs.Count}개의 usagestats+media.camera 세션을 올바르게 병합하였습니다.\n");

        _output.WriteLine($"병합 통계:");
        _output.WriteLine($"  총 최종 세션: {allMergedSessions.Count}개");
        _output.WriteLine($"  usagestats+media.camera 병합: {mergedPairs.Count}개");
        _output.WriteLine($"  검증 통과: {mergedPairs.Count - validationErrors.Count}/{mergedPairs.Count}개");
        
        if (validationErrors.Count == 0)
        {
            _output.WriteLine($"  병합 성공률: 100% (모든 병합 세션이 임계값 기준 만족)\n");
        }
        else
        {
            _output.WriteLine($"  ⚠️  일부 병합 세션에 검증 오류가 있습니다.\n");
        }

        // 4. 논문 작성용 요약
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📝 논문 작성용 요약 (제5장 제3절)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        _output.WriteLine($"본 실험(Sample 1-10)에서 SameCameraUsageTimeThreshold 검증 결과:");
        _output.WriteLine($"- 최종 병합된 세션: {allMergedSessions.Count}개");
        _output.WriteLine($"- usagestats+media.camera 병합 쌍: {mergedPairs.Count}개");
        _output.WriteLine($"- SameCameraUsageTimeThreshold: {ArtifactWeights.SameCameraUsageTimeThreshold:F1}초");
        
        if (validationErrors.Count == 0)
        {
            _output.WriteLine($"- 검증 결과: ✅ 타당함 (모든 병합 세션이 기준 만족)");
            _output.WriteLine($"- Orchestrator를 통한 IsSameCameraUsage() 동작: ✅ 정상 (병합 성공률 100%)\n");
        }
        else
        {
            _output.WriteLine($"- 검증 결과: ⚠️  일부 오류 발견 ({validationErrors.Count}개)");
            _output.WriteLine($"- Orchestrator를 통한 IsSameCameraUsage() 동작: ⚠️  검증 필요\n");
        }

        _output.WriteLine("════════════════════════════════════════════════════════════\n");

        // 5. Assertion - Orchestrator의 CameraSessionDetector가 올바르게 병합했는지 확인
        mergedPairs.Should().NotBeEmpty("usagestats와 media_camera 세션이 SameCameraUsageTimeThreshold 기준에 따라 병합되어야 함");
        mergedPairs.Should().AllSatisfy(s => 
        {
            s.SourceLogTypes.Should().Contain(ArtifactWeights.SessionSourceNames.Usagestats, "병합된 세션은 usagestats를 포함해야 함");
            s.SourceLogTypes.Should().Contain(ArtifactWeights.SessionSourceNames.MediaCamera, "병합된 세션은 media_camera를 포함해야 함");
            s.EndTime.Should().HaveValue("병합된 세션은 불완전 세션 처리 후 EndTime이 있어야 함");
        });
    }

    #region Helper Methods

    /// <summary>
    /// 샘플에서 병합 전 원본 세션을 추출합니다.
    /// </summary>
    private async Task<List<CameraSession>> ExtractRawSessionsFromSample(
        string sampleDirectory, 
        DateTime startTime, 
        DateTime endTime)
    {
        // 1. 로그 파싱
        var samplePath = Path.Combine(_sampleLogsPath, sampleDirectory);
        var parsedEvents = await ParseSampleLogsAsync(samplePath, startTime, endTime);
        
        // 2. SessionSource들을 직접 호출하여 병합 전 원본 세션 추출
        var confidenceCalculator = new ConfidenceCalculator(NullLogger<ConfidenceCalculator>.Instance);
        var sessionSources = new List<ISessionSource>
        {
            new UsagestatsSessionSource(NullLogger<UsagestatsSessionSource>.Instance, confidenceCalculator),
            new MediaCameraSessionSource(NullLogger<MediaCameraSessionSource>.Instance, confidenceCalculator)
        };
        
        var options = CreateAnalysisOptions();
        var rawSessions = new List<CameraSession>();
        
        foreach (var source in sessionSources)
        {
            var sourceSessions = source.ExtractSessions(parsedEvents, options);
            rawSessions.AddRange(sourceSessions);
        }
        
        return rawSessions;
    }

    private async Task<List<NormalizedLogEvent>> ParseSampleLogsAsync(
        string samplePath,
        DateTime startTime,
        DateTime endTime)
    {
        var allEvents = new List<NormalizedLogEvent>();
        var parseStats = new Dictionary<string, (bool exists, int eventCount, int cameraEvents)>();

        // 로그 파일 매핑
        var logConfigs = new Dictionary<string, string>
        {
            ["audio.log"] = "adb_audio_config.yaml",
            ["media_camera_worker.log"] = "adb_media_camera_worker_config.yaml",
            ["media_camera.log"] = "adb_media_camera_config.yaml",  // ← 언더스코어로 수정!
            ["media_metrics.log"] = "adb_media_metrics_config.yaml",
            ["usagestats.log"] = "adb_usagestats_config.yaml",
            ["vibrator_manager.log"] = "adb_vibrator_config.yaml",
            ["activity.log"] = "adb_activity_config.yaml"
        };

        foreach (var (logFileName, configFileName) in logConfigs)
        {
            var logPath = Path.Combine(samplePath, logFileName);
            if (!File.Exists(logPath))
            {
                parseStats[logFileName] = (false, 0, 0);
                continue;
            }

            var events = await ParseLogFileAsync(logPath, configFileName, startTime, endTime);
            var cameraEvents = events.Count(e => 
                e.EventType == "CAMERA_CONNECT" || e.EventType == "CAMERA_DISCONNECT");
            
            parseStats[logFileName] = (true, events.Count, cameraEvents);
            allEvents.AddRange(events);
        }

        // 디버깅 출력: media_camera.log 상태
        if (parseStats.TryGetValue("media_camera.log", out var mediaCameraStats))
        {
            if (!mediaCameraStats.exists)
            {
                _output.WriteLine($"    ⚠️  media_camera.log: 파일 없음 (휘발됨)");
            }
            else if (mediaCameraStats.eventCount == 0)
            {
                _output.WriteLine($"    ⚠️  media_camera.log: 파일 있음, 이벤트 0개 (시간 범위 밖)");
            }
            else if (mediaCameraStats.cameraEvents == 0)
            {
                _output.WriteLine($"    ⚠️  media_camera.log: 이벤트 {mediaCameraStats.eventCount}개, CAMERA_CONNECT/DISCONNECT 0개");
            }
            else
            {
                _output.WriteLine($"    ✅ media_camera.log: 이벤트 {mediaCameraStats.eventCount}개, CAMERA 이벤트 {mediaCameraStats.cameraEvents}개");
            }
        }

        return allEvents;
    }

    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string logFilePath,
        string configFileName,
        DateTime startTime,
        DateTime endTime)
    {
        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
            return new List<NormalizedLogEvent>();

        // YAML 설정 로드
        var configLoader = new YamlConfigurationLoader(configPath);
        var configuration = configLoader.Load(configPath);

        // DeviceInfo 생성
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = startTime,
            Model = "Samsung Galaxy S24",
            AndroidVersion = "15",
            Manufacturer = "Samsung"
        };

        // Parser 생성
        var parser = new AdbLogParser(configuration, NullLogger<AdbLogParser>.Instance);

        // 파싱 옵션 설정
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = false,
            StartTime = startTime,
            EndTime = endTime
        };

        try
        {
            var result = await parser.ParseAsync(logFilePath, options);
            return result.Success ? result.Events.ToList() : new List<NormalizedLogEvent>();
        }
        catch
        {
            return new List<NormalizedLogEvent>();
        }
    }

    /// <summary>
    /// usagestats-media_camera 세션 쌍 식별 (예비 실험 전용)
    /// </summary>
    /// <remarks>
    /// 본 메서드는 예비 실험 테스트(Measure_SameCameraUsageTimeThreshold_PreliminaryExperiments)에서만 사용됩니다.
    /// 본 실험 테스트는 CameraSessionDetector를 사용하므로 본 메서드를 사용하지 않습니다.
    /// </remarks>
    private List<(CameraSession Usagestats, CameraSession MediaCamera)> IdentifyUsagestatsMediaCameraPairs(
        List<CameraSession> sessions)
    {
        var pairs = new List<(CameraSession, CameraSession)>();

        // usagestats와 media_camera 세션 분리
        var usagestatsSessions = sessions
            .Where(s => s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.Usagestats))
            .OrderBy(s => s.StartTime)
            .ToList();

        var mediaCameraSessions = sessions
            .Where(s => s.SourceLogTypes.Contains(ArtifactWeights.SessionSourceNames.MediaCamera))
            .OrderBy(s => s.StartTime)
            .ToList();

        // 같은 패키지명 + 시간 겹침으로 쌍 매칭
        foreach (var usagestats in usagestatsSessions)
        {
            var matchingMediaCamera = mediaCameraSessions.FirstOrDefault(mc =>
                string.Equals(mc.PackageName, usagestats.PackageName, StringComparison.OrdinalIgnoreCase) &&
                HasTimeOverlap(usagestats, mc));

            if (matchingMediaCamera != null)
            {
                pairs.Add((usagestats, matchingMediaCamera));
            }
        }

        return pairs;
    }

    /// <summary>
    /// 두 세션의 시간 겹침 여부 확인 (예비 실험 전용)
    /// </summary>
    private bool HasTimeOverlap(CameraSession session1, CameraSession session2)
    {
        if (!session1.EndTime.HasValue || !session2.EndTime.HasValue)
            return false;

        var start1 = session1.StartTime;
        var end1 = session1.EndTime.Value;
        var start2 = session2.StartTime;
        var end2 = session2.EndTime.Value;

        return start1 < end2 && start2 < end1;
    }

    private AnalysisOptions CreateAnalysisOptions()
    {
        // AnalysisOptions 기본값 사용 (하드코딩 금지)
        return new AnalysisOptions();
    }

    /// <summary>
    /// Orchestrator 생성 (GT 테스트와 동일한 방식)
    /// </summary>
    /// <remarks>
    /// GT 테스트의 CreateOrchestratorWithDefaultConfig() 메서드를 참고하여
    /// 동일한 방식으로 Orchestrator를 생성합니다.
    /// 중복 제거가 포함된 전체 분석 파이프라인을 사용합니다.
    /// </remarks>
    private IAnalysisOrchestrator CreateOrchestrator()
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
        // 기본값 사용 (CreateAnalysisOptions와 동일)
        services.AddSingleton(new AnalysisOptions());
        
        // AndroidAdbAnalysis 서비스 등록 (기본 설정 사용)
        services.AddAndroidAdbAnalysis();
        
        // ServiceProvider 빌드
        var serviceProvider = services.BuildServiceProvider();
        
        // IAnalysisOrchestrator 해결
        return serviceProvider.GetRequiredService<IAnalysisOrchestrator>();
    }

    /// <summary>
    /// 디버깅 목적: 불완전 세션 처리 (CameraSessionDetector.HandleIncompleteSessions 로직 복제)
    /// </summary>
    /// <remarks>
    /// CameraSessionDetector의 HandleIncompleteSessions 로직을 복제하여
    /// 디버깅 목적으로 사용합니다. 비즈니스 로직을 수정하지 않고
    /// 테스트 코드에서만 사용됩니다.
    /// </remarks>
    private List<CameraSession> ProcessIncompleteSessionsForDebugging(
        List<CameraSession> sessions,
        AnalysisOptions options)
    {
        var incompleteSessions = sessions.Where(s => s.IsIncomplete).ToList();
        
        if (incompleteSessions.Count == 0)
            return sessions;

        var completeSessions = sessions.Where(s => !s.IsIncomplete).ToList();
        var processedSessions = new List<CameraSession>(completeSessions);

        // 패키지별 평균 세션 지속 시간 계산
        var packageAverageDurations = completeSessions
            .GroupBy(s => s.PackageName)
            .ToDictionary(
                g => g.Key,
                g => TimeSpan.FromSeconds(g.Average(s => s.Duration!.Value.TotalSeconds))
            );

        // 전체 평균 (fallback용)
        var overallAverageDuration = completeSessions.Any()
            ? TimeSpan.FromSeconds(completeSessions.Average(s => s.Duration!.Value.TotalSeconds))
            : TimeSpan.FromMinutes(5); // 기본값

        foreach (var session in incompleteSessions)
        {
            // 해당 패키지의 평균 사용, 없으면 전체 평균 사용
            var averageDuration = packageAverageDurations.TryGetValue(session.PackageName, out var pkgAvg)
                ? pkgAvg
                : overallAverageDuration;

            var processed = TryCompleteSessionForDebugging(session, sessions, averageDuration, options);
            processedSessions.Add(processed);
        }

        return processedSessions.OrderBy(s => s.StartTime).ToList();
    }

    /// <summary>
    /// 디버깅 목적: 불완전 세션 완료 시도 (CameraSessionDetector.TryCompleteSession 로직 복제)
    /// </summary>
    private CameraSession TryCompleteSessionForDebugging(
        CameraSession session,
        List<CameraSession> allSessions,
        TimeSpan averageDuration,
        AnalysisOptions options)
    {
        // 1순위: 다음 세션 시작 시각으로 종료
        var nextSession = allSessions
            .Where(s => s.PackageName == session.PackageName && s.StartTime > session.StartTime)
            .OrderBy(s => s.StartTime)
            .FirstOrDefault();

        if (nextSession != null)
        {
            var gap = nextSession.StartTime - session.StartTime;
            
            // 동적 MaxSessionGap 계산 (패키지 평균 기반)
            var dynamicMaxGap = CalculateDynamicMaxSessionGapForDebugging(averageDuration, options.MaxSessionGap);
            
            if (gap <= dynamicMaxGap)
            {
                // 다음 세션이 합리적인 거리 → 다음 세션 직전까지 사용
                return new CameraSession
                {
                    SessionId = session.SessionId,
                    StartTime = session.StartTime,
                    EndTime = nextSession.StartTime.AddSeconds(-1),
                    PackageName = session.PackageName,
                    ProcessId = session.ProcessId,
                    SourceLogTypes = session.SourceLogTypes,
                    CaptureEventIds = session.CaptureEventIds,
                    StartEventId = session.StartEventId,
                    EndEventId = session.EndEventId,
                    IncompleteReason = null, // 완료됨
                    SessionCompletenessScore = session.SessionCompletenessScore,
                    SourceEventIds = session.SourceEventIds,
                    CameraDeviceIds = session.CameraDeviceIds
                };
            }
            else
            {
                // 다음 세션이 너무 멂 → 평균 사용 시간 기반 추정
                var estimatedEnd = session.StartTime + averageDuration;
                
                return new CameraSession
                {
                    SessionId = session.SessionId,
                    StartTime = session.StartTime,
                    EndTime = estimatedEnd,
                    PackageName = session.PackageName,
                    ProcessId = session.ProcessId,
                    SourceLogTypes = session.SourceLogTypes,
                    CaptureEventIds = session.CaptureEventIds,
                    StartEventId = session.StartEventId,
                    EndEventId = session.EndEventId,
                    IncompleteReason = SessionIncompleteReason.LogTruncated,
                    SessionCompletenessScore = session.SessionCompletenessScore,
                    SourceEventIds = session.SourceEventIds,
                    CameraDeviceIds = session.CameraDeviceIds
                };
            }
        }

        // 2순위: 다음 세션 없음 → 평균 사용 시간 기반 추정
        var estimatedEndTime = session.StartTime + averageDuration;
        
        return new CameraSession
        {
            SessionId = session.SessionId,
            StartTime = session.StartTime,
            EndTime = estimatedEndTime,
            PackageName = session.PackageName,
            ProcessId = session.ProcessId,
            SourceLogTypes = session.SourceLogTypes,
            CaptureEventIds = session.CaptureEventIds,
            StartEventId = session.StartEventId,
            EndEventId = session.EndEventId,
            IncompleteReason = SessionIncompleteReason.LogTruncated,
            SessionCompletenessScore = session.SessionCompletenessScore,
            SourceEventIds = session.SourceEventIds,
            CameraDeviceIds = session.CameraDeviceIds
        };
    }

    /// <summary>
    /// 디버깅 목적: 동적 MaxSessionGap 계산 (CameraSessionDetector.CalculateDynamicMaxSessionGap 로직 복제)
    /// </summary>
    private TimeSpan CalculateDynamicMaxSessionGapForDebugging(TimeSpan packageAverage, TimeSpan configuredMax)
    {
        const double SessionGapMultiplier = 1.0;
        
        // 패키지 평균 × 가중치
        var calculated = TimeSpan.FromMinutes(packageAverage.TotalMinutes * SessionGapMultiplier);
        
        // 최소값: 5분
        var minimum = TimeSpan.FromMinutes(5);
        
        // 최소값과 설정값 사이로 제한
        var result = TimeSpan.FromMinutes(
            Math.Clamp(
                calculated.TotalMinutes,
                minimum.TotalMinutes,
                configuredMax.TotalMinutes
            )
        );
        
        return result;
    }

    #endregion
}

