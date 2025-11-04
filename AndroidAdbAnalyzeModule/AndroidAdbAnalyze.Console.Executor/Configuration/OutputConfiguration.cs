namespace AndroidAdbAnalyze.Console.Executor.Configuration;

/// <summary>
/// 출력 설정 (appsettings.json의 "Output" 섹션)
/// </summary>
public sealed class OutputConfiguration
{
    /// <summary>
    /// HTML 보고서 생성 여부
    /// </summary>
    public bool GenerateHtmlReport { get; set; } = true;
    
    /// <summary>
    /// 파싱된 이벤트 저장 여부 (JSON)
    /// </summary>
    public bool SaveParsedEvents { get; set; } = true;
    
    /// <summary>
    /// 분석 결과 저장 여부 (JSON)
    /// </summary>
    public bool SaveAnalysisResult { get; set; } = true;
}

