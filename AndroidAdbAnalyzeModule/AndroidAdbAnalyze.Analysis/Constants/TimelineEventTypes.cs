namespace AndroidAdbAnalyze.Analysis.Constants;

/// <summary>
/// 타임라인 이벤트 타입 상수 정의
/// TimelineItem의 EventType에 사용되는 문자열을 중앙에서 관리합니다.
/// </summary>
public static class TimelineEventTypes
{
    /// <summary>
    /// 카메라 세션 타임라인 아이템
    /// </summary>
    public const string CAMERA_SESSION = "CameraSession";
    
    /// <summary>
    /// 카메라 촬영 타임라인 아이템
    /// </summary>
    public const string CAMERA_CAPTURE = "CameraCapture";
    
    /// <summary>
    /// 네트워크 전송 타임라인 아이템
    /// </summary>
    /// <remarks>
    /// 촬영 후 네트워크를 통해 전송이 발생한 이벤트를 나타냅니다.
    /// sem_wifi 로그 분석을 통해 탐지됩니다.
    /// </remarks>
    public const string TRANSMISSION = "Transmission";
}
