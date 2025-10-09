namespace AndroidAdbAnalyze.Analysis.Models.Options;

/// <summary>
/// 분석 옵션
/// </summary>
public sealed class AnalysisOptions
{
    /// <summary>
    /// 패키지 필터 (null이면 모든 패키지 분석)
    /// </summary>
    public IReadOnlyList<string>? PackageWhitelist { get; init; }
    
    /// <summary>
    /// 제외할 패키지 목록
    /// </summary>
    public IReadOnlyList<string> PackageBlacklist { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// 세션 간 최대 간격 (이 시간 이상 차이나면 다른 세션으로 간주)
    /// </summary>
    public TimeSpan MaxSessionGap { get; init; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// 세션 내 이벤트 상관관계 최대 시간 윈도우
    /// </summary>
    public TimeSpan EventCorrelationWindow { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 최소 신뢰도 임계값 (이보다 낮은 이벤트는 제외)
    /// </summary>
    public double MinConfidenceThreshold { get; init; } = 0.5;
    
    /// <summary>
    /// 스크린샷 경로 패턴 제외 (오탐 방지)
    /// </summary>
    public IReadOnlyList<string> ScreenshotPathPatterns { get; init; } = new[]
    {
        "/Screenshots/",
        "/screenshot/",
        "Screenshot_"
    };
    
    /// <summary>
    /// 다운로드 경로 패턴 제외 (오탐 방지)
    /// </summary>
    public IReadOnlyList<string> DownloadPathPatterns { get; init; } = new[]
    {
        "/Download/",
        "/download/",
        "Download_"
    };
    
    /// <summary>
    /// 불완전 세션 처리 활성화
    /// </summary>
    public bool EnableIncompleteSessionHandling { get; init; } = true;
    
    /// <summary>
    /// 진행 상태 보고 활성화
    /// </summary>
    public bool EnableProgressReporting { get; init; } = false;
}
