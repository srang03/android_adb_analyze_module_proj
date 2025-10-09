namespace AndroidAdbAnalyzeModule.Core.Models;

/// <summary>
/// 로그 파싱 옵션
/// </summary>
public sealed class LogParsingOptions
{
    /// <summary>
    /// 디바이스 정보
    /// </summary>
    public DeviceInfo DeviceInfo { get; init; } = new();

    /// <summary>
    /// 타임스탬프를 UTC로 변환할지 여부
    /// </summary>
    public bool ConvertToUtc { get; init; } = true;

    /// <summary>
    /// 로그 파일 인코딩 (기본: UTF-8)
    /// </summary>
    public string Encoding { get; init; } = "utf-8";

    /// <summary>
    /// 최대 파일 크기 (MB)
    /// </summary>
    public int MaxFileSizeMB { get; init; } = 500;

    /// <summary>
    /// 파싱할 로그의 시작 시간 (null이면 제한 없음)
    /// 이 시간 이상의 로그만 파싱 (포함)
    /// </summary>
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// 파싱할 로그의 종료 시간 (null이면 제한 없음)
    /// 이 시간 이하의 로그만 파싱 (포함)
    /// </summary>
    public DateTime? EndTime { get; init; }
}

