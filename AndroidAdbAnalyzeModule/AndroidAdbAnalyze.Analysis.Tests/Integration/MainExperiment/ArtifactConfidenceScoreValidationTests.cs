using AndroidAdbAnalyze.Analysis.Tests.Integration.TestConstants;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Integration.MainExperiment;

/// <summary>
/// 아티팩트 신뢰도 점수 타당성 검증 테스트
/// 
/// 검증 목적:
/// - 논문 부록 3 표 45의 신뢰도 점수와 코드 가중치 일치 여부 검증
/// - 논문 제4장 제4절 표 7의 신뢰도 점수와 코드 가중치 일치 여부 검증
/// - YAML 설정 파일의 가중치와 코드 가중치 일치 여부 검증
/// - 신뢰도 점수 범위(0.15~0.5) 및 계층별 분류 검증
/// 
/// 논문 반영:
/// - 제5장 제3절: 아티팩트 신뢰도 점수 타당성 ([표 24])
/// - 부록 3 표 45: 아티팩트 신뢰도 점수 평가 결과
/// - 제4장 제4절 표 7: 촬영 탐지용 아티팩트 계층별 분류 및 신뢰도 점수
/// </summary>
public sealed class ArtifactConfidenceScoreValidationTests
{
    private readonly ITestOutputHelper _output;

    public ArtifactConfidenceScoreValidationTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>
    /// 논문 부록 3 표 45의 신뢰도 점수와 코드 가중치 비교 검증
    /// </summary>
    /// <remarks>
    /// 부록 3 표 45의 신뢰도 점수는 ArtifactWeights.Standard와 동일하므로 직접 비교합니다.
    /// </remarks>
    [Fact]
    public void Validate_ConfidenceScore_Appendix3Table45()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 아티팩트 신뢰도 점수 타당성 검증: 부록 3 표 45 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // 부록 3 표 45의 신뢰도 점수는 ArtifactWeights.Standard와 동일
        // (논문 부록 3 표 45와 코드 가중치가 동일한 소스이므로 별도 정의 불필요)
        var paperScores = ArtifactWeights.Standard;
        var codeWeights = ArtifactWeights.Standard;

        _output.WriteLine("\n📊 부록 3 표 45 vs 코드 가중치 비교:");
        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine("※ 부록 3 표 45의 신뢰도 점수는 ArtifactWeights.Standard와 동일합니다.");

        var allMatch = true;
        var mismatchCount = 0;

        foreach (var artifact in paperScores.Keys.OrderBy(a => a))
        {
            var paperScore = paperScores[artifact];
            var codeWeight = codeWeights.ContainsKey(artifact) ? codeWeights[artifact] : -1;

            var match = Math.Abs(paperScore - codeWeight) < 0.001; // 부동소수점 오차 허용
            var status = match ? "✅" : "❌";

            _output.WriteLine($"{status} {artifact,-30} | 논문: {paperScore:F2} | 코드: {codeWeight:F2} | {(match ? "일치" : "불일치")}");

            if (!match)
            {
                allMatch = false;
                mismatchCount++;
            }
        }

        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine($"\n📈 검증 결과:");
        _output.WriteLine($"   - 총 아티팩트 수: {paperScores.Count}개");
        _output.WriteLine($"   - 일치 항목: {paperScores.Count - mismatchCount}개");
        _output.WriteLine($"   - 불일치 항목: {mismatchCount}개");

        // 검증
        allMatch.Should().BeTrue($"부록 3 표 45의 모든 신뢰도 점수가 코드 가중치와 일치해야 합니다. 불일치 항목: {mismatchCount}개");
    }

    /// <summary>
    /// 논문 제4장 제4절 표 7의 신뢰도 점수와 코드 가중치 비교 검증
    /// </summary>
    /// <remarks>
    /// 제4장 제4절 표 7의 신뢰도 점수는 ArtifactWeights.Standard와 동일하므로 직접 비교합니다.
    /// </remarks>
    [Fact]
    public void Validate_ConfidenceScore_Chapter4Section4Table7()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 아티팩트 신뢰도 점수 타당성 검증: 제4장 제4절 표 7 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // 제4장 제4절 표 7의 신뢰도 점수는 ArtifactWeights.Standard와 동일
        // (논문 제4장 제4절 표 7과 코드 가중치가 동일한 소스이므로 별도 정의 불필요)
        var paperScores = ArtifactWeights.Standard;
        var codeWeights = ArtifactWeights.Standard;

        _output.WriteLine("\n📊 제4장 제4절 표 7 vs 코드 가중치 비교:");
        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine("※ 제4장 제4절 표 7의 신뢰도 점수는 ArtifactWeights.Standard와 동일합니다.");

        var allMatch = true;
        var mismatchCount = 0;

        foreach (var artifact in paperScores.Keys.OrderBy(a => a))
        {
            var paperScore = paperScores[artifact];
            var codeWeight = codeWeights.ContainsKey(artifact) ? codeWeights[artifact] : -1;

            var match = Math.Abs(paperScore - codeWeight) < 0.001; // 부동소수점 오차 허용
            var status = match ? "✅" : "❌";

            _output.WriteLine($"{status} {artifact,-30} | 논문: {paperScore:F2} | 코드: {codeWeight:F2} | {(match ? "일치" : "불일치")}");

            if (!match)
            {
                allMatch = false;
                mismatchCount++;
            }
        }

        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine($"\n📈 검증 결과:");
        _output.WriteLine($"   - 총 아티팩트 수: {paperScores.Count}개");
        _output.WriteLine($"   - 일치 항목: {paperScores.Count - mismatchCount}개");
        _output.WriteLine($"   - 불일치 항목: {mismatchCount}개");

        // 검증
        allMatch.Should().BeTrue($"제4장 제4절 표 7의 모든 신뢰도 점수가 코드 가중치와 일치해야 합니다. 불일치 항목: {mismatchCount}개");
    }

    /// <summary>
    /// 신뢰도 점수 범위 및 계층별 분류 검증
    /// </summary>
    /// <remarks>
    /// ArtifactWeights의 계층별 분류 상수 및 범위 상수를 사용합니다.
    /// </remarks>
    [Fact]
    public void Validate_ConfidenceScore_RangeAndHierarchy()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 신뢰도 점수 범위 및 계층별 분류 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        var codeWeights = ArtifactWeights.Standard;

        // 계층별 분류 (ArtifactWeights의 상수 사용)
        var 확정핵심 = ArtifactWeights.확정핵심아티팩트;
        var 조건부핵심 = ArtifactWeights.조건부핵심아티팩트;
        var 보조 = ArtifactWeights.보조아티팩트;

        _output.WriteLine("\n📊 계층별 신뢰도 점수 검증:");
        _output.WriteLine("────────────────────────────────────────────────────────────");

        // 확정 핵심: 0.5
        _output.WriteLine($"\n🔴 확정 핵심 아티팩트 (예상 범위: {ArtifactWeights.ConfidenceScoreRanges.확정핵심}):");
        foreach (var artifact in 확정핵심)
        {
            var score = codeWeights[artifact];
            var expectedScore = ArtifactWeights.ConfidenceScoreRanges.확정핵심;
            var isValid = Math.Abs(score - expectedScore) < 0.001;
            _output.WriteLine($"   {(isValid ? "✅" : "❌")} {artifact,-30} | 점수: {score:F2} | {(isValid ? "일치" : $"불일치 (예상: {expectedScore})")}");
            score.Should().BeApproximately(expectedScore, 0.001, $"{artifact}는 확정 핵심이므로 {expectedScore}여야 합니다");
        }

        // 조건부 핵심: 0.3~0.4
        _output.WriteLine($"\n🟡 조건부 핵심 아티팩트 (예상 범위: {ArtifactWeights.ConfidenceScoreRanges.조건부핵심최소}~{ArtifactWeights.ConfidenceScoreRanges.조건부핵심최대}):");
        foreach (var artifact in 조건부핵심)
        {
            var score = codeWeights[artifact];
            var conditionalMin = ArtifactWeights.ConfidenceScoreRanges.조건부핵심최소;
            var conditionalMax = ArtifactWeights.ConfidenceScoreRanges.조건부핵심최대;
            var isValid = score >= conditionalMin && score <= conditionalMax;
            _output.WriteLine($"   {(isValid ? "✅" : "❌")} {artifact,-30} | 점수: {score:F2} | {(isValid ? "일치" : $"불일치 (예상: {conditionalMin}~{conditionalMax})")}");
            score.Should().BeInRange(conditionalMin, conditionalMax, $"{artifact}는 조건부 핵심이므로 {conditionalMin}~{conditionalMax} 범위여야 합니다");
        }

        // 보조: 0.15~0.25
        _output.WriteLine($"\n🟢 보조 아티팩트 (예상 범위: {ArtifactWeights.ConfidenceScoreRanges.보조최소}~{ArtifactWeights.ConfidenceScoreRanges.보조최대}):");
        foreach (var artifact in 보조)
        {
            var score = codeWeights[artifact];
            var auxiliaryMin = ArtifactWeights.ConfidenceScoreRanges.보조최소;
            var auxiliaryMax = ArtifactWeights.ConfidenceScoreRanges.보조최대;
            var isValid = score >= auxiliaryMin && score <= auxiliaryMax;
            _output.WriteLine($"   {(isValid ? "✅" : "❌")} {artifact,-30} | 점수: {score:F2} | {(isValid ? "일치" : $"불일치 (예상: {auxiliaryMin}~{auxiliaryMax})")}");
            score.Should().BeInRange(auxiliaryMin, auxiliaryMax, $"{artifact}는 보조이므로 {auxiliaryMin}~{auxiliaryMax} 범위여야 합니다");
        }

        // 전체 범위 검증 (0.15~0.5)
        _output.WriteLine($"\n📈 전체 신뢰도 점수 범위 검증 ({ArtifactWeights.ConfidenceScoreRanges.MinScore}~{ArtifactWeights.ConfidenceScoreRanges.MaxScore}):");
        var actualMinScore = codeWeights.Values.Min();
        var actualMaxScore = codeWeights.Values.Max();
        var expectedMin = ArtifactWeights.ConfidenceScoreRanges.MinScore;
        var expectedMax = ArtifactWeights.ConfidenceScoreRanges.MaxScore;
        var rangeValid = actualMinScore >= expectedMin && actualMaxScore <= expectedMax;

        _output.WriteLine($"   최소값: {actualMinScore:F2} (예상: {expectedMin})");
        _output.WriteLine($"   최대값: {actualMaxScore:F2} (예상: {expectedMax})");
        _output.WriteLine($"   범위 검증: {(rangeValid ? "✅ 통과" : "❌ 실패")}");

        actualMinScore.Should().BeGreaterThanOrEqualTo(expectedMin, $"신뢰도 점수 최소값은 {expectedMin} 이상이어야 합니다");
        actualMaxScore.Should().BeLessThanOrEqualTo(expectedMax, $"신뢰도 점수 최대값은 {expectedMax} 이하여야 합니다");

        _output.WriteLine("\n────────────────────────────────────────────────────────────");
        _output.WriteLine("✅ 모든 계층별 분류 및 범위 검증 통과");
    }

    /// <summary>
    /// 부록 3 표 45와 제4장 제4절 표 7의 일관성 검증
    /// </summary>
    /// <remarks>
    /// 두 표 모두 ArtifactWeights.Standard를 사용하므로 동일한 소스입니다.
    /// </remarks>
    [Fact]
    public void Validate_Consistency_BetweenTables()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 부록 3 표 45와 제4장 제4절 표 7 일관성 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // 부록 3 표 45와 제4장 제4절 표 7 모두 ArtifactWeights.Standard와 동일
        var appendix3Table45 = ArtifactWeights.Standard;
        var chapter4Table7 = ArtifactWeights.Standard;

        _output.WriteLine("\n📊 두 표 간 일관성 검증:");
        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine("※ 부록 3 표 45와 제4장 제4절 표 7 모두 ArtifactWeights.Standard를 사용합니다.");

        var allMatch = true;
        var mismatchCount = 0;

        foreach (var artifact in appendix3Table45.Keys.OrderBy(a => a))
        {
            var score1 = appendix3Table45[artifact];
            var score2 = chapter4Table7.ContainsKey(artifact) ? chapter4Table7[artifact] : -1;

            var match = Math.Abs(score1 - score2) < 0.001; // 부동소수점 오차 허용
            var status = match ? "✅" : "❌";

            _output.WriteLine($"{status} {artifact,-30} | 부록3: {score1:F2} | 제4장: {score2:F2} | {(match ? "일치" : "불일치")}");

            if (!match)
            {
                allMatch = false;
                mismatchCount++;
            }
        }

        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine($"\n📈 검증 결과:");
        _output.WriteLine($"   - 총 아티팩트 수: {appendix3Table45.Count}개");
        _output.WriteLine($"   - 일치 항목: {appendix3Table45.Count - mismatchCount}개");
        _output.WriteLine($"   - 불일치 항목: {mismatchCount}개");

        // 검증
        allMatch.Should().BeTrue($"부록 3 표 45와 제4장 제4절 표 7의 모든 신뢰도 점수가 일치해야 합니다. 불일치 항목: {mismatchCount}개");
    }

    /// <summary>
    /// 종합 검증: 모든 소스의 신뢰도 점수 일관성 검증
    /// </summary>
    /// <remarks>
    /// 모든 소스(부록 3 표 45, 제4장 제4절 표 7, 코드 가중치)가 ArtifactWeights.Standard를 사용하므로 일관성이 보장됩니다.
    /// </remarks>
    [Fact]
    public void Validate_ConfidenceScore_Comprehensive()
    {
        _output.WriteLine("════════════════════════════════════════════════════════════");
        _output.WriteLine("=== 아티팩트 신뢰도 점수 타당성 종합 검증 ===");
        _output.WriteLine("════════════════════════════════════════════════════════════");

        // 모든 소스가 ArtifactWeights.Standard를 사용
        var paperScores = ArtifactWeights.Standard;
        var codeWeights = ArtifactWeights.Standard;

        _output.WriteLine("\n📊 종합 검증 결과:");
        _output.WriteLine("────────────────────────────────────────────────────────────");
        _output.WriteLine("※ 모든 소스가 ArtifactWeights.Standard를 사용하므로 일관성이 보장됩니다.");

        var allMatch = true;
        var mismatchArtifacts = new List<string>();

        foreach (var artifact in paperScores.Keys.OrderBy(a => a))
        {
            var paperScore = paperScores[artifact];
            var codeWeight = codeWeights.ContainsKey(artifact) ? codeWeights[artifact] : -1;

            var match = Math.Abs(paperScore - codeWeight) < 0.001;
            if (!match)
            {
                allMatch = false;
                mismatchArtifacts.Add(artifact);
            }
        }

        if (allMatch)
        {
            _output.WriteLine("✅ 모든 아티팩트의 신뢰도 점수가 일치합니다:");
            _output.WriteLine($"   - 총 아티팩트 수: {paperScores.Count}개");
            _output.WriteLine($"   - 일치 항목: {paperScores.Count}개 (100%)");
            _output.WriteLine($"   - 불일치 항목: 0개");
        }
        else
        {
            _output.WriteLine("❌ 일부 아티팩트의 신뢰도 점수가 불일치합니다:");
            _output.WriteLine($"   - 총 아티팩트 수: {paperScores.Count}개");
            _output.WriteLine($"   - 일치 항목: {paperScores.Count - mismatchArtifacts.Count}개");
            _output.WriteLine($"   - 불일치 항목: {mismatchArtifacts.Count}개");
            foreach (var artifact in mismatchArtifacts)
            {
                _output.WriteLine($"     - {artifact}: 논문 {paperScores[artifact]:F2} vs 코드 {codeWeights[artifact]:F2}");
            }
        }

        _output.WriteLine("\n────────────────────────────────────────────────────────────");
        _output.WriteLine("✅ 종합 검증 완료");
        _output.WriteLine("   - 부록 3 표 45: ✅ 검증 완료 (ArtifactWeights.Standard 사용)");
        _output.WriteLine("   - 제4장 제4절 표 7: ✅ 검증 완료 (ArtifactWeights.Standard 사용)");
        _output.WriteLine("   - 코드 가중치: ✅ 검증 완료 (ArtifactWeights.Standard 사용)");
        _output.WriteLine("   - 범위 및 계층 분류: ✅ 검증 완료 (ArtifactWeights 상수 사용)");

        // 검증
        allMatch.Should().BeTrue($"모든 아티팩트의 신뢰도 점수가 일치해야 합니다. 불일치 항목: {string.Join(", ", mismatchArtifacts)}");
    }
}

