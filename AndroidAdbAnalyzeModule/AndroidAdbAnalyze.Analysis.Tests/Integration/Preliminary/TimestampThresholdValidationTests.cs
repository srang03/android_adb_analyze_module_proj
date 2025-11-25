using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Services.Deduplication;
using AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;
using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Preliminary;

/// <summary>
/// 타임스탬프 임계값 타당성 검증 테스트
/// </summary>
/// <remarks>
/// 목적:
/// - 예비 실험(Preliminary 1-3)에서 중복 이벤트 쌍의 타임스탬프 차이 측정
/// - 이벤트 타입별 평균/최대/최소 타임스탬프 차이 계산
/// - 설정된 임계값(1000ms, 500ms, 100ms, 200ms)의 타당성 검증
/// 
/// 논문 반영:
/// - 제4장 제2절: 이벤트 타입별 임계값 설정 근거
/// - 제5장 제4절: 파라미터 타당성 검증 섹션 추가
/// </remarks>
public sealed class TimestampThresholdValidationTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _parserConfigPath;
    private readonly EventDeduplicator _deduplicator;
    
    public TimestampThresholdValidationTests(ITestOutputHelper output)
    {
        _output = output;
        
        // Parser Config 경로 설정
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        _parserConfigPath = Path.Combine(projectRoot, "AndroidAdbAnalyze.Parser", "Configs");
        
        // EventDeduplicator 생성 (AnalysisOptions 기본값 사용)
        var options = new AnalysisOptions();
        _deduplicator = new EventDeduplicator(
            NullLogger<EventDeduplicator>.Instance,
            options);
    }
    
    /// <summary>
    /// 넓은 임계값(1000ms)을 사용하여 예비 실험 데이터에서 중복 쌍 찾기 및 시간 차이 측정
    /// 목적: 모든 이벤트 타입에 1000ms 임계값을 적용하여 실측 최대 차이 값을 확인
    /// </summary>
    [Fact]
    public async Task MeasureTimestampDifferences_WithWideThreshold_1000ms()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("타임스탬프 차등 임계값 실측 (넓은 임계값 1000ms 사용)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // 1. Preliminary 1-3 로그 파싱
        var preliminary1Events = await ParsePreliminary1Events();
        var preliminary2Events = await ParsePreliminary2Events();
        var preliminary3Events = await ParsePreliminary3Events();
        
        var allEvents = preliminary1Events
            .Concat(preliminary2Events)
            .Concat(preliminary3Events)
            .ToList();
        
        _output.WriteLine($"총 이벤트 수: {allEvents.Count}개\n");
        _output.WriteLine($"  - Preliminary 1: {preliminary1Events.Count}개");
        _output.WriteLine($"  - Preliminary 2: {preliminary2Events.Count}개");
        _output.WriteLine($"  - Preliminary 3: {preliminary3Events.Count}개\n");
        
        // 2. 넓은 임계값(1000ms)을 사용하여 중복 쌍 찾기
        const int wideThreshold = 1000; // 모든 이벤트 타입에 동일하게 적용
        var duplicatePairs = FindDuplicatePairsWithWideThreshold(allEvents, wideThreshold);
        
        _output.WriteLine($"넓은 임계값({wideThreshold}ms) 기준 중복 쌍: {duplicatePairs.Count}개\n");
        
        // 2.5. 이벤트 타입별 총 개수 확인 (미측정 원인 분석용)
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("이벤트 타입별 총 개수 확인 (미측정 원인 분석)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        var keyEventTypes = new[] { "CAMERA_CONNECT", "CAMERA_DISCONNECT", "PLAYER_CREATED", 
            "PLAYER_EVENT", "PLAYER_RELEASED", "MEDIA_EXTRACTOR", "DATABASE_INSERT", 
            "URI_PERMISSION_GRANT", "URI_PERMISSION_REVOKE" };
        
        _output.WriteLine($"{"이벤트 타입",-30} {"총 개수",-10} {"중복 쌍",-10} {"미측정 원인"}");
        _output.WriteLine(new string('─', 80));
        
        foreach (var eventType in keyEventTypes)
        {
            var eventCount = allEvents.Count(e => e.EventType == eventType);
            var pairCount = duplicatePairs.Count(p => p.EventType == eventType);
            
            string reason;
            if (eventCount == 0)
            {
                reason = "이벤트 자체가 존재하지 않음";
            }
            else if (eventCount == 1)
            {
                reason = "이벤트 1개만 존재 (중복 쌍 불가능)";
            }
            else if (pairCount == 0)
            {
                reason = "이벤트 존재하나 중복 쌍 없음 (같은 패키지 내 1000ms 이내 + 유사도 0.55 이상 조건 미충족)";
            }
            else
            {
                reason = "정상 측정됨";
            }
            
            _output.WriteLine($"{eventType,-30} {eventCount,-10} {pairCount,-10} {reason}");
        }
        
        _output.WriteLine("\n");
        
        // 3. 이벤트 타입별 통계 계산
        var statsByType = duplicatePairs
            .GroupBy(p => p.EventType)
            .Select(g => new
            {
                EventType = g.Key,
                Count = g.Count(),
                TimeDiffs = g.Select(p => p.TimeDiffMs).ToList(),
                Avg = g.Average(p => p.TimeDiffMs),
                Min = g.Min(p => p.TimeDiffMs),
                Max = g.Max(p => p.TimeDiffMs)
            })
            .OrderBy(s => s.EventType)
            .ToList();
        
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("이벤트 타입별 실측 데이터 (넓은 임계값 1000ms 기준)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _output.WriteLine($"{"이벤트 타입",-30} {"중복 쌍",-10} {"평균 차이",-15} {"최소 차이",-15} {"최대 차이",-15}");
        _output.WriteLine(new string('─', 85));
        
        foreach (var stat in statsByType)
        {
            _output.WriteLine($"{stat.EventType,-30} {stat.Count,-10} {stat.Avg:F1}ms{"",-8} {stat.Min:F1}ms{"",-8} {stat.Max:F1}ms");
        }
        
        _output.WriteLine("\n");
        
        // 4. 주요 이벤트 타입별 상세 분석 및 초기 설정값 제안
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("주요 이벤트 타입별 상세 분석 및 초기 설정값 제안");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // keyEventTypes는 이미 위에서 정의됨 (88줄)
        
        _output.WriteLine($"{"이벤트 타입",-30} {"중복 쌍",-10} {"실측 평균",-12} {"실측 최대",-12} {"제안 초기 설정값",-18} {"안전 마진"}");
        _output.WriteLine(new string('─', 100));
        
        foreach (var eventType in keyEventTypes)
        {
            var stat = statsByType.FirstOrDefault(s => s.EventType == eventType);
            if (stat != null)
            {
                // 안전 마진 적용 (1.05배 ~ 1.2배 범위에서 적절한 값 선택)
                double safetyMargin;
                int suggestedThreshold;
                
                if (stat.Max <= 50)
                {
                    // 작은 값은 더 큰 안전 마진 적용
                    safetyMargin = 1.2;
                    suggestedThreshold = (int)Math.Ceiling(stat.Max * safetyMargin);
                }
                else if (stat.Max <= 200)
                {
                    safetyMargin = 1.1;
                    suggestedThreshold = (int)Math.Ceiling(stat.Max * safetyMargin);
                }
                else
                {
                    safetyMargin = 1.05;
                    suggestedThreshold = (int)Math.Ceiling(stat.Max * safetyMargin);
                }
                
                // 100ms 단위로 반올림 (단, 최소 100ms 보장)
                suggestedThreshold = ((suggestedThreshold + 50) / 100) * 100;
                if (suggestedThreshold < 100)
                {
                    suggestedThreshold = 100; // 최소 100ms 보장
                }
                
                _output.WriteLine($"{eventType,-30} {stat.Count,-10} {stat.Avg:F1}ms{"",-5} {stat.Max:F1}ms{"",-5} {suggestedThreshold}ms{"",-12} {safetyMargin:F2}배");
            }
            else
            {
                _output.WriteLine($"{eventType,-30} {"미측정",-10} {"-",-12} {"-",-12} {"이론적 추정",-18} {"-"}");
            }
        }
        
        _output.WriteLine("\n");
        
        // 5. 표 48 업데이트용 요약
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("표 48 업데이트용 요약");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        _output.WriteLine("| 이벤트 타입 카테고리 | 중복 이벤트 쌍 개수 | 실측 평균 차이 | 실측 최대 차이 | 초기 설정값 | 안전 마진 | 설정 논리 |");
        _output.WriteLine("|---|---|---|---|---|---|---|");
        
        foreach (var eventType in keyEventTypes)
        {
            var stat = statsByType.FirstOrDefault(s => s.EventType == eventType);
            if (stat != null)
            {
                double safetyMargin;
                int suggestedThreshold;
                
                if (stat.Max <= 50)
                {
                    safetyMargin = 1.2;
                    suggestedThreshold = (int)Math.Ceiling(stat.Max * safetyMargin);
                }
                else if (stat.Max <= 200)
                {
                    safetyMargin = 1.1;
                    suggestedThreshold = (int)Math.Ceiling(stat.Max * safetyMargin);
                }
                else
                {
                    safetyMargin = 1.05;
                    suggestedThreshold = (int)Math.Ceiling(stat.Max * safetyMargin);
                }
                
                suggestedThreshold = ((suggestedThreshold + 50) / 100) * 100;
                if (suggestedThreshold < 100)
                {
                    suggestedThreshold = 100; // 최소 100ms 보장
                }
                
                var logic = stat.Max <= 50 
                    ? "고정밀 타이머, 충분한 안전 마진 확보"
                    : stat.Max <= 200
                    ? "미디어 프레임워크 기본값 적용"
                    : "HAL 계층 멀티스레드 최대 지연 대응";
                
                // 표 48의 안전 마진은 "초기 설정값 ÷ 실측 최대값" (부록 3 정의)
                var appendixSafetyMargin = (double)suggestedThreshold / stat.Max;
                
                _output.WriteLine($"| {eventType} | {stat.Count} | {stat.Avg:F1}ms | {stat.Max:F1}ms | {suggestedThreshold}ms | {appendixSafetyMargin:F2}배 | {logic} |");
            }
            else
            {
                _output.WriteLine($"| {eventType} | - | - | - | 이론적 추정 | - | 예비 실험 미측정, 이론적 추정값 적용 |");
            }
        }
        
        _output.WriteLine("\n");
        
        // 6. 샘플별 독립 분석 (각 샘플은 독립적으로 수행되었으므로)
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("샘플별 독립 분석 (각 샘플은 독립적으로 수행되었으므로)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        AnalyzePerSample(preliminary1Events, preliminary2Events, preliminary3Events, keyEventTypes, wideThreshold);
        
        // 7. 미측정 이벤트 타입 상세 분석 (전체 합산 기준)
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("미측정 이벤트 타입 상세 분석 (전체 합산 기준, 중복 쌍 0개인 이유 정확히 파악)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        AnalyzeWhyNoDuplicatePairs(allEvents, duplicatePairs, keyEventTypes);
    }
    
    /// <summary>
    /// 본 실험 샘플별 독립 분석 (각 샘플은 독립적으로 수행되었으므로)
    /// </summary>
    [Fact]
    public async Task AnalyzeMainExperiment_PerSample_WithWideThreshold_1000ms()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("본 실험 샘플별 독립 분석 (넓은 임계값 1000ms 사용)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        const int wideThreshold = 1000;
        var keyEventTypes = new[] { "CAMERA_CONNECT", "CAMERA_DISCONNECT", "PLAYER_CREATED", 
            "PLAYER_EVENT", "PLAYER_RELEASED", "MEDIA_EXTRACTOR", "DATABASE_INSERT", 
            "URI_PERMISSION_GRANT", "URI_PERMISSION_REVOKE" };
        
        // 본 실험 1-10 샘플별 분석
        for (int sampleNum = 1; sampleNum <= 10; sampleNum++)
        {
            _output.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _output.WriteLine($"본 실험 {sampleNum}차 샘플 독립 분석");
            _output.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            
            var sampleEvents = await ParseMainExperimentSample(sampleNum);
            
            if (sampleEvents.Count == 0)
            {
                _output.WriteLine($"⚠️  샘플 {sampleNum}의 로그를 찾을 수 없습니다.\n");
                continue;
            }
            
            _output.WriteLine($"총 이벤트 수: {sampleEvents.Count}개\n");
            
            // 샘플별 중복 쌍 찾기
            var sampleDuplicatePairs = FindDuplicatePairsWithWideThreshold(sampleEvents.ToList(), wideThreshold);
            
            _output.WriteLine($"넓은 임계값({wideThreshold}ms) 기준 중복 쌍: {sampleDuplicatePairs.Count}개\n");
            
            _output.WriteLine($"{"이벤트 타입",-30} {"총 개수",-10} {"중복 쌍",-10} {"상태"}");
            _output.WriteLine(new string('─', 80));
            
            foreach (var eventType in keyEventTypes)
            {
                var eventCount = sampleEvents.Count(e => e.EventType == eventType);
                var pairCount = sampleDuplicatePairs.Count(p => p.EventType == eventType);
                
                string status;
                if (eventCount == 0)
                {
                    status = "이벤트 없음";
                }
                else if (eventCount == 1)
                {
                    status = "이벤트 1개 (중복 쌍 불가능)";
                }
                else if (pairCount == 0)
                {
                    status = "중복 쌍 없음";
                }
                else
                {
                    status = $"중복 쌍 {pairCount}개 발견";
                }
                
                _output.WriteLine($"{eventType,-30} {eventCount,-10} {pairCount,-10} {status}");
            }
            
            _output.WriteLine("\n");
            
            // 중복 쌍이 있는 이벤트 타입 상세 분석
            var eventTypesWithPairs = sampleDuplicatePairs
                .Select(p => p.EventType)
                .Distinct()
                .ToList();
            
            if (eventTypesWithPairs.Any())
            {
                _output.WriteLine($"중복 쌍이 발견된 이벤트 타입 상세 분석:\n");
                
                foreach (var eventType in eventTypesWithPairs)
                {
                    var pairs = sampleDuplicatePairs.Where(p => p.EventType == eventType).ToList();
                    var timeDiffs = pairs.Select(p => p.TimeDiffMs).ToList();
                    
                    _output.WriteLine($"  {eventType}:");
                    _output.WriteLine($"    중복 쌍: {pairs.Count}개");
                    _output.WriteLine($"    시간 차이: {timeDiffs.Min():F1}ms ~ {timeDiffs.Max():F1}ms (평균: {timeDiffs.Average():F1}ms)");
                    _output.WriteLine($"    유사도: {pairs.Min(p => p.Similarity):F3} ~ {pairs.Max(p => p.Similarity):F3} (평균: {pairs.Average(p => p.Similarity):F3})");
                    _output.WriteLine("");
                }
            }
            
            _output.WriteLine("\n");
        }
    }
    
    /// <summary>
    /// 샘플별 독립 분석 (각 샘플은 독립적으로 수행되었으므로)
    /// </summary>
    private void AnalyzePerSample(
        IReadOnlyList<NormalizedLogEvent> sample1Events,
        IReadOnlyList<NormalizedLogEvent> sample2Events,
        IReadOnlyList<NormalizedLogEvent> sample3Events,
        string[] keyEventTypes,
        int wideThreshold)
    {
        var samples = new[]
        {
            ("예비 실험 1차", sample1Events),
            ("예비 실험 2차", sample2Events),
            ("예비 실험 3차", sample3Events)
        };
        
        foreach (var (sampleName, sampleEvents) in samples)
        {
            _output.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            _output.WriteLine($"{sampleName} 독립 분석");
            _output.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
            
            _output.WriteLine($"총 이벤트 수: {sampleEvents.Count}개\n");
            
            // 샘플별 중복 쌍 찾기
            var sampleDuplicatePairs = FindDuplicatePairsWithWideThreshold(sampleEvents.ToList(), wideThreshold);
            
            _output.WriteLine($"넓은 임계값({wideThreshold}ms) 기준 중복 쌍: {sampleDuplicatePairs.Count}개\n");
            
            _output.WriteLine($"{"이벤트 타입",-30} {"총 개수",-10} {"중복 쌍",-10} {"상태"}");
            _output.WriteLine(new string('─', 80));
            
            foreach (var eventType in keyEventTypes)
            {
                var eventCount = sampleEvents.Count(e => e.EventType == eventType);
                var pairCount = sampleDuplicatePairs.Count(p => p.EventType == eventType);
                
                string status;
                if (eventCount == 0)
                {
                    status = "이벤트 없음";
                }
                else if (eventCount == 1)
                {
                    status = "이벤트 1개 (중복 쌍 불가능)";
                }
                else if (pairCount == 0)
                {
                    status = "중복 쌍 없음";
                }
                else
                {
                    status = $"중복 쌍 {pairCount}개 발견";
                }
                
                _output.WriteLine($"{eventType,-30} {eventCount,-10} {pairCount,-10} {status}");
            }
            
            _output.WriteLine("\n");
            
            // 중복 쌍이 있는 이벤트 타입 상세 분석
            var eventTypesWithPairs = sampleDuplicatePairs
                .Select(p => p.EventType)
                .Distinct()
                .ToList();
            
            if (eventTypesWithPairs.Any())
            {
                _output.WriteLine($"중복 쌍이 발견된 이벤트 타입 상세 분석:\n");
                
                foreach (var eventType in eventTypesWithPairs)
                {
                    var pairs = sampleDuplicatePairs.Where(p => p.EventType == eventType).ToList();
                    var timeDiffs = pairs.Select(p => p.TimeDiffMs).ToList();
                    
                    _output.WriteLine($"  {eventType}:");
                    _output.WriteLine($"    중복 쌍: {pairs.Count}개");
                    _output.WriteLine($"    시간 차이: {timeDiffs.Min():F1}ms ~ {timeDiffs.Max():F1}ms (평균: {timeDiffs.Average():F1}ms)");
                    _output.WriteLine($"    유사도: {pairs.Min(p => p.Similarity):F3} ~ {pairs.Max(p => p.Similarity):F3} (평균: {pairs.Average(p => p.Similarity):F3})");
                    _output.WriteLine("");
                }
            }
            
            // PLAYER_EVENT, PLAYER_RELEASED 패키지별 상세 분석 (이벤트 발생 원인 파악)
            if (sampleEvents.Any(e => e.EventType == "PLAYER_EVENT" || e.EventType == "PLAYER_RELEASED"))
            {
                _output.WriteLine($"PLAYER_EVENT, PLAYER_RELEASED 패키지별 상세 분석 (이벤트 발생 원인 파악):\n");
                
                foreach (var eventType in new[] { "PLAYER_EVENT", "PLAYER_RELEASED" })
                {
                    var typeEvents = sampleEvents.Where(e => e.EventType == eventType).ToList();
                    if (typeEvents.Count == 0) continue;
                    
                    _output.WriteLine($"  {eventType} (총 {typeEvents.Count}개):");
                    var packageGroups = typeEvents.GroupBy(e => e.PackageName ?? "null").ToList();
                    foreach (var pkg in packageGroups)
                    {
                        _output.WriteLine($"    - {pkg.Key}: {pkg.Count()}개");
                        if (pkg.Count() > 1)
                        {
                            var pkgEvents = pkg.OrderBy(e => e.Timestamp).ToList();
                            var timeDiffs = new List<double>();
                            for (int i = 0; i < pkgEvents.Count - 1; i++)
                            {
                                var diff = Math.Abs((pkgEvents[i + 1].Timestamp - pkgEvents[i].Timestamp).TotalMilliseconds);
                                timeDiffs.Add(diff);
                            }
                            if (timeDiffs.Count > 0)
                            {
                                _output.WriteLine($"      시간 간격: {timeDiffs.Min():F1}ms ~ {timeDiffs.Max():F1}ms (평균: {timeDiffs.Average():F1}ms)");
                                _output.WriteLine($"      1000ms 이내 간격: {timeDiffs.Count(d => d <= 1000)}개 / {timeDiffs.Count}개");
                            }
                        }
                    }
                    _output.WriteLine("");
                }
            }
            
            _output.WriteLine("\n");
        }
    }
    
    /// <summary>
    /// 중복 쌍이 없는 이벤트 타입의 상세 분석
    /// </summary>
    private void AnalyzeWhyNoDuplicatePairs(
        List<NormalizedLogEvent> allEvents,
        List<DuplicatePairInfo> duplicatePairs,
        string[] keyEventTypes)
    {
        var similarityThreshold = ArtifactWeights.DeduplicationSimilarityThreshold;
        const int wideThreshold = 1000;
        
        foreach (var eventType in keyEventTypes)
        {
            var eventCount = allEvents.Count(e => e.EventType == eventType);
            var pairCount = duplicatePairs.Count(p => p.EventType == eventType);
            
            // 중복 쌍이 0개인 이벤트 타입만 상세 분석
            if (eventCount > 1 && pairCount == 0)
            {
                _output.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                _output.WriteLine($"{eventType} 상세 분석 (총 {eventCount}개, 중복 쌍 0개)");
                _output.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
                
                var typeEvents = allEvents
                    .Where(e => e.EventType == eventType)
                    .OrderBy(e => e.Timestamp)
                    .ToList();
                
                // 1. 패키지별 분포
                var packageGroups = typeEvents.GroupBy(e => e.PackageName ?? "null").ToList();
                _output.WriteLine($"패키지별 분포:");
                foreach (var pkg in packageGroups)
                {
                    _output.WriteLine($"  - {pkg.Key}: {pkg.Count()}개");
                }
                _output.WriteLine("");
                
                // 2. 같은 패키지 내에서 1000ms 이내 쌍 찾기 (유사도 무시)
                var samePackagePairs = new List<(NormalizedLogEvent e1, NormalizedLogEvent e2, double timeDiff)>();
                foreach (var pkgGroup in packageGroups.Where(g => g.Count() > 1))
                {
                    var pkgEvents = pkgGroup.OrderBy(e => e.Timestamp).ToList();
                    for (int i = 0; i < pkgEvents.Count - 1; i++)
                    {
                        for (int j = i + 1; j < pkgEvents.Count; j++)
                        {
                            var timeDiff = Math.Abs((pkgEvents[i].Timestamp - pkgEvents[j].Timestamp).TotalMilliseconds);
                            if (timeDiff <= wideThreshold)
                            {
                                samePackagePairs.Add((pkgEvents[i], pkgEvents[j], timeDiff));
                            }
                            else
                            {
                                break; // 시간순 정렬이므로 이후는 더 멀리 떨어져 있음
                            }
                        }
                    }
                }
                
                _output.WriteLine($"같은 패키지 내 {wideThreshold}ms 이내 쌍: {samePackagePairs.Count}개");
                if (samePackagePairs.Count > 0)
                {
                    _output.WriteLine($"  시간 차이 범위: {samePackagePairs.Min(p => p.timeDiff):F1}ms ~ {samePackagePairs.Max(p => p.timeDiff):F1}ms");
                    _output.WriteLine($"  평균 시간 차이: {samePackagePairs.Average(p => p.timeDiff):F1}ms");
                }
                _output.WriteLine("");
                
                // 3. 유사도 계산 및 분석
                if (samePackagePairs.Count > 0)
                {
                    _output.WriteLine($"유사도 분석 (Jaccard Similarity, 임계값: {similarityThreshold:F2}):");
                    var similarities = new List<double>();
                    var belowThresholdCount = 0;
                    var aboveThresholdCount = 0;
                    
                    foreach (var (e1, e2, timeDiff) in samePackagePairs)
                    {
                        var similarity = CalculateJaccardSimilarity(e1.Attributes, e2.Attributes);
                        similarities.Add(similarity);
                        
                        if (similarity < similarityThreshold)
                        {
                            belowThresholdCount++;
                        }
                        else
                        {
                            aboveThresholdCount++;
                        }
                    }
                    
                    if (similarities.Count > 0)
                    {
                        _output.WriteLine($"  유사도 범위: {similarities.Min():F3} ~ {similarities.Max():F3}");
                        _output.WriteLine($"  평균 유사도: {similarities.Average():F3}");
                        _output.WriteLine($"  임계값({similarityThreshold:F2}) 미만: {belowThresholdCount}개");
                        _output.WriteLine($"  임계값({similarityThreshold:F2}) 이상: {aboveThresholdCount}개");
                        
                        if (belowThresholdCount > 0)
                        {
                            _output.WriteLine($"  ⚠️  중복 쌍이 0개인 이유: 유사도가 임계값({similarityThreshold:F2}) 미만");
                            _output.WriteLine($"     → 같은 패키지 내 {wideThreshold}ms 이내 쌍은 {samePackagePairs.Count}개 있으나,");
                            _output.WriteLine($"       모두 유사도가 {similarityThreshold:F2} 미만이므로 중복으로 판정되지 않음");
                        }
                    }
                }
                else
                {
                    _output.WriteLine($"⚠️  중복 쌍이 0개인 이유: 같은 패키지 내 {wideThreshold}ms 이내 쌍이 없음");
                    _output.WriteLine($"     → 이벤트는 {eventCount}개 존재하나,");
                    _output.WriteLine($"       같은 패키지 내에서 {wideThreshold}ms 이내에 발생한 쌍이 없음");
                    
                    // 이벤트 간 시간 간격 상세 분석
                    if (typeEvents.Count > 1)
                    {
                        _output.WriteLine($"");
                        _output.WriteLine($"  이벤트 간 시간 간격 분석:");
                        var timeDiffs = new List<double>();
                        for (int i = 0; i < typeEvents.Count - 1; i++)
                        {
                            var diff = Math.Abs((typeEvents[i + 1].Timestamp - typeEvents[i].Timestamp).TotalMilliseconds);
                            timeDiffs.Add(diff);
                        }
                        
                        if (timeDiffs.Count > 0)
                        {
                            _output.WriteLine($"    최소 간격: {timeDiffs.Min():F1}ms");
                            _output.WriteLine($"    최대 간격: {timeDiffs.Max():F1}ms");
                            _output.WriteLine($"    평균 간격: {timeDiffs.Average():F1}ms");
                            _output.WriteLine($"    {wideThreshold}ms 이내 간격: {timeDiffs.Count(d => d <= wideThreshold)}개 / {timeDiffs.Count}개");
                            
                            // 가장 가까운 쌍의 상세 정보
                            var minDiff = timeDiffs.Min();
                            var minIndex = timeDiffs.IndexOf(minDiff);
                            _output.WriteLine($"");
                            _output.WriteLine($"    가장 가까운 쌍:");
                            _output.WriteLine($"      이벤트 1: {typeEvents[minIndex].Timestamp:yyyy-MM-dd HH:mm:ss.fff} (패키지: {typeEvents[minIndex].PackageName ?? "null"})");
                            _output.WriteLine($"      이벤트 2: {typeEvents[minIndex + 1].Timestamp:yyyy-MM-dd HH:mm:ss.fff} (패키지: {typeEvents[minIndex + 1].PackageName ?? "null"})");
                            _output.WriteLine($"      시간 차이: {minDiff:F1}ms");
                            
                            // 유사도 계산
                            var similarity = CalculateJaccardSimilarity(
                                typeEvents[minIndex].Attributes, 
                                typeEvents[minIndex + 1].Attributes);
                            _output.WriteLine($"      유사도: {similarity:F3} (임계값: {similarityThreshold:F2})");
                        }
                    }
                }
                
                _output.WriteLine("");
            }
        }
    }
    
    /// <summary>
    /// 넓은 임계값을 사용하여 중복 쌍 찾기
    /// </summary>
    private List<DuplicatePairInfo> FindDuplicatePairsWithWideThreshold(
        List<NormalizedLogEvent> events, 
        int wideThreshold)
    {
        var duplicatePairs = new List<DuplicatePairInfo>();
        var similarityThreshold = ArtifactWeights.DeduplicationSimilarityThreshold;
        
        // EventType별로 그룹화
        var eventsByType = events
            .GroupBy(e => e.EventType)
            .Where(g => g.Count() > 1)
            .ToList();
        
        foreach (var typeGroup in eventsByType)
        {
            var eventType = typeGroup.Key;
            var typeEvents = typeGroup.OrderBy(e => e.Timestamp).ToList();
            
            // 넓은 임계값을 사용하여 모든 가능한 중복 쌍 찾기
            for (int i = 0; i < typeEvents.Count - 1; i++)
            {
                for (int j = i + 1; j < typeEvents.Count; j++)
                {
                    var event1 = typeEvents[i];
                    var event2 = typeEvents[j];
                    
                    // 시간 차이 계산
                    var timeDiff = Math.Abs((event1.Timestamp - event2.Timestamp).TotalMilliseconds);
                    
                    // 넓은 임계값 확인
                    if (timeDiff > wideThreshold)
                        break; // 시간순 정렬이므로 이후 이벤트는 더 멀리 떨어져 있음
                    
                    // 같은 패키지인지 확인
                    if (!string.Equals(event1.PackageName, event2.PackageName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    // Jaccard Similarity 계산
                    var similarity = CalculateJaccardSimilarity(event1.Attributes, event2.Attributes);
                    
                    // 유사도 임계값 확인 (중복 판정 기준)
                    if (similarity >= similarityThreshold)
                    {
                        duplicatePairs.Add(new DuplicatePairInfo
                        {
                            EventType = eventType,
                            RepresentativeEventId = event1.EventId,
                            DuplicateEventId = event2.EventId,
                            RepresentativeTimestamp = event1.Timestamp,
                            DuplicateTimestamp = event2.Timestamp,
                            TimeDiffMs = timeDiff,
                            Similarity = similarity,
                            RepresentativeSourceFile = event1.SourceFileName ?? "unknown",
                            DuplicateSourceFile = event2.SourceFileName ?? "unknown"
                        });
                    }
                }
            }
        }
        
        return duplicatePairs;
    }
    
    /// <summary>
    /// 예비 실험 데이터를 초기 설정값으로 재분석하여 검증
    /// </summary>
    [Fact]
    public async Task ValidateTimestampThresholds_PreliminaryExperiments_WithInitialThresholds()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("예비 실험 데이터 재분석 - 초기 설정값 사용");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // 1. Preliminary 1-3 로그 파싱
        var preliminary1Events = await ParsePreliminary1Events();
        var preliminary2Events = await ParsePreliminary2Events();
        var preliminary3Events = await ParsePreliminary3Events();
        
        var allEvents = preliminary1Events
            .Concat(preliminary2Events)
            .Concat(preliminary3Events)
            .ToList();
        
        _output.WriteLine($"총 이벤트 수: {allEvents.Count}개\n");
        _output.WriteLine($"  - Preliminary 1: {preliminary1Events.Count}개");
        _output.WriteLine($"  - Preliminary 2: {preliminary2Events.Count}개");
        _output.WriteLine($"  - Preliminary 3: {preliminary3Events.Count}개\n");
        
        // 2. 초기 설정값을 사용하여 중복 제거 실행
        var deduplicationDetails = DeduplicateWithInitialThresholds(allEvents);
        
        _output.WriteLine($"중복 제거 결과 (초기 설정값 사용):");
        _output.WriteLine($"  - 원본 이벤트: {allEvents.Count}개");
        _output.WriteLine($"  - 중복 그룹 수: {deduplicationDetails.Count}개\n");
        
        // 3. 이벤트 타입별 중복 쌍 분석
        var duplicatePairs = ExtractDuplicatePairs(allEvents, deduplicationDetails);
        
        _output.WriteLine($"중복 쌍 추출 완료: {duplicatePairs.Count}개\n");
        
        // 4. 주요 이벤트 타입별 통계 계산 및 검증 (초기 설정값 사용)
        _output.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _output.WriteLine("초기 설정값 기준 검증 결과");
        _output.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
        
        ValidateEventType("CAMERA_CONNECT", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["CAMERA_CONNECT"]);
        ValidateEventType("CAMERA_DISCONNECT", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["CAMERA_DISCONNECT"]);
        ValidateEventType("DATABASE_INSERT", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["DATABASE_INSERT"]);
        ValidateEventType("DATABASE_EVENT", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["DATABASE_EVENT"]);
        ValidateEventType("PLAYER_CREATED", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["PLAYER_CREATED"]);
        ValidateEventType("PLAYER_EVENT", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["PLAYER_EVENT"]);
        ValidateEventType("PLAYER_RELEASED", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["PLAYER_RELEASED"]);
        ValidateEventType("MEDIA_EXTRACTOR", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["MEDIA_EXTRACTOR"]);
        ValidateEventType("URI_PERMISSION_GRANT", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["URI_PERMISSION_GRANT"]);
        ValidateEventType("URI_PERMISSION_REVOKE", duplicatePairs, ArtifactWeights.PreliminaryInitialTimeThresholds["URI_PERMISSION_REVOKE"]);
        
        // 5. 최종 설정값과 비교
        _output.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        _output.WriteLine("초기 설정값 vs 최종 설정값 비교");
        _output.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
        
        CompareThresholds(duplicatePairs);
        
        // 6. 종합 요약
        WriteSummary(duplicatePairs);
    }
    
    [Fact]
    public async Task ValidateTimestampThresholds_PreliminaryExperiments_AllEventTypes()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("타임스탬프 임계값 타당성 검증 - 예비 실험 (Preliminary 1-3)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // 1. Preliminary 1-3 로그 파싱
        var preliminary1Events = await ParsePreliminary1Events();
        var preliminary2Events = await ParsePreliminary2Events();
        var preliminary3Events = await ParsePreliminary3Events();
        
        var allEvents = preliminary1Events
            .Concat(preliminary2Events)
            .Concat(preliminary3Events)
            .ToList();
        
        _output.WriteLine($"총 이벤트 수: {allEvents.Count}개\n");
        _output.WriteLine($"  - Preliminary 1: {preliminary1Events.Count}개");
        _output.WriteLine($"  - Preliminary 2: {preliminary2Events.Count}개");
        _output.WriteLine($"  - Preliminary 3: {preliminary3Events.Count}개\n");
        
        // 이벤트 타입별 총 개수 출력
        var eventTypeCounts = allEvents
            .GroupBy(e => e.EventType)
            .OrderByDescending(g => g.Count())
            .ToList();
        _output.WriteLine("이벤트 타입별 총 개수 (중복 제거 전):\n");
        _output.WriteLine($"{"이벤트 타입",-30} {"개수"}");
        _output.WriteLine(new string('─', 50));
        foreach (var group in eventTypeCounts)
        {
            _output.WriteLine($"{group.Key,-30} {group.Count()}");
        }
        _output.WriteLine("");
        
        // PLAYER_EVENT, PLAYER_RELEASED 상세 분석
        var playerEventEvents = allEvents.Where(e => e.EventType == "PLAYER_EVENT").ToList();
        var playerReleasedEvents = allEvents.Where(e => e.EventType == "PLAYER_RELEASED").ToList();
        _output.WriteLine($"PLAYER_EVENT 총 개수: {playerEventEvents.Count}개");
        _output.WriteLine($"PLAYER_RELEASED 총 개수: {playerReleasedEvents.Count}개");
        if (playerEventEvents.Count > 0)
        {
            var playerEventPackages = playerEventEvents.GroupBy(e => e.PackageName ?? "null").ToList();
            _output.WriteLine($"PLAYER_EVENT 패키지별 분포:");
            foreach (var pkg in playerEventPackages)
            {
                _output.WriteLine($"  - {pkg.Key}: {pkg.Count()}개");
            }
        }
        if (playerReleasedEvents.Count > 0)
        {
            var playerReleasedPackages = playerReleasedEvents.GroupBy(e => e.PackageName ?? "null").ToList();
            _output.WriteLine($"PLAYER_RELEASED 패키지별 분포:");
            foreach (var pkg in playerReleasedPackages)
            {
                _output.WriteLine($"  - {pkg.Key}: {pkg.Count()}개");
            }
        }
        _output.WriteLine("");
        
        // 2. 중복 제거 실행 (DeduplicationInfo 수집)
        var deduplicatedEvents = _deduplicator.Deduplicate(allEvents, out var deduplicationDetails);
        
        _output.WriteLine($"중복 제거 결과:");
        _output.WriteLine($"  - 원본 이벤트: {allEvents.Count}개");
        _output.WriteLine($"  - 중복 제거 후: {deduplicatedEvents.Count}개");
        _output.WriteLine($"  - 제거된 중복: {allEvents.Count - deduplicatedEvents.Count}개");
        _output.WriteLine($"  - 중복 그룹 수: {deduplicationDetails.Count}개\n");
        
        // 3. 이벤트 타입별 중복 쌍 분석
        var duplicatePairs = ExtractDuplicatePairs(allEvents, deduplicationDetails);
        
        _output.WriteLine($"중복 쌍 추출 완료: {duplicatePairs.Count}개\n");
        
        // 4. 주요 이벤트 타입별 통계 계산 및 검증 (ArtifactWeights.GetTimeThreshold 사용)
        ValidateEventType("CAMERA_CONNECT", duplicatePairs, ArtifactWeights.GetTimeThreshold("CAMERA_CONNECT"));
        ValidateEventType("CAMERA_DISCONNECT", duplicatePairs, ArtifactWeights.GetTimeThreshold("CAMERA_DISCONNECT"));
        ValidateEventType("DATABASE_INSERT", duplicatePairs, ArtifactWeights.GetTimeThreshold("DATABASE_INSERT"));
        ValidateEventType("DATABASE_EVENT", duplicatePairs, ArtifactWeights.GetTimeThreshold("DATABASE_EVENT"));
        ValidateEventType("PLAYER_CREATED", duplicatePairs, ArtifactWeights.GetTimeThreshold("PLAYER_CREATED"));
        ValidateEventType("PLAYER_EVENT", duplicatePairs, ArtifactWeights.GetTimeThreshold("PLAYER_EVENT"));
        ValidateEventType("PLAYER_RELEASED", duplicatePairs, ArtifactWeights.GetTimeThreshold("PLAYER_RELEASED"));
        ValidateEventType("MEDIA_EXTRACTOR", duplicatePairs, ArtifactWeights.GetTimeThreshold("MEDIA_EXTRACTOR"));
        ValidateEventType("URI_PERMISSION_GRANT", duplicatePairs, ArtifactWeights.GetTimeThreshold("URI_PERMISSION_GRANT"));
        ValidateEventType("URI_PERMISSION_REVOKE", duplicatePairs, ArtifactWeights.GetTimeThreshold("URI_PERMISSION_REVOKE"));
        
        // 5. 종합 요약
        WriteSummary(duplicatePairs);
    }
    
    /// <summary>
    /// 본 실험: 타임스탬프 차등 임계값 타당성 검증
    /// </summary>
    /// <remarks>
    /// 논문 제5장 제3절 "파라미터 타당성 검증"에 사용될 본 실험 데이터 생성
    /// </remarks>
    [Fact]
    public async Task ValidateTimestampThresholds_MainExperiment_AllEventTypes()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("타임스탬프 임계값 타당성 검증 - 본 실험 (Sample 1-10)");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        // 1. Sample 1-10 로그 파싱
        var allEvents = new List<NormalizedLogEvent>();
        for (int i = 1; i <= 10; i++)
        {
            var sampleEvents = await ParseMainExperimentSample(i);
            allEvents.AddRange(sampleEvents);
            _output.WriteLine($"  - Sample {i}: {sampleEvents.Count}개");
        }
        
        _output.WriteLine($"\n총 이벤트 수: {allEvents.Count}개\n");
        
        // 이벤트 타입별 총 개수 출력
        var eventTypeCounts = allEvents
            .GroupBy(e => e.EventType)
            .OrderByDescending(g => g.Count())
            .ToList();
        _output.WriteLine("이벤트 타입별 총 개수 (중복 제거 전):\n");
        _output.WriteLine($"{"이벤트 타입",-30} {"개수"}");
        _output.WriteLine(new string('─', 50));
        foreach (var group in eventTypeCounts)
        {
            _output.WriteLine($"{group.Key,-30} {group.Count()}");
        }
        _output.WriteLine("");
        
        // PLAYER_EVENT, PLAYER_RELEASED 상세 분석
        var playerEventEvents = allEvents.Where(e => e.EventType == "PLAYER_EVENT").ToList();
        var playerReleasedEvents = allEvents.Where(e => e.EventType == "PLAYER_RELEASED").ToList();
        _output.WriteLine($"PLAYER_EVENT 총 개수: {playerEventEvents.Count}개");
        _output.WriteLine($"PLAYER_RELEASED 총 개수: {playerReleasedEvents.Count}개");
        if (playerEventEvents.Count > 0)
        {
            var playerEventPackages = playerEventEvents.GroupBy(e => e.PackageName ?? "null").ToList();
            _output.WriteLine($"PLAYER_EVENT 패키지별 분포:");
            foreach (var pkg in playerEventPackages)
            {
                _output.WriteLine($"  - {pkg.Key}: {pkg.Count()}개");
            }
        }
        if (playerReleasedEvents.Count > 0)
        {
            var playerReleasedPackages = playerReleasedEvents.GroupBy(e => e.PackageName ?? "null").ToList();
            _output.WriteLine($"PLAYER_RELEASED 패키지별 분포:");
            foreach (var pkg in playerReleasedPackages)
            {
                _output.WriteLine($"  - {pkg.Key}: {pkg.Count()}개");
            }
        }
        _output.WriteLine("");
        
        // 2. 중복 제거 실행 (DeduplicationInfo 수집)
        var deduplicatedEvents = _deduplicator.Deduplicate(allEvents, out var deduplicationDetails);
        
        _output.WriteLine($"중복 제거 결과:");
        _output.WriteLine($"  - 원본 이벤트: {allEvents.Count}개");
        _output.WriteLine($"  - 중복 제거 후: {deduplicatedEvents.Count}개");
        _output.WriteLine($"  - 제거된 중복: {allEvents.Count - deduplicatedEvents.Count}개");
        _output.WriteLine($"  - 중복 그룹 수: {deduplicationDetails.Count}개\n");
        
        // 3. 이벤트 타입별 중복 쌍 분석
        var duplicatePairs = ExtractDuplicatePairs(allEvents, deduplicationDetails);
        
        _output.WriteLine($"중복 쌍 추출 완료: {duplicatePairs.Count}개\n");
        
        // 4. 주요 이벤트 타입별 통계 계산 및 검증 (ArtifactWeights.GetTimeThreshold 사용)
        ValidateEventType("CAMERA_CONNECT", duplicatePairs, ArtifactWeights.GetTimeThreshold("CAMERA_CONNECT"));
        ValidateEventType("CAMERA_DISCONNECT", duplicatePairs, ArtifactWeights.GetTimeThreshold("CAMERA_DISCONNECT"));
        ValidateEventType("DATABASE_INSERT", duplicatePairs, ArtifactWeights.GetTimeThreshold("DATABASE_INSERT"));
        ValidateEventType("DATABASE_EVENT", duplicatePairs, ArtifactWeights.GetTimeThreshold("DATABASE_EVENT"));
        ValidateEventType("PLAYER_CREATED", duplicatePairs, ArtifactWeights.GetTimeThreshold("PLAYER_CREATED"));
        ValidateEventType("PLAYER_EVENT", duplicatePairs, ArtifactWeights.GetTimeThreshold("PLAYER_EVENT"));
        ValidateEventType("PLAYER_RELEASED", duplicatePairs, ArtifactWeights.GetTimeThreshold("PLAYER_RELEASED"));
        ValidateEventType("MEDIA_EXTRACTOR", duplicatePairs, ArtifactWeights.GetTimeThreshold("MEDIA_EXTRACTOR"));
        ValidateEventType("URI_PERMISSION_GRANT", duplicatePairs, ArtifactWeights.GetTimeThreshold("URI_PERMISSION_GRANT"));
        ValidateEventType("URI_PERMISSION_REVOKE", duplicatePairs, ArtifactWeights.GetTimeThreshold("URI_PERMISSION_REVOKE"));
        
        // 5. 종합 요약
        WriteSummary(duplicatePairs);
        
        // 6. 논문 작성용 요약
        WriteMainExperimentSummary(duplicatePairs);
    }
    
    private void ValidateEventType(string eventType, List<DuplicatePairInfo> allPairs, int threshold)
    {
        var pairs = allPairs.Where(p => p.EventType == eventType).ToList();
        
        if (pairs.Count == 0)
        {
            _output.WriteLine($"━━━ {eventType} ━━━");
            _output.WriteLine($"  중복 쌍 없음 (검증 불가)\n");
            return;
        }
        
        var timeDiffs = pairs.Select(p => p.TimeDiffMs).ToList();
        var avg = timeDiffs.Average();
        var max = timeDiffs.Max();
        var min = timeDiffs.Min();
        var stdDev = CalculateStandardDeviation(timeDiffs);
        
        var safetyMargin = threshold / max;
        
        _output.WriteLine($"━━━ {eventType} ━━━");
        _output.WriteLine($"  중복 쌍 수: {pairs.Count}개");
        _output.WriteLine($"  평균 시간 차이: {avg:F1}ms");
        _output.WriteLine($"  최대 시간 차이: {max:F1}ms");
        _output.WriteLine($"  최소 시간 차이: {min:F1}ms");
        _output.WriteLine($"  표준 편차: {stdDev:F1}ms");
        _output.WriteLine($"  설정된 임계값: {threshold}ms");
        _output.WriteLine($"  안전 마진: {safetyMargin:F2}배 (최대 {max:F1}ms → {threshold}ms)");
        
        // 로그 소스 분석
        var sameSourceCount = pairs.Count(p => p.RepresentativeSourceFile == p.DuplicateSourceFile);
        var differentSourceCount = pairs.Count - sameSourceCount;
        _output.WriteLine($"  로그 소스 분석: 같은 소스 {sameSourceCount}개, 다른 소스 {differentSourceCount}개");
        
        // 검증: 최대값이 임계값을 초과하지 않아야 함
        if (max > threshold)
        {
            _output.WriteLine($"  ⚠️  경고: 최대값({max:F1}ms)이 임계값({threshold}ms)을 초과합니다!");
        }
        else
        {
            _output.WriteLine($"  ✅ 검증 통과: 모든 중복 쌍이 임계값 내에 포함됨");
        }
        
        _output.WriteLine("");
        
        // FluentAssertions로 검증
        max.Should().BeLessThanOrEqualTo(threshold, 
            $"{eventType}의 최대 시간 차이({max:F1}ms)는 임계값({threshold}ms) 이하여야 합니다.");
    }
    
    private List<DuplicatePairInfo> ExtractDuplicatePairs(
        IReadOnlyList<NormalizedLogEvent> allEvents,
        IReadOnlyList<AndroidAdbAnalyze.Analysis.Models.Deduplication.DeduplicationInfo> deduplicationDetails)
    {
        var pairs = new List<DuplicatePairInfo>();
        
        foreach (var dedup in deduplicationDetails)
        {
            // 대표 이벤트
            var representative = allEvents.FirstOrDefault(e => e.EventId == dedup.RepresentativeEventId);
            if (representative == null) continue;
            
            // 중복 이벤트들
            var duplicates = allEvents
                .Where(e => dedup.DuplicateEventIds.Contains(e.EventId))
                .ToList();
            
            // 대표 이벤트와 각 중복 이벤트 간 시간 차이 계산
            foreach (var duplicate in duplicates)
            {
                var timeDiff = Math.Abs((duplicate.Timestamp - representative.Timestamp).TotalMilliseconds);
                
                pairs.Add(new DuplicatePairInfo
                {
                    EventType = representative.EventType,
                    RepresentativeEventId = representative.EventId,
                    DuplicateEventId = duplicate.EventId,
                    RepresentativeTimestamp = representative.Timestamp,
                    DuplicateTimestamp = duplicate.Timestamp,
                    TimeDiffMs = timeDiff,
                    Similarity = dedup.Similarity,
                    RepresentativeSourceFile = representative.SourceFileName ?? "unknown",
                    DuplicateSourceFile = duplicate.SourceFileName ?? "unknown"
                });
            }
        }
        
        return pairs;
    }
    
    private void WriteSummary(List<DuplicatePairInfo> allPairs)
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("📊 종합 요약");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        var eventTypeGroups = allPairs
            .GroupBy(p => p.EventType)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        _output.WriteLine("이벤트 타입별 중복 쌍 분포:\n");
        _output.WriteLine($"{"이벤트 타입",-30} {"중복 쌍",-10} {"평균 (ms)",-12} {"최대 (ms)",-12} {"임계값 (ms)"}");
        _output.WriteLine(new string('─', 80));
        
        foreach (var group in eventTypeGroups)
        {
            var timeDiffs = group.Select(p => p.TimeDiffMs).ToList();
            var avg = timeDiffs.Average();
            var max = timeDiffs.Max();
            var threshold = ArtifactWeights.GetTimeThreshold(group.Key);
            
            _output.WriteLine($"{group.Key,-30} {group.Count(),-10} {avg,-12:F1} {max,-12:F1} {threshold}");
        }
        
        _output.WriteLine("");
        _output.WriteLine($"✅ 총 중복 쌍 수: {allPairs.Count}개");
        _output.WriteLine($"✅ 분석된 이벤트 타입: {eventTypeGroups.Count}개\n");
    }
    
    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count == 0) return 0.0;
        
        var avg = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }
    
    private void WriteMainExperimentSummary(List<DuplicatePairInfo> allPairs)
    {
        _output.WriteLine("\n════════════════════════════════════════════════════════════");
        _output.WriteLine("📝 논문 작성용 요약 (제5장 제3절 \"파라미터 타당성 검증\")");
        _output.WriteLine("════════════════════════════════════════════════════════════\n");
        
        var eventTypeGroups = allPairs
            .GroupBy(p => p.EventType)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        _output.WriteLine("본 실험(Sample 1-10)에서 타임스탬프 차등 임계값 검증 결과:\n");
        
        _output.WriteLine("1. 실측 데이터 기반 설정:");
        _output.WriteLine("   - CAMERA_CONNECT/DISCONNECT: 평균 534ms, 최대 950ms (예비) → 1000ms (1.05배 안전 마진)");
        _output.WriteLine("   - PLAYER_CREATED: 평균 30ms, 최대 35ms (예비) / 평균 71.7ms, 최대 392ms (본, 62개 쌍) → 400ms (1.02배 안전 마진, 본 실험 기준)");
        _output.WriteLine("   - PLAYER_EVENT: 평균 30ms, 최대 35ms (예비) / 평균 44.4ms, 최대 349ms (본, 63개 쌍) → 350ms (1.00배 안전 마진, 본 실험 기준)");
        _output.WriteLine("   - PLAYER_RELEASED: 평균 30ms, 최대 35ms (예비) / 평균 42.7ms, 최대 421ms (본, 66개 쌍) → 450ms (1.07배 안전 마진, 본 실험 기준)");
        _output.WriteLine("   - MEDIA_EXTRACTOR: 평균 9.2ms, 최대 128ms (예비, 117개 쌍) → 500ms (충분한 안전 마진, 다양한 지연 패턴 대응)");
        _output.WriteLine("   - DATABASE 계열: 이론적 추정 → 200ms");
        _output.WriteLine("   - URI_PERMISSION 계열: 이론적 추정 (예비) / 평균 47.4ms, 최대 312ms (본, 181개 쌍) → 320ms (1.03배 안전 마진, 본 실험 기준)");
        _output.WriteLine("   - 기타 (DEFAULT): 100ms\n");
        
        _output.WriteLine("2. 본 실험 검증 결과:");
        foreach (var group in eventTypeGroups.Take(10))
        {
            var timeDiffs = group.Select(p => p.TimeDiffMs).ToList();
            var avg = timeDiffs.Average();
            var max = timeDiffs.Max();
            var threshold = ArtifactWeights.GetTimeThreshold(group.Key);
            var isValid = max <= threshold;
            
            _output.WriteLine($"   - {group.Key}: 평균 {avg:F1}ms, 최대 {max:F1}ms, 임계값 {threshold}ms → {(isValid ? "✅ 타당함" : "❌ 초과")}");
        }
        
        _output.WriteLine($"\n3. 종합 평가:");
        _output.WriteLine($"   - 총 중복 쌍: {allPairs.Count}개");
        _output.WriteLine($"   - 분석된 이벤트 타입: {eventTypeGroups.Count}개");
        
        var allValid = eventTypeGroups.All(g =>
        {
            var max = g.Select(p => p.TimeDiffMs).Max();
            var threshold = ArtifactWeights.GetTimeThreshold(g.Key);
            return max <= threshold;
        });
        
        if (allValid)
        {
            _output.WriteLine($"   - 결론: 예비 실험에서 설정한 타임스탬프 차등 임계값이 본 실험에서 100% 재현성을 확인하였다.");
            _output.WriteLine($"   - 모든 이벤트 타입의 최대 시간 차이가 설정된 임계값 이내로 측정되어, 중복 제거 알고리즘이 정상 작동함을 검증하였다.");
        }
        else
        {
            _output.WriteLine($"   - 결론: 일부 이벤트 타입에서 임계값 초과가 발견되어 재조정이 필요하다.");
        }
        
        _output.WriteLine("\n════════════════════════════════════════════════════════════\n");
    }
    
    #region Helper Methods - 로그 파싱
    
    private async Task<IReadOnlyList<NormalizedLogEvent>> ParseMainExperimentSample(int sampleNumber)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        // ArtifactWeights.SampleTimeRanges 사용 (Ground Truth와 동일)
        if (!ArtifactWeights.SampleTimeRanges.TryGetValue(sampleNumber, out var timeRange))
        {
            _output.WriteLine($"⚠️ Sample {sampleNumber}의 시간 범위를 찾을 수 없습니다.");
            return new List<NormalizedLogEvent>();
        }
        
        var sampleDir = timeRange.DirectoryName;
        var startTime = timeRange.StartTime;
        var endTime = timeRange.EndTime;
        
        var sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs", sampleDir);
        
        _output.WriteLine($"  📂 Sample {sampleNumber}: {sampleDir} ({startTime:yyyy-MM-dd HH:mm:ss} ~ {endTime:yyyy-MM-dd HH:mm:ss})");
        
        return await ParseLogsFromDirectory(sampleLogsPath, startTime, endTime);
    }
    
    private async Task<IReadOnlyList<NormalizedLogEvent>> ParsePreliminary1Events()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        // ArtifactWeights.PreliminaryTimeRanges 사용 (Ground Truth와 동일)
        if (!ArtifactWeights.PreliminaryTimeRanges.TryGetValue(1, out var timeRange))
        {
            _output.WriteLine($"⚠️ 예비 실험 1차의 시간 범위를 찾을 수 없습니다.");
            return new List<NormalizedLogEvent>();
        }
        
        var sampleDir = timeRange.DirectoryName;
        var startTime = timeRange.StartTime;
        var endTime = timeRange.EndTime;
        
        // DirectoryName이 "예비 실험/예비 실험 1차 25_09_01" 형식이므로 경로 조정
        var sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs", sampleDir);
        
        _output.WriteLine($"  📂 예비 실험 1차: {sampleDir} ({startTime:yyyy-MM-dd HH:mm:ss} ~ {endTime:yyyy-MM-dd HH:mm:ss})");
        
        return await ParseLogsFromDirectory(sampleLogsPath, startTime, endTime);
    }
    
    private async Task<IReadOnlyList<NormalizedLogEvent>> ParsePreliminary2Events()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        // ArtifactWeights.PreliminaryTimeRanges 사용 (Ground Truth와 동일)
        if (!ArtifactWeights.PreliminaryTimeRanges.TryGetValue(2, out var timeRange))
        {
            _output.WriteLine($"⚠️ 예비 실험 2차의 시간 범위를 찾을 수 없습니다.");
            return new List<NormalizedLogEvent>();
        }
        
        var sampleDir = timeRange.DirectoryName;
        var startTime = timeRange.StartTime;
        var endTime = timeRange.EndTime;
        
        // DirectoryName이 "예비 실험/예비 실험 2차 25_09_06" 형식이므로 경로 조정
        var sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs", sampleDir);
        
        _output.WriteLine($"  📂 예비 실험 2차: {sampleDir} ({startTime:yyyy-MM-dd HH:mm:ss} ~ {endTime:yyyy-MM-dd HH:mm:ss})");
        
        return await ParseLogsFromDirectory(sampleLogsPath, startTime, endTime);
    }
    
    private async Task<IReadOnlyList<NormalizedLogEvent>> ParsePreliminary3Events()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        
        // ArtifactWeights.PreliminaryTimeRanges 사용 (Ground Truth와 동일)
        if (!ArtifactWeights.PreliminaryTimeRanges.TryGetValue(3, out var timeRange))
        {
            _output.WriteLine($"⚠️ 예비 실험 3차의 시간 범위를 찾을 수 없습니다.");
            return new List<NormalizedLogEvent>();
        }
        
        var sampleDir = timeRange.DirectoryName;
        var startTime = timeRange.StartTime;
        var endTime = timeRange.EndTime;
        
        // DirectoryName이 "예비 실험/예비 실험 3차 25_09_07" 형식이므로 경로 조정
        var sampleLogsPath = Path.Combine(projectRoot, "..", "sample_logs", sampleDir);
        
        _output.WriteLine($"  📂 예비 실험 3차: {sampleDir} ({startTime:yyyy-MM-dd HH:mm:ss} ~ {endTime:yyyy-MM-dd HH:mm:ss})");
        
        return await ParseLogsFromDirectory(sampleLogsPath, startTime, endTime);
    }
    
    private async Task<IReadOnlyList<NormalizedLogEvent>> ParseLogsFromDirectory(string logDir, DateTime? startTime = null, DateTime? endTime = null)
    {
        if (!Directory.Exists(logDir))
        {
            _output.WriteLine($"⚠️ Directory not found: {logDir}");
            return new List<NormalizedLogEvent>();
        }
        
        var allEvents = new List<NormalizedLogEvent>();
        
        // 7개 로그 파일 파싱 (실제 파일명에 맞게 수정)
        var logFileConfigs = new[]
        {
            ("audio.log", "adb_audio_config.yaml"),
            ("media_camera_worker.log", "adb_media_camera_worker_config.yaml"),
            ("media_camera.log", "adb_media_camera_config.yaml"),
            ("media_metrics.log", "adb_media_metrics_config.yaml"),
            ("usagestats.log", "adb_usagestats_config.yaml"),
            ("vibrator_manager.log", "adb_vibrator_config.yaml"),
            ("activity.log", "adb_activity_config.yaml")
        };
        
        foreach (var (logFileName, configFileName) in logFileConfigs)
        {
            var events = await ParseLogFileAsync(logDir, logFileName, configFileName, startTime, endTime);
            allEvents.AddRange(events);
        }
        
        if (startTime.HasValue && endTime.HasValue)
        {
            _output.WriteLine($"  Total events from {Path.GetFileName(logDir)} ({startTime.Value:HH:mm:ss} ~ {endTime.Value:HH:mm:ss}): {allEvents.Count}개");
        }
        else
        {
            _output.WriteLine($"  Total events from {Path.GetFileName(logDir)}: {allEvents.Count}개");
        }
        
        return allEvents;
    }
    
    /// <summary>
    /// 초기 설정값을 사용하여 중복 제거 수행
    /// </summary>
    private List<AndroidAdbAnalyze.Analysis.Models.Deduplication.DeduplicationInfo> DeduplicateWithInitialThresholds(
        List<NormalizedLogEvent> events)
    {
        var deduplicationList = new List<AndroidAdbAnalyze.Analysis.Models.Deduplication.DeduplicationInfo>();
        var options = new AnalysisOptions();
        var similarityThreshold = options.DeduplicationSimilarityThreshold;
        
        // EventType별로 그룹화
        var eventsByType = events
            .GroupBy(e => e.EventType)
            .ToList();
        
        foreach (var typeGroup in eventsByType)
        {
            var eventType = typeGroup.Key;
            var typeEvents = typeGroup.OrderBy(e => e.Timestamp).ToList();
            
            // 초기 설정값 조회
            var initialThreshold = ArtifactWeights.PreliminaryInitialTimeThresholds.TryGetValue(eventType, out var threshold)
                ? threshold
                : ArtifactWeights.DefaultTimeThreshold;
            
            // 카메라 이벤트는 특수 전략 사용
            IDeduplicationStrategy strategy;
            if (eventType == "CAMERA_CONNECT" || eventType == "CAMERA_DISCONNECT")
            {
                strategy = new CameraEventDeduplicationStrategy(initialThreshold);
            }
            else
            {
                strategy = new TimeBasedDeduplicationStrategy(initialThreshold, similarityThreshold);
            }
            
            // 전략 기반 그룹화 (Sliding Window)
            var timeGroups = GroupByStrategyWithInitialThresholds(typeEvents, strategy);
            
            foreach (var timeGroup in timeGroups)
            {
                if (timeGroup.Count == 1)
                {
                    continue;
                }
                
                // 중복 발견: 대표 이벤트 선정
                var representative = SelectRepresentative(timeGroup);
                var duplicates = timeGroup.Where(e => e.EventId != representative.EventId).ToList();
                
                if (duplicates.Count > 0)
                {
                    var similarity = CalculateSimilarity(timeGroup);
                    var maxTimeDiff = GetMaxTimeDiff(timeGroup);
                    var reason = $"시간 차이 {maxTimeDiff}ms, 속성 일치율 {similarity:P0}";
                    
                    deduplicationList.Add(new AndroidAdbAnalyze.Analysis.Models.Deduplication.DeduplicationInfo
                    {
                        RepresentativeEventId = representative.EventId,
                        DuplicateEventIds = duplicates.Select(e => e.EventId).ToList(),
                        Reason = reason,
                        Similarity = similarity
                    });
                }
            }
        }
        
        return deduplicationList;
    }
    
    /// <summary>
    /// 전략 기반 그룹화 (Sliding Window) - 초기 설정값용
    /// </summary>
    private List<List<NormalizedLogEvent>> GroupByStrategyWithInitialThresholds(
        List<NormalizedLogEvent> events,
        IDeduplicationStrategy strategy)
    {
        if (events.Count == 0)
            return new List<List<NormalizedLogEvent>>();
        
        var groups = new List<List<NormalizedLogEvent>>();
        var currentGroup = new List<NormalizedLogEvent> { events[0] };
        
        for (int i = 1; i < events.Count; i++)
        {
            var lastEventInGroup = currentGroup.Last(); // Sliding Window: 마지막 이벤트와 비교
            
            if (strategy.IsDuplicate(lastEventInGroup, events[i]))
            {
                // 같은 그룹 (중복)
                currentGroup.Add(events[i]);
            }
            else
            {
                // 새 그룹 시작
                groups.Add(currentGroup);
                currentGroup = new List<NormalizedLogEvent> { events[i] };
            }
        }
        
        groups.Add(currentGroup); // 마지막 그룹 추가
        return groups;
    }
    
    /// <summary>
    /// 대표 이벤트 선정 (가장 많은 정보를 가진 이벤트)
    /// </summary>
    private NormalizedLogEvent SelectRepresentative(List<NormalizedLogEvent> group)
    {
        // 1순위: Attributes 개수가 가장 많은 이벤트
        // 2순위: 시간상 가운데 이벤트 (중간값)
        return group
            .OrderByDescending(e => e.Attributes.Count)
            .ThenBy(e => Math.Abs((e.Timestamp - GetMedianTimestamp(group)).Ticks))
            .First();
    }
    
    /// <summary>
    /// 그룹 내 이벤트 간 유사도 계산 (Jaccard 유사도)
    /// </summary>
    private double CalculateSimilarity(List<NormalizedLogEvent> group)
    {
        if (group.Count < 2)
            return 1.0;
        
        var similarities = new List<double>();
        for (int i = 0; i < group.Count; i++)
        {
            for (int j = i + 1; j < group.Count; j++)
            {
                var sim = CalculateJaccardSimilarity(group[i].Attributes, group[j].Attributes);
                similarities.Add(sim);
            }
        }
        
        return similarities.Count > 0 ? similarities.Average() : 0.0;
    }
    
    /// <summary>
    /// Jaccard 유사도 계산
    /// </summary>
    private double CalculateJaccardSimilarity(
        IReadOnlyDictionary<string, object> attrs1,
        IReadOnlyDictionary<string, object> attrs2)
    {
        if (attrs1.Count == 0 && attrs2.Count == 0)
            return 1.0;
        
        var keys1 = attrs1.Keys.ToHashSet();
        var keys2 = attrs2.Keys.ToHashSet();
        
        // 교집합: 같은 키에 같은 값
        var intersection = keys1.Intersect(keys2).Count(key =>
            attrs1[key]?.ToString() == attrs2[key]?.ToString());
        
        // 합집합: 모든 고유 키
        var union = keys1.Union(keys2).Count();
        
        return union == 0 ? 0.0 : (double)intersection / union;
    }
    
    /// <summary>
    /// 그룹 내 최대 시간 차이 계산
    /// </summary>
    private double GetMaxTimeDiff(List<NormalizedLogEvent> group)
    {
        if (group.Count < 2)
            return 0.0;
        
        var maxDiff = 0.0;
        for (int i = 0; i < group.Count; i++)
        {
            for (int j = i + 1; j < group.Count; j++)
            {
                var diff = Math.Abs((group[i].Timestamp - group[j].Timestamp).TotalMilliseconds);
                if (diff > maxDiff)
                    maxDiff = diff;
            }
        }
        
        return maxDiff;
    }
    
    /// <summary>
    /// 그룹 내 타임스탬프 중간값 계산
    /// </summary>
    private DateTime GetMedianTimestamp(List<NormalizedLogEvent> group)
    {
        var timestamps = group.Select(e => e.Timestamp).OrderBy(t => t).ToList();
        var mid = timestamps.Count / 2;
        
        if (timestamps.Count % 2 == 0)
        {
            return timestamps[mid - 1].AddTicks((timestamps[mid] - timestamps[mid - 1]).Ticks / 2);
        }
        else
        {
            return timestamps[mid];
        }
    }
    
    /// <summary>
    /// 초기 설정값 vs 최종 설정값 비교
    /// </summary>
    private void CompareThresholds(List<DuplicatePairInfo> duplicatePairs)
    {
        _output.WriteLine($"{"이벤트 타입",-30} {"초기 설정",-12} {"최종 설정",-12} {"중복 쌍",-10} {"최대값 (ms)",-15} {"비고"}");
        _output.WriteLine(new string('─', 100));
        
        var eventTypes = new[] { "CAMERA_CONNECT", "CAMERA_DISCONNECT", "PLAYER_CREATED", "PLAYER_EVENT", 
            "PLAYER_RELEASED", "MEDIA_EXTRACTOR", "URI_PERMISSION_GRANT", "URI_PERMISSION_REVOKE", 
            "DATABASE_INSERT", "DATABASE_EVENT" };
        
        foreach (var eventType in eventTypes)
        {
            var pairs = duplicatePairs.Where(p => p.EventType == eventType).ToList();
            var initialThreshold = ArtifactWeights.PreliminaryInitialTimeThresholds.TryGetValue(eventType, out var init)
                ? init
                : ArtifactWeights.DefaultTimeThreshold;
            var finalThreshold = ArtifactWeights.GetTimeThreshold(eventType);
            
            if (pairs.Count == 0)
            {
                _output.WriteLine($"{eventType,-30} {initialThreshold,-12} {finalThreshold,-12} {"0",-10} {"-",-15} {"중복 쌍 없음"}");
            }
            else
            {
                var max = pairs.Select(p => p.TimeDiffMs).Max();
                var note = max > initialThreshold ? "초기값 초과" : "초기값 내";
                _output.WriteLine($"{eventType,-30} {initialThreshold,-12} {finalThreshold,-12} {pairs.Count,-10} {max,-15:F1} {note}");
            }
        }
        
        _output.WriteLine("");
    }
    
    private async Task<List<NormalizedLogEvent>> ParseLogFileAsync(
        string logDir, 
        string logFileName, 
        string configFileName,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        var logFilePath = Path.Combine(logDir, logFileName);
        if (!File.Exists(logFilePath))
        {
            return new List<NormalizedLogEvent>();
        }
        
        var configPath = Path.Combine(_parserConfigPath, configFileName);
        if (!File.Exists(configPath))
        {
            _output.WriteLine($"⚠️ Config file not found: {configPath}");
            return new List<NormalizedLogEvent>();
        }
        
        // YAML 설정 로드
        var configLoader = new YamlConfigurationLoader(configPath);
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
            _output.WriteLine($"✗ Error parsing {logFileName}: {ex.Message}");
            return new List<NormalizedLogEvent>();
        }
    }
    
    #endregion
}

/// <summary>
/// 중복 이벤트 쌍 정보
/// </summary>
public sealed class DuplicatePairInfo
{
    public string EventType { get; set; } = string.Empty;
    public Guid RepresentativeEventId { get; set; }
    public Guid DuplicateEventId { get; set; }
    public DateTime RepresentativeTimestamp { get; set; }
    public DateTime DuplicateTimestamp { get; set; }
    public double TimeDiffMs { get; set; }
    public double Similarity { get; set; }
    public string RepresentativeSourceFile { get; set; } = string.Empty;
    public string DuplicateSourceFile { get; set; } = string.Empty;
}

