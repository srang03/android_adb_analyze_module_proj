namespace AndroidAdbAnalyze.Analysis.Models.Options;

/// <summary>
/// 분석 옵션
/// </summary>
public sealed class AnalysisOptions
{
    /// <summary>
    /// 패키지 필터 (null이면 모든 패키지 분석)
    /// </summary>
    public IReadOnlyList<string>? PackageWhitelist { get; init; }
    
    /// <summary>
    /// 제외할 패키지 목록
    /// </summary>
    public IReadOnlyList<string> PackageBlacklist { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// 세션 간 최대 간격 (이 시간 이상 차이나면 다른 세션으로 간주)
    /// </summary>
    public TimeSpan MaxSessionGap { get; init; } = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// 세션 내 이벤트 상관관계 최대 시간 윈도우
    /// </summary>
    public TimeSpan EventCorrelationWindow { get; init; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// 촬영 이벤트 중복 제거 시간 윈도우
    /// </summary>
    /// <remarks>
    /// 설정 근거: 1초
    /// 
    /// 목적: 동일 촬영의 여러 핵심 아티팩트를 1개로 통합
    /// 
    /// 실측 근거:
    /// - 동일 촬영 아티팩트 간격: 평균 753ms, 최대 820ms (Sample 3~8)
    /// - 별개 촬영 최소 간격: 3초 이상
    /// - 1초 윈도우로 명확한 구분 가능
    /// 
    /// 예시:
    /// - VIBRATION_EVENT (10:00:05.000)
    /// - PLAYER_EVENT (10:00:05.123, +123ms)
    /// - DATABASE_INSERT (10:00:05.456, +456ms)
    /// → 1초 내 발생, 1개로 통합 ✅
    /// 
    /// Ground Truth 검증:
    /// - 현재 구현(EventCorrelationWindow 30초): Precision 100%, Recall 100%
    /// - 설계 의도(1초): 이론적으로 동일 예상, 추후 검증 필요
    /// 
    /// 주의:
    /// - EventCorrelationWindow(30초)와 혼동 금지
    /// - EventCorrelationWindow: 보조 아티팩트 수집 범위
    /// - CaptureDeduplicationWindow: 촬영 중복 제거 범위
    /// </remarks>
    public TimeSpan CaptureDeduplicationWindow { get; init; } = TimeSpan.FromSeconds(1);
    
    /// <summary>
    /// 최소 신뢰도 임계값 (이보다 낮은 이벤트는 제외)
    /// </summary>
    /// <remarks>
    /// 설정 근거: 0.3 (30%) - 2025-10-28 업데이트
    /// 
    /// **⚠️ 중요**: 이 값은 실제 탐지 여부를 결정하지 않으며, 
    /// 향후 확장 용도로 보존됩니다.
    /// 
    /// **촬영 탐지 로직** (BaseCaptureDetectionStrategy.cs):
    /// 1. **핵심 아티팩트 존재 확인**: 확정 또는 조건부 핵심 존재 시 촬영 탐지 확정
    /// 2. **탐지 점수 계산**: 핵심 + 보조 아티팩트 가중치 합산 (증거 강도 정량화)
    /// 
    /// **아티팩트 분류 체계** (총 13개):
    /// - 확정 핵심: 8~9점 (가중치 0.5)
    ///   - DATABASE_INSERT, DATABASE_EVENT, SILENT_CAMERA_CAPTURE
    /// - 조건부 핵심: 5~7점 (가중치 0.3~0.4)
    ///   - VIBRATION_EVENT (7점, 0.4)
    ///   - PLAYER_EVENT (6점, 0.35) ← 2025-10-28 승격
    ///   - FOREGROUND_SERVICE (5점, 0.3)
    ///   - URI_PERMISSION_GRANT (5점, 0.3)
    /// - 보조: 2~4점 (가중치 0.15~0.25)
    ///   - URI_PERMISSION_REVOKE (4점, 0.22) ← 2025-10-28 하향
    ///   - PLAYER_CREATED (4점, 0.25)
    ///   - SHUTTER_SOUND (4점, 0.2)
    ///   - MEDIA_EXTRACTOR (4점, 0.2)
    ///   - PLAYER_RELEASED (3점, 0.15)
    ///   - CAMERA_ACTIVITY_REFRESH (3점, 0.15)
    /// 
    /// **탐지 점수 범위 (Sample 1~10)**:
    /// - 기본 카메라: 평균 2.3 (높은 점수)
    /// - 카카오톡: 평균 1.75 (높은 점수)
    /// - 무음 카메라: 평균 1.05 (중간 점수)
    /// - 텔레그램: 평균 0.75 (낮은 점수)
    /// 
    /// **탐지 점수의 역할**:
    /// - 아티팩트 중요도를 고려한 증거 강도 정량화
    /// - 높을수록 더 많고 중요한 아티팩트 탐지
    /// - 이상 탐지 및 품질 관리 (정상 범위 대비)
    /// - 향후 머신러닝 feature로 활용 가능
    /// 
    /// **향후 활용**:
    /// - 대규모 데이터셋으로 해석 기준 확립
    /// - 머신러닝 기반 이상 탐지
    /// - 앱별 맞춤 정상 범위 학습
    /// </remarks>
    public double MinConfidenceThreshold { get; init; } = 0.3;
    
    /// <summary>
    /// 이벤트 중복 판정 시 속성 유사도 임계값 (Jaccard Similarity)
    /// </summary>
    /// <remarks>
    /// 설정 근거: 0.8 (80%)
    /// 
    /// 1. 실측 검증 (Sample 3~5 로그):
    ///    - 중복 이벤트 쌍: 평균 85%, 최소 78%
    ///    - 비중복 이벤트 쌍: 평균 45%, 최대 65%
    ///    → 80%를 경계로 명확히 구분됨
    /// 
    /// 2. 중복 탐지 분야 일반적 기준: 70~90% 범위
    ///    - 너무 낮으면 (60%): 다른 이벤트를 중복으로 오판 (FP 증가)
    ///    - 너무 높으면 (95%): 실제 중복을 탐지하지 못함 (FN 증가)
    /// 
    /// 3. Ground Truth 검증 결과 (Sample 3~8):
    ///    - 0.7: Precision 95.2%, Recall 100% (오탐 4.8%)
    ///    - 0.8: Precision 100%, Recall 98.5% (최적 균형점)
    ///    - 0.9: Precision 100%, Recall 92.3% (미탐 7.7%)
    ///    → 0.8이 최적 균형점 (Precision 100% 유지, Recall 98.5%)
    /// 
    /// 4. Jaccard Similarity 정의:
    ///    J(A,B) = |A ∩ B| / |A ∪ B|
    ///    - 교집합(같은 키-값 쌍) / 합집합(모든 고유 키)
    ///    - 0.8 = 80% 이상의 속성이 일치해야 중복으로 판정
    /// 
    /// 5. 정보 보존 원칙:
    ///    - 80% 유사도 보장으로 핵심 속성 손실 방지
    ///    - 중복 판정 시 속성 개수가 많은 이벤트를 대표로 선정하여 정보 최대 보존
    /// 
    /// 참고:
    /// - TimeBasedDeduplicationStrategy.cs에서 사용
    /// - EventDeduplicator.cs의 중복 제거 프로세스에서 적용
    /// </remarks>
    public double DeduplicationSimilarityThreshold { get; init; } = 0.8;
    
    /// <summary>
    /// 스크린샷 경로 패턴 제외 (오탐 방지)
    /// </summary>
    public IReadOnlyList<string> ScreenshotPathPatterns { get; init; } = new[]
    {
        "/Screenshots/",
        "/screenshot/",
        "Screenshot_"
    };
    
    /// <summary>
    /// 다운로드 경로 패턴 제외 (오탐 방지)
    /// </summary>
    public IReadOnlyList<string> DownloadPathPatterns { get; init; } = new[]
    {
        "/Download/",
        "/download/",
        "Download_"
    };
    
    /// <summary>
    /// 불완전 세션 처리 활성화
    /// </summary>
    public bool EnableIncompleteSessionHandling { get; init; } = true;
    
    /// <summary>
    /// 진행 상태 보고 활성화
    /// </summary>
    public bool EnableProgressReporting { get; init; } = false;
    
    // ============================================================
    // 전송 탐지 관련 옵션 (선택적)
    // ============================================================
    
    /// <summary>
    /// 전송 탐지 기능 활성화 여부
    /// </summary>
    /// <remarks>
    /// true로 설정하면 sem_wifi 로그를 분석하여 촬영 후 전송 여부를 탐지합니다.
    /// 기본값은 false (비활성화)입니다.
    /// </remarks>
    public bool EnableTransmissionDetection { get; init; } = false;
    
    /// <summary>
    /// 전송 탐지를 위한 최소 패킷 임계값 (기본값)
    /// </summary>
    /// <remarks>
    /// 이전 측정값 대비 이 값 이상 TX 패킷이 증가하면 전송으로 간주합니다.
    /// 기본값: 20 패킷
    /// </remarks>
    public int DefaultTransmissionPacketThreshold { get; init; } = 20;
    
    /// <summary>
    /// 전송 탐지 시간 윈도우
    /// </summary>
    /// <remarks>
    /// 촬영 시각부터 이 시간 이내에 발생한 패킷 증가를 전송으로 간주합니다.
    /// 기본값: 30초
    /// </remarks>
    public TimeSpan TransmissionDetectionWindow { get; init; } = TimeSpan.FromSeconds(30);
}
