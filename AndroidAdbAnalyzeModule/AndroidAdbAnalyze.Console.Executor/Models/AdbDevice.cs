namespace AndroidAdbAnalyze.Console.Executor.Models;

/// <summary>
/// ADB로 연결된 디바이스 정보
/// </summary>
public sealed record AdbDevice
{
    /// <summary>
    /// 디바이스 시리얼 번호 또는 네트워크 주소 (예: "1234567890", "192.168.0.100:5555")
    /// </summary>
    public required string Serial { get; init; }
    
    /// <summary>
    /// 디바이스 연결 상태 (예: "device", "offline", "unauthorized")
    /// </summary>
    public required string State { get; init; }
    
    /// <summary>
    /// 디바이스 모델명 (선택적, getprop으로 추출)
    /// </summary>
    public string? Model { get; init; }
    
    /// <summary>
    /// 연결 타입 (USB 또는 WiFi)
    /// </summary>
    public ConnectionType ConnectionType { get; init; }
    
    /// <summary>
    /// 연결 타입 판별 (시리얼 번호에 IP 주소 포함 여부)
    /// </summary>
    /// <returns>연결 타입</returns>
    public static ConnectionType DetermineConnectionType(string serial)
    {
        // IP:PORT 형식이면 WiFi (예: "192.168.0.100:5555")
        return serial.Contains(':') && serial.Contains('.') 
            ? ConnectionType.WiFi 
            : ConnectionType.Usb;
    }
    
    /// <summary>
    /// 디바이스가 사용 가능한 상태인지 확인
    /// </summary>
    public bool IsAvailable => State.Equals("device", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 디바이스 연결 타입
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// USB 연결
    /// </summary>
    Usb,
    
    /// <summary>
    /// WiFi 무선 디버깅 연결
    /// </summary>
    WiFi
}

