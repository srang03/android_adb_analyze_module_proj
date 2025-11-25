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
    /// 설정 근거: 0.5초 (500ms)
    /// 
    /// 목적: 동일 촬영의 여러 핵심 아티팩트를 1개로 통합
    /// 
    /// 실측 근거 (예비 실험):
    /// - 측정 대상: 촬영 시점(±100ms)에 발생하는 핵심 아티팩트
    ///   - 포함: DATABASE_INSERT, SILENT_CAMERA_CAPTURE, VIBRATION_EVENT, PLAYER_EVENT, URI_PERMISSION_GRANT
    ///   - 제외: FOREGROUND_SERVICE (1초 단위 타임스탬프, 2~3초 지연), MEDIA_EXTRACTOR (촬영 후 처리, 최대 5초 지연),
    ///           PLAYER_CREATED/RELEASED (앱 실행/종료 시점, 촬영과 시간적 분리)
    /// - 측정 결과: 최대 330ms (기본 카메라, VIBRATION_EVENT 간격)
    /// - 안전 마진: 1.52배 (500ms / 330ms)
    /// - 다른 파라미터와의 일관성: SameCameraUsageTimeThreshold (2.0배)와 유사한 수준
    /// 
    /// 예시:
    /// - VIBRATION_EVENT (10:00:05.000)
    /// - PLAYER_EVENT (10:00:05.123, +123ms)
    /// - DATABASE_INSERT (10:00:05.330, +330ms)
    /// → 500ms 내 발생, 1개로 통합 ✅
    /// 
    /// Ground Truth 검증:
    /// - 본 실험 46개 촬영: Precision 100%, Recall 100%
    /// - 중복 탐지 0건, 오탐 0건
    /// 
    /// 주의:
    /// - EventCorrelationWindow(30초)와 혼동 금지
    /// - EventCorrelationWindow: 보조 아티팩트 수집 범위
    /// - CaptureDeduplicationWindow: 촬영 중복 제거 범위
    /// 
    /// 참고: 제4장 제4절 (촬영 탐지 설계), 부록 3 (예비 실험 상세)
    /// </remarks>
    public TimeSpan CaptureDeduplicationWindow { get; init; } = TimeSpan.FromMilliseconds(500);
    
    /// <summary>
    /// 같은 카메라 사용 판정 시간 임계값 (세션 병합 규칙 1)
    /// </summary>
    /// <remarks>
    /// 설정 근거: 2.0초
    /// 
    /// 목적: usagestats와 media.camera의 같은 카메라 사용 세션 병합 (병합 규칙 1)
    /// 
    /// 실측 근거 (예비 실험):
    /// - usagestats-media.camera 간 시작 시각 차이: 평균 0.62초 (최소 0.31초, 최대 0.85초)
    /// - usagestats-media.camera 간 종료 시각 차이: 평균 0.25초 (최소 0.00초, 최대 1.00초)
    /// - 안전 마진 2배 적용 (종료 차이 최대값 1.00초 기준) → 2.0초
    /// 
    /// 병합 조건 (4가지 모두 만족):
    /// 1. 서로 다른 로그 소스 쌍 (usagestats ↔ media.camera)
    /// 2. 패키지명 일치
    /// 3. 시작 시각 차이 ≤ SameCameraUsageTimeThreshold
    /// 4. 종료 시각 차이 ≤ SameCameraUsageTimeThreshold
    /// 
    /// 설계 의도:
    /// - usagestats는 앱 생명주기 (ACTIVITY_RESUMED/PAUSED)
    /// - media.camera는 하드웨어 연결 (CONNECT/DISCONNECT)
    /// - 같은 카메라 사용이지만 로그 계층이 달라 약 1초 시각 차이 발생
    /// 
    /// 참고: 제4장 제3절 (세션 탐지 설계), 부록 3 (예비 실험 상세)
    /// </remarks>
    public TimeSpan SameCameraUsageTimeThreshold { get; init; } = TimeSpan.FromSeconds(2);
    
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
    /// 설정 근거: 0.55 (55%)
    /// 
    /// 1. 실측 검증 (예비 실험 3회):
    ///    - 중복 이벤트 쌍: 평균 64%, 최소 60%, 최대 71%
    ///    - 비중복 이벤트 쌍: 평균 45%, 최대 65% (부록 3 기준)
    ///    → 55%를 경계로 두 분포 구분 (안전 마진 확보)
    /// 
    /// 2. 임계값 설정 논리:
    ///    - 중복 쌍 최소값(60%)보다 낮아야 모든 중복 탐지 가능
    ///    - 비중복 쌍 최대값(65%)보다 낮아야 오탐 방지
    ///    - 55% 선정: 두 분포 사이의 안전한 경계값
    ///    - 안전 마진: (0.55 - 0.45) / (0.60 - 0.45) = 66.7%
    /// 
    /// 3. 실측 데이터 상세 (예비 실험):
    ///    - STANDBY_BUCKET_CHANGED: 60.0%
    ///    - ACTIVITY_RESUMED: 62.5%
    ///    - ACTIVITY_STOPPED: 62.5%
    ///    - VIBRATION_EVENT: 64.3%
    ///    - AUDIO_TRACK: 71.4%
    /// 
    /// 4. Jaccard Similarity 정의:
    ///    J(A,B) = |A ∩ B| / |A ∪ B|
    ///    - 교집합(같은 키-값 쌍) / 합집합(모든 고유 키)
    ///    - 0.55 = 55% 이상의 속성이 일치하면 중복으로 판정
    /// 
    /// 5. 정보 보존 원칙:
    ///    - 55% 유사도 보장으로 핵심 속성 손실 방지
    ///    - 중복 판정 시 속성 개수가 많은 이벤트를 대표로 선정하여 정보 최대 보존
    /// 
    /// 참고:
    /// - TimeBasedDeduplicationStrategy.cs에서 사용
    /// - EventDeduplicator.cs의 중복 제거 프로세스에서 적용
    /// - 부록 3 (예비 실험 상세), 제5장 제3절 (파라미터 타당성 검증)
    /// </remarks>
    public double DeduplicationSimilarityThreshold { get; init; } = 0.55;
    
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
