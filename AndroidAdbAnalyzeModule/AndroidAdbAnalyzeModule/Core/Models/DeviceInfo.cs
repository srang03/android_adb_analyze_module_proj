namespace AndroidAdbAnalyzeModule.Core.Models;

/// <summary>
/// 안드로이드 디바이스 정보
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>
    /// 디바이스 TimeZone (예: "Asia/Seoul", "America/New_York")
    /// </summary>
    public string TimeZone { get; init; } = "Asia/Seoul";

    /// <summary>
    /// 디바이스 현재 시간
    /// </summary>
    public DateTime CurrentTime { get; init; } = DateTime.Now;

    /// <summary>
    /// 안드로이드 버전 (예: "15", "14")
    /// </summary>
    public string? AndroidVersion { get; init; }

    /// <summary>
    /// 제조사 (예: "samsung", "google", "xiaomi")
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// 디바이스 모델명 (선택사항)
    /// </summary>
    public string? Model { get; init; }
}

