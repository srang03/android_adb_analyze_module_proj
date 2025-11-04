using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.Aggregated;

/// <summary>
/// 휘발성 영향 통합 검증
/// 목적: 논문 5.3절 "휘발성 영향 분석" 표의 사실 기반 검증
/// 방법: GT 문서 직접 파싱 및 집계
/// </summary>
public sealed class VolatilityImpactVerification
{
    private readonly ITestOutputHelper _output;
    private readonly string _gtDocPath;

    public VolatilityImpactVerification(ITestOutputHelper output)
    {
        _output = output;

        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        _gtDocPath = Path.Combine(projectRoot, "AndroidAdbAnalyze.Analysis.Tests", "Documentation", "GroundTruth");
    }

    [Fact]
    public void Verify_24Hours_Volatility_Impact()
    {
        _output.WriteLine("=== 24시간 후 휘발성 영향 검증 (사실 기반) ===\n");
        _output.WriteLine("목적: 논문 5.3절 '휘발성 영향 분석' 표의 실제 데이터 검증\n");
        _output.WriteLine("========================================\n");

        // 24시간 휘발성 샘플: Sample 2, 3, 5
        var samples = new[] { 2, 3, 5 };
        var volatilityData = new List<VolatilityPerformance>();

        foreach (var sampleNum in samples)
        {
            // T0 (실시간) GT 문서
            var t0Path = Path.Combine(_gtDocPath, $"Sample{sampleNum}_Ground_Truth.md");

            // T1 (24시간 후) GT 문서
            var t1Path = Path.Combine(_gtDocPath, "Volatility", $"Sample{sampleNum}_Volatility24h_Ground_Truth.md");

            if (!File.Exists(t0Path))
            {
                _output.WriteLine($"⚠️ Sample {sampleNum} T0 GT 문서 없음");
                continue;
            }

            if (!File.Exists(t1Path))
            {
                _output.WriteLine($"⚠️ Sample {sampleNum} T1 (24h) GT 문서 없음");
                continue;
            }

            var t0Captures = ParseCaptureCount(t0Path);
            var t1Captures = ParseCaptureCount(t1Path);
            var usagestatsEvents = ParseUsagestatsEventCount(t1Path);

            var perf = new VolatilityPerformance
            {
                SampleNumber = sampleNum,
                T0Captures = t0Captures,
                T1Captures = t1Captures,
                UsagestatsEvents = usagestatsEvents
            };

            volatilityData.Add(perf);

            _output.WriteLine($"✓ Sample {sampleNum}: T0={t0Captures}개, T1={t1Captures}개, usagestats={usagestatsEvents}개 이벤트");
        }

        _output.WriteLine($"\n총 {volatilityData.Count}개 샘플 데이터 수집 완료\n");

        // 통합 지표 계산
        _output.WriteLine("## 📊 24시간 후 휘발성 영향 분석\n");
        _output.WriteLine("### 논문 5.3.1절 \"휘발성 조건별 성능 변화\" 검증\n");

        _output.WriteLine("| Sample | T0 촬영 | T1 촬영 | 탐지율 | usagestats 잔존 |");
        _output.WriteLine("|--------|---------|---------|--------|-----------------|");

        double totalDetectionRate = 0;

        foreach (var perf in volatilityData)
        {
            var detectionRate = perf.T0Captures > 0 ? (double)perf.T1Captures / perf.T0Captures : 0;
            totalDetectionRate += detectionRate;

            _output.WriteLine($"| Sample {perf.SampleNumber} | {perf.T0Captures} | {perf.T1Captures} | {detectionRate:P1} | {perf.UsagestatsEvents} events |");
        }

        var avgDetectionRate = volatilityData.Count > 0 ? totalDetectionRate / volatilityData.Count : 0;
        var avgUsagestatsEvents = volatilityData.Count > 0 ? volatilityData.Average(v => v.UsagestatsEvents) : 0;

        _output.WriteLine($"| **평균** | **{volatilityData.Average(v => v.T0Captures):F1}** | **{volatilityData.Average(v => v.T1Captures):F1}** | **{avgDetectionRate:P1}** | **{avgUsagestatsEvents:F1} events** |");

        _output.WriteLine($"\n### 검증 결과:");
        _output.WriteLine($"✅ 24시간 후 평균 탐지율: {avgDetectionRate:P1}");
        _output.WriteLine($"✅ usagestats 평균 잔존: {avgUsagestatsEvents:F0} events");

        if (avgDetectionRate >= 0.9)
        {
            _output.WriteLine($"✅ 탐지율 90% 이상 유지 → 로그 휘발성에도 불구하고 높은 탐지 성능");
        }

        _output.WriteLine($"\n### 논문 작성 권장 내용:");
        _output.WriteLine($"```");
        _output.WriteLine($"[표 X] 휘발성 조건별 성능 변화 (24시간 후)");
        _output.WriteLine($"");
        _output.WriteLine($"| 조건 | Sample | T0 촬영 | T1 촬영 | 탐지율 | usagestats 잔존 |");
        _output.WriteLine($"|------|--------|---------|---------|--------|-----------------|");

        foreach (var perf in volatilityData)
        {
            var detectionRate = perf.T0Captures > 0 ? (double)perf.T1Captures / perf.T0Captures : 0;
            _output.WriteLine($"| **24시간 후** | Sample{perf.SampleNumber} | {perf.T0Captures} | {perf.T1Captures} | {detectionRate:P1} | {perf.UsagestatsEvents} events |");
        }

        _output.WriteLine($"| | **평균** | **{volatilityData.Average(v => v.T0Captures):F1}** | **{volatilityData.Average(v => v.T1Captures):F1}** | **{avgDetectionRate:P1}** | **{avgUsagestatsEvents:F1} events** |");
        _output.WriteLine($"```");
    }

    [Fact]
    public void Verify_Reboot_Volatility_Impact()
    {
        _output.WriteLine("=== 재부팅 후 휘발성 영향 검증 (사실 기반) ===\n");
        _output.WriteLine("목적: 논문 5.3절 '휘발성 영향 분석' 표의 실제 데이터 검증\n");
        _output.WriteLine("========================================\n");

        // 재부팅 샘플: Sample 1, 4, 9
        var samples = new[] { 1, 4, 9 };
        var rebootData = new List<VolatilityPerformance>();

        foreach (var sampleNum in samples)
        {
            // T0 (실시간) GT 문서
            var t0Path = Path.Combine(_gtDocPath, $"Sample{sampleNum}_Ground_Truth.md");

            // TReboot (재부팅 후) GT 문서
            var trebootPath = Path.Combine(_gtDocPath, "Reboot", $"Sample{sampleNum}_Reboot_Ground_Truth.md");

            if (!File.Exists(t0Path))
            {
                _output.WriteLine($"⚠️ Sample {sampleNum} T0 GT 문서 없음");
                continue;
            }

            if (!File.Exists(trebootPath))
            {
                _output.WriteLine($"⚠️ Sample {sampleNum} TReboot GT 문서 없음");
                continue;
            }

            var t0Captures = ParseCaptureCount(t0Path);
            var trebootCaptures = ParseCaptureCount(trebootPath);
            var usagestatsEvents = ParseUsagestatsEventCount(trebootPath);

            var perf = new VolatilityPerformance
            {
                SampleNumber = sampleNum,
                T0Captures = t0Captures,
                T1Captures = trebootCaptures,
                UsagestatsEvents = usagestatsEvents
            };

            rebootData.Add(perf);

            _output.WriteLine($"✓ Sample {sampleNum}: T0={t0Captures}개, TReboot={trebootCaptures}개, usagestats={usagestatsEvents}개 이벤트");
        }

        _output.WriteLine($"\n총 {rebootData.Count}개 샘플 데이터 수집 완료\n");

        // 통합 지표 계산
        _output.WriteLine("## 📊 재부팅 후 휘발성 영향 분석\n");
        _output.WriteLine("### 논문 5.3.1절 \"휘발성 조건별 성능 변화\" 검증\n");

        _output.WriteLine("| Sample | T0 촬영 | TReboot 촬영 | 탐지율 | usagestats 잔존 |");
        _output.WriteLine("|--------|---------|--------------|--------|-----------------|");

        double totalDetectionRate = 0;

        foreach (var perf in rebootData)
        {
            var detectionRate = perf.T0Captures > 0 ? (double)perf.T1Captures / perf.T0Captures : 0;
            totalDetectionRate += detectionRate;

            _output.WriteLine($"| Sample {perf.SampleNumber} | {perf.T0Captures} | {perf.T1Captures} | {detectionRate:P1} | {perf.UsagestatsEvents} events |");
        }

        var avgDetectionRate = rebootData.Count > 0 ? totalDetectionRate / rebootData.Count : 0;
        var avgUsagestatsEvents = rebootData.Count > 0 ? rebootData.Average(v => v.UsagestatsEvents) : 0;

        _output.WriteLine($"| **평균** | **{rebootData.Average(v => v.T0Captures):F1}** | **{rebootData.Average(v => v.T1Captures):F1}** | **{avgDetectionRate:P1}** | **{avgUsagestatsEvents:F1} events** |");

        _output.WriteLine($"\n### 검증 결과:");
        _output.WriteLine($"❌ 재부팅 후 평균 탐지율: {avgDetectionRate:P1}");
        _output.WriteLine($"❌ usagestats 완전 휘발: {avgUsagestatsEvents:F0} events");

        if (avgDetectionRate == 0)
        {
            _output.WriteLine($"❌ 재부팅 후 탐지율 0% → usagestats 완전 휘발로 세션 식별 불가");
        }

        _output.WriteLine($"\n### 논문 작성 권장 내용:");
        _output.WriteLine($"```");
        _output.WriteLine($"[표 X] 휘발성 조건별 성능 변화 (재부팅 후)");
        _output.WriteLine($"");
        _output.WriteLine($"| 조건 | Sample | T0 촬영 | TReboot 촬영 | 탐지율 | usagestats 잔존 |");
        _output.WriteLine($"|------|--------|---------|--------------|--------|-----------------|");

        foreach (var perf in rebootData)
        {
            var detectionRate = perf.T0Captures > 0 ? (double)perf.T1Captures / perf.T0Captures : 0;
            _output.WriteLine($"| **재부팅 후** | Sample{perf.SampleNumber} | {perf.T0Captures} | {perf.T1Captures} | {detectionRate:P1} | {perf.UsagestatsEvents} events |");
        }

        _output.WriteLine($"| | **평균** | **{rebootData.Average(v => v.T0Captures):F1}** | **{rebootData.Average(v => v.T1Captures):F1}** | **{avgDetectionRate:P1}** | **{avgUsagestatsEvents:F1} events** |");
        _output.WriteLine($"```");
    }

    private int ParseCaptureCount(string gtPath)
    {
        var content = File.ReadAllText(gtPath);

        // "**총 촬영 수**: 4개" 형식 파싱
        var match = Regex.Match(content, @"\*\*총 촬영 수\*\*:\s*(\d+)개");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private int ParseUsagestatsEventCount(string gtPath)
    {
        var content = File.ReadAllText(gtPath);

        // usagestats 이벤트 수 파싱 (문서에 명시적으로 기재되어 있을 경우)
        // 예: "usagestats: 45 events"
        var match = Regex.Match(content, @"usagestats[:\s]+(\d+)\s*events?", RegexOptions.IgnoreCase);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private class VolatilityPerformance
    {
        public int SampleNumber { get; set; }
        public int T0Captures { get; set; }
        public int T1Captures { get; set; }
        public int UsagestatsEvents { get; set; }
    }
}

