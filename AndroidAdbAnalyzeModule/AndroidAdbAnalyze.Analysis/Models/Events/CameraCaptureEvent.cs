namespace AndroidAdbAnalyze.Analysis.Models.Events;

/// <summary>
/// 카메라 촬영 이벤트
/// </summary>
public sealed class CameraCaptureEvent
{
    /// <summary>
    /// 촬영 이벤트 고유 ID
    /// </summary>
    public Guid CaptureId { get; init; }
    
    /// <summary>
    /// 소속 세션 ID (연결용, 순환 참조 방지)
    /// </summary>
    public Guid ParentSessionId { get; init; }
    
    /// <summary>
    /// 촬영 시각 (UTC)
    /// </summary>
    public DateTime CaptureTime { get; init; }
    
    /// <summary>
    /// 패키지명
    /// </summary>
    public string PackageName { get; init; } = string.Empty;
    
    /// <summary>
    /// 이미지 파일 경로 (있는 경우)
    /// </summary>
    public string? FilePath { get; init; }
    
    /// <summary>
    /// 파일 URI (있는 경우)
    /// </summary>
    public string? FileUri { get; init; }
    
    /// <summary>
    /// 핵심 아티팩트 이벤트 ID (Key Artifact, 예: DATABASE_INSERT)
    /// </summary>
    /// <remarks>
    /// 학술 논문에서는 "핵심 아티팩트 (Key Artifact)"로 표현.
    /// 변수명은 레거시 코드와의 호환성을 위해 decisiveArtifact 유지.
    /// </remarks>
    public Guid? decisiveArtifact { get; init; }
    
    /// <summary>
    /// 보조 아티팩트 이벤트 ID 목록 (Supporting Artifact, 예: SHUTTER_SOUND, VIBRATION_EVENT)
    /// </summary>
    public IReadOnlyList<Guid> SupportingArtifactIds { get; init; } = Array.Empty<Guid>();
    
    /// <summary>
    /// 직접 증거 없이 추정된 촬영인지 여부
    /// </summary>
    public bool IsEstimated { get; init; }
    
    /// <summary>
    /// 촬영 탐지 점수 (Capture Detection Score, 0.0 ~ 1.0)
    /// </summary>
    /// <remarks>
    /// 촬영 증거 이벤트들의 가중치 합으로 계산됨.
    /// - DATABASE_INSERT(0.5), VIBRATION_EVENT(0.4), PLAYER_EVENT(0.35) 등
    /// 학술 논문에서는 "촬영 탐지 점수 (Capture Detection Score)"로 표현.
    /// 
    /// 주의: CameraSession.CaptureDetectionScore(세션 완전성 점수)와 다른 개념!
    /// </remarks>
    public double CaptureDetectionScore { get; init; }
    
    /// <summary>
    /// 증거 타입 목록 (예: ["DATABASE_INSERT", "SHUTTER_SOUND"])
    /// </summary>
    public IReadOnlyList<string> ArtifactTypes { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// 이 촬영 이벤트의 근거가 되는 원본 이벤트 ID 목록
    /// </summary>
    public IReadOnlyList<Guid> SourceEventIds { get; init; } = Array.Empty<Guid>();
    
    /// <summary>
    /// 추가 메타데이터 (필요시 확장 가능)
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = 
        new Dictionary<string, string>();
    
    // ============================================================
    // 전송 탐지 관련 필드 (선택적)
    // ============================================================
    
    /// <summary>
    /// 촬영 후 네트워크 전송이 탐지되었는지 여부
    /// </summary>
    /// <remarks>
    /// sem_wifi 로그 분석을 통해 설정됩니다.
    /// 전송 탐지 기능이 비활성화된 경우 기본값 false입니다.
    /// </remarks>
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
}
