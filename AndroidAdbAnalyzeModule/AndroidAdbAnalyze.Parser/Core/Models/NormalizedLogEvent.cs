namespace AndroidAdbAnalyze.Parser.Core.Models;

/// <summary>
/// 다양한 ADB 로그 소스에서 파싱되고 표준화된 단일 이벤트를 나타냅니다.
/// 이 클래스는 불변(immutable)으로 설계되었습니다.
/// </summary>
public sealed class NormalizedLogEvent
{
    /// <summary>
    /// 이벤트의 고유 식별자입니다.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// 이벤트가 발생한 정규화된 타임스탬프입니다.
    /// 파싱 옵션에 따라 UTC 또는 로컬 시간일 수 있습니다.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// YAML 설정 파일에 정의된 이벤트 유형입니다. (예: "CAMERA_CONNECT", "AUDIO_PLAYER_CREATED")
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// 이벤트가 추출된 로그 파일의 섹션 ID입니다.
    /// `log_config.yaml`의 `sections`에 정의된 ID와 일치합니다.
    /// </summary>
    public string SourceSection { get; init; } = string.Empty;

    /// <summary>
    /// 이벤트와 관련된 애플리케이션 패키지 이름입니다. (선택 사항)
    /// `Attributes` 컬렉션에 "package" 키가 있는 경우 자동으로 채워집니다.
    /// </summary>
    public string? PackageName { get; init; }

    /// <summary>
    /// 로그 라인에서 추출된 동적 속성들의 읽기 전용 사전입니다.
    /// 키는 `log_config.yaml`에 정의된 필드 이름입니다.
    /// </summary>
    public IReadOnlyDictionary<string, object> Attributes { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// 이 이벤트가 파생된 원본 로그 라인입니다. (디버깅 목적으로 사용)
    /// </summary>
    public string? RawLine { get; init; }

    /// <summary>
    /// 이벤트가 파생된 원본 로그 파일의 이름입니다. (선택 사항)
    /// </summary>
    public string? SourceFileName { get; init; }
    
    /// <summary>
    /// 파싱 시점의 디바이스 정보입니다.
    /// 이 속성은 파서 내부에서 설정됩니다.
    /// </summary>
    public DeviceInfo DeviceInfo { get; internal set; } = null!;
}

