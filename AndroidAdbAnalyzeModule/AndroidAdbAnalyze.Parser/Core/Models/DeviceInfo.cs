namespace AndroidAdbAnalyze.Parser.Core.Models;

/// <summary>
/// 로그가 생성된 안드로이드 디바이스의 컨텍스트 정보를 제공합니다.
/// 이 정보는 타임스탬프 정규화 및 설정 파일의 버전 호환성 검증에 사용됩니다.
/// 이 클래스는 불변(immutable)으로 설계되었습니다.
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>
    /// 디바이스의 IANA 시간대 ID입니다. (예: "Asia/Seoul", "America/New_York")
    /// 연도가 없는 타임스탬프의 날짜를 유추하고 UTC로 변환하는 데 사용됩니다.
    /// 기본값은 "Asia/Seoul"입니다.
    /// </summary>
    public string TimeZone { get; init; } = "Asia/Seoul";

    /// <summary>
    /// 로그 수집 시점의 디바이스 현재 시간입니다.
    /// 연도가 없는 타임스탬프의 연도를 유추하는 데 사용됩니다.
    /// 기본값은 <see cref="DateTime.Now"/>입니다.
    /// </summary>
    public DateTime CurrentTime { get; init; } = DateTime.Now;

    /// <summary>
    /// 디바이스의 안드로이드 OS 버전입니다. (예: "15", "14")
    /// `log_config.yaml`의 `supportedVersions`와 비교하여 호환성을 검증하는 데 사용됩니다.
    /// </summary>
    public string? AndroidVersion { get; init; }

    /// <summary>
    /// 디바이스 제조사입니다. (예: "samsung", "google") (선택 사항)
    /// 향후 제조사별 파싱 규칙을 적용하는 데 사용될 수 있습니다.
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// 디바이스 모델명입니다. (선택 사항)
    /// 향후 모델별 파싱 규칙을 적용하는 데 사용될 수 있습니다.
    /// </summary>
    public string? Model { get; init; }
}

