using AndroidAdbAnalyze.Analysis.Models.Results;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 보고서 생성 서비스 인터페이스
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// 분석 결과를 기반으로 보고서를 생성합니다.
    /// </summary>
    /// <param name="result">분석 결과</param>
    /// <returns>생성된 보고서 (형식은 구현체에 따라 다름)</returns>
    string GenerateReport(AnalysisResult result);
    
    /// <summary>
    /// 보고서 형식을 반환합니다.
    /// </summary>
    string Format { get; }
}
