namespace AndroidAdbAnalyzeModule.Core.Models;

/// <summary>
/// 정규화된 로그 이벤트
/// 하나의 로그 파일에서 여러 타입의 이벤트가 추출될 수 있음
/// </summary>
public sealed class NormalizedLogEvent
{
    /// <summary>
    /// 이벤트 고유 ID
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// 정규화된 타임스탬프 (UTC 또는 로컬)
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// 이벤트 타입 (예: "CAMERA_CAPTURE", "AUDIO_FOCUS", "RECORDING")
    /// 하나의 로그 파일에서 여러 타입의 이벤트 추출 가능
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// 이벤트가 추출된 로그 섹션 ID
    /// </summary>
    public string SourceSection { get; init; } = string.Empty;

    /// <summary>
    /// 이벤트 발생 패키지명 (선택사항)
    /// Attributes["package"]에서 자동 추출되며, 패키지별 필터링/분석에 사용됩니다.
    /// </summary>
    public string? PackageName { get; init; }

    /// <summary>
    /// 이벤트 속성 (동적 필드)
    /// </summary>
    public IReadOnlyDictionary<string, object> Attributes { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    /// 원본 로그 라인 (디버깅용)
    /// </summary>
    public string? RawLine { get; init; }

    /// <summary>
    /// 로그 파일명 (선택사항)
    /// </summary>
    public string? SourceFileName { get; init; }
    
    /// <summary>
    /// 디바이스 정보 (파싱 시 설정됨)
    /// </summary>
    public DeviceInfo DeviceInfo { get; internal set; }
}

