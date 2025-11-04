namespace AndroidAdbAnalyze.Analysis.Models.Transmission;

/// <summary>
/// 전송 탐지 결과
/// </summary>
/// <remarks>
/// 카메라 촬영 이벤트 이후 네트워크 전송 발생 여부를 나타냅니다.
/// sem_wifi 로그 분석을 통해 생성됩니다.
/// </remarks>
public sealed class TransmissionResult
{
    /// <summary>
    /// 전송이 탐지되었는지 여부
    /// </summary>
    public bool IsTransmitted { get; init; }
    
    /// <summary>
    /// 전송이 탐지된 시간 (UTC)
    /// </summary>
    /// <remarks>
    /// IsTransmitted가 true일 때만 값이 있습니다.
    /// </remarks>
    public DateTime? TransmissionTime { get; init; }
    
    /// <summary>
    /// 전송된 패킷 수 (델타 값)
    /// </summary>
    /// <remarks>
    /// 이전 측정값 대비 증가한 TX 패킷 수입니다.
    /// IsTransmitted가 true일 때만 값이 있습니다.
    /// </remarks>
    public int? TransmittedPackets { get; init; }
    
    /// <summary>
    /// 전송 탐지 방법
    /// </summary>
    /// <remarks>
    /// 예: "WiFi", "Mobile", "Unknown"
    /// 현재는 WiFi만 지원합니다.
    /// </remarks>
    public string? DetectionMethod { get; init; }
    
    /// <summary>
    /// 전송 탐지에 사용된 UID
    /// </summary>
    /// <remarks>
    /// sem_wifi 로그의 UID 필드 값입니다.
    /// </remarks>
    public int? DetectedUid { get; init; }
    
    /// <summary>
    /// 빈 결과 (전송 탐지 실패)
    /// </summary>
    public static TransmissionResult Empty => new() { IsTransmitted = false };
}

