namespace AndroidAdbAnalyze.Analysis.Models.Context;

/// <summary>
/// Foreground Service 정보
/// </summary>
/// <remarks>
/// usagestats.log의 FOREGROUND_SERVICE_START/STOP 이벤트에서 추출됩니다.
/// 실제 촬영 처리 시점을 식별하는 데 사용됩니다.
/// </remarks>
public sealed class ForegroundServiceInfo
{
    /// <summary>
    /// 서비스 클래스명 (예: com.samsung.android.camera.core2.processor.PostProcessService)
    /// </summary>
    public string ServiceClass { get; init; } = string.Empty;
    
    /// <summary>
    /// 서비스 시작 시간
    /// </summary>
    public DateTime StartTime { get; init; }
    
    /// <summary>
    /// 서비스 종료 시간 (null이면 아직 실행 중)
    /// </summary>
    public DateTime? StopTime { get; init; }
    
    /// <summary>
    /// 서비스 지속 시간
    /// </summary>
    public TimeSpan Duration => 
        (StopTime ?? DateTime.MaxValue) - StartTime;
}
