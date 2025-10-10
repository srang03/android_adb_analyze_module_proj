namespace AndroidAdbAnalyze.Parser.Core.Models;

/// <summary>
/// 로그 파싱 프로세스를 제어하는 옵션을 제공합니다.
/// 이 클래스는 불변(immutable)으로 설계되었습니다.
/// </summary>
public sealed class LogParsingOptions
{
    /// <summary>
    /// 타임스탬프 정규화 및 버전 호환성 검증에 사용될 디바이스 정보입니다.
    /// <see cref="Models.DeviceInfo"/>를 참조하세요.
    /// </summary>
    public DeviceInfo DeviceInfo { get; init; } = new();

    /// <summary>
    /// 파싱할 최대 로그 파일 크기 (MB). 이 크기를 초과하면 LogFileTooLargeException이 발생합니다.
    /// 기본값은 50MB입니다.
    /// </summary>
    public int MaxFileSizeMB { get; init; } = 50;

    /// <summary>
    /// 정규화된 이벤트의 타임스탬프를 UTC로 변환할지 여부를 결정합니다.
    /// 기본값은 true입니다.
    /// </summary>
    public bool ConvertToUtc { get; init; } = true;

    /// <summary>
    /// 로그 파일을 읽을 때 사용할 인코딩의 이름입니다 (예: "utf-8", "utf-16").
    /// 기본값은 "utf-8"입니다.
    /// </summary>
    public string Encoding { get; init; } = "utf-8";

    /// <summary>
    /// 파싱할 이벤트의 시간 범위 필터링을 위한 시작 시간(포함)입니다.
    /// null인 경우 시간 제한 없이 가장 오래된 로그부터 파싱을 시작합니다.
    /// </summary>
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// 파싱할 이벤트의 시간 범위 필터링을 위한 종료 시간(포함)입니다.
    /// null인 경우 시간 제한 없이 가장 최신 로그까지 파싱합니다.
    /// </summary>
    public DateTime? EndTime { get; init; }
}

