using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Configuration;
using Microsoft.Extensions.Logging;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Services.Confidence;

/// <summary>
/// 촬영 탐지 점수 계산 서비스 구현
/// </summary>
/// <remarks>
/// 학술 논문에서는 "탐지 점수 (Detection Score)"로 표현.
/// 아티팩트 가중치의 합산 방식으로 계산.
/// 
/// **⚠️ 주의**: 탐지 점수는 촬영 여부를 결정하지 않으며, 증거 강도 정량화 지표로 사용됩니다.
/// - 촬영 탐지: 핵심 아티팩트 존재 여부로 판단 (BaseCaptureDetectionStrategy.cs)
/// - 탐지 점수: 아티팩트 중요도를 고려한 증거 강도 정량화
/// 
/// **탐지 점수의 4가지 역할**:
/// 1. 아티팩트 중요도의 정량화 (가중치 반영)
/// 2. 증거 강도의 단일 지표 (빠른 비교)
/// 3. 이상 탐지 및 품질 관리 (정상 범위 대비)
/// 4. 향후 확장성 (머신러닝, 통계 분석)
/// </remarks>
public sealed class ConfidenceCalculator : IConfidenceCalculator
{
    private readonly ILogger<ConfidenceCalculator> _logger;
    private readonly Dictionary<string, double> _eventTypeWeights;
    
    /// <summary>
    /// 이벤트 타입별 가중치 (0.0 ~ 1.0)
    /// </summary>
    /// <remarks>
    /// ⚠️ 주의: 이 필드는 Configuration 기반으로 동적 생성됩니다.
    /// 기존 하드코딩된 값들은 ConfigurationProvider.GetDefault()에서 제공됩니다.
    /// 
    /// 원본 주석 (참고용):
    /// 가중치 결정 기준 (3가지 평가 축):
    /// 1. 직접성 (Directness): 촬영 행위와의 인과관계 강도 (0~100%)
    ///    - 100%: 파일 저장 확정 (촬영 완료의 결정적 증거)
    ///    - 80%: HAL 레벨 하드웨어 동작
    ///    - 50%: 앱 레벨 사용자 행위
    ///    - 30%: 시스템 지원 동작
    /// 
    /// 2. 배타성 (Exclusivity): 촬영 시에만 발생하는가 (0~100%)
    ///    - 100%: 카메라 촬영 외에는 절대 발생 안 함
    ///    - 80%: 대부분 촬영 시 발생 (일부 예외 있음)
    ///    - 50%: 촬영 시 자주 발생하나 다른 경우도 있음
    ///    - 30%: 촬영과 무관하게도 발생 가능
    /// 
    /// 3. 신뢰성 (Reliability): 항상 기록되는가 (0~100%)
    ///    - 100%: 촬영 시 항상 기록됨
    ///    - 80%: 대부분의 경우 기록됨
    ///    - 50%: 경우에 따라 기록 안 될 수 있음
    ///    - 30%: 자주 누락됨
    /// 
    /// 가중치 계산식:
    /// Weight = (직접성 × 0.4) + (배타성 × 0.3) + (신뢰성 × 0.3)
    /// 
    /// 분류 체계:
    /// - Critical (0.5): 직접성 90~100% - 파일 저장 확정
    /// - Strong (0.35~0.4): 직접성 70~85% - HAL 레벨, 햅틱 피드백
    /// - Medium (0.25~0.3): 직접성 50~65% - 권한, Activity
    /// - Supporting (0.15~0.2): 직접성 35~45% - 보조 이벤트
    /// 
    /// 예시 계산:
    /// 
    /// DATABASE_INSERT (0.5):
    ///   - 직접성: 100% (파일 저장 = 촬영 완료 확정)
    ///   - 배타성: 90% (카메라 촬영 외 거의 없음)
    ///   - 신뢰성: 95% (항상 기록됨)
    ///   → (100×0.4) + (90×0.3) + (95×0.3) = 40 + 27 + 28.5 = 95.5 ≈ 0.95
    ///   → 실무 조정: 0.5 (다른 아티팩트와의 균형 고려)
    /// 
    /// CAMERA_CONNECT (0.4):
    ///   - 직접성: 80% (HAL 레벨 카메라 하드웨어 활성화)
    ///   - 배타성: 85% (카메라 앱 실행 시만 발생)
    ///   - 신뢰성: 90% (항상 기록됨)
    ///   → (80×0.4) + (85×0.3) + (90×0.3) = 32 + 25.5 + 27 = 84.5 ≈ 0.85
    ///   → 실무 조정: 0.4 (세션 시작 마커, 촬영 확정 아님)
    /// 
    /// VIBRATION_EVENT (0.4):
    ///   - 직접성: 75% (hapticType=50061 = 촬영 버튼 터치)
    ///   - 배타성: 80% (촬영 버튼 터치 시만 발생)
    ///   - 신뢰성: 85% (대부분 기록됨, 일부 기기는 햅틱 비활성화 가능)
    ///   → (75×0.4) + (80×0.3) + (85×0.3) = 30 + 24 + 25.5 = 79.5 ≈ 0.8
    ///   → 실무 조정: 0.4 (강력한 증거이나 기기 설정 의존)
    /// 
    /// PLAYER_EVENT (0.35):
    ///   - 직접성: 70% (셔터음 재생 = 촬영 행위 수행)
    ///   - 배타성: 75% (대부분 촬영 시 발생, 일부 미리듣기 등 예외)
    ///   - 신뢰성: 80% (무음 모드 시 누락 가능)
    ///   → (70×0.4) + (75×0.3) + (80×0.3) = 28 + 22.5 + 24 = 74.5 ≈ 0.75
    ///   → 실무 조정: 0.35 (무음 촬영 시 누락 가능성 반영)
    /// 
    /// URI_PERMISSION_GRANT (0.3):
    ///   - 직접성: 60% (파일 접근 권한 부여 = 저장 준비)
    ///   - 배타성: 65% (촬영 외 파일 접근 시에도 발생)
    ///   - 신뢰성: 70% (일부 앱은 권한 미리 획득)
    ///   → (60×0.4) + (65×0.3) + (70×0.3) = 24 + 19.5 + 21 = 64.5 ≈ 0.65
    ///   → 실무 조정: 0.3 (보조 아티팩트 수준)
    /// 
    /// ACTIVITY_LIFECYCLE (0.25):
    ///   - 직접성: 50% (앱 실행/종료 = 촬영 환경 제공)
    ///   - 배타성: 50% (카메라 앱 실행이지만 촬영 확정 아님)
    ///   - 신뢰성: 90% (항상 기록됨)
    ///   → (50×0.4) + (50×0.3) + (90×0.3) = 20 + 15 + 27 = 62 ≈ 0.6
    ///   → 실무 조정: 0.25 (세션 컨텍스트 제공용, 촬영 직접 증거 아님)
    /// 
    /// SHUTTER_SOUND (0.2):
    ///   - 직접성: 45% (셔터음 파일 참조)
    ///   - 배타성: 60% (미리듣기 등 예외 많음)
    ///   - 신뢰성: 70% (무음 모드 시 누락)
    ///   → (45×0.4) + (60×0.3) + (70×0.3) = 18 + 18 + 21 = 57 ≈ 0.57
    ///   → 실무 조정: 0.2 (PLAYER_EVENT보다 약한 증거)
    /// 
    /// PLAYER_RELEASED (0.15):
    ///   - 직접성: 35% (셔터음 재생 종료)
    ///   - 배타성: 55% (다른 오디오 해제와 혼재)
    ///   - 신뢰성: 75%
    ///   → (35×0.4) + (55×0.3) + (75×0.3) = 14 + 16.5 + 22.5 = 53 ≈ 0.5
    ///   → 실무 조정: 0.15 (부가 정보 수준)
    /// 
    /// 검증 방법:
    /// - Ground Truth 기반 ROC 분석
    /// - 가중치 합산 후 신뢰도 등급 분류 (Low/Medium/High)
    /// - Low (0.3~0.5): 조건부 핵심 단독 또는 + 보조 소수
    /// - Medium (0.5~1.0): 확정 핵심 단독 또는 조건부 + 보조 다수
    /// - High (1.0+): 확정 핵심 + 보조 다수
    /// 
    /// 향후 최적화:
    /// - 대규모 데이터셋으로 최적값 탐색 (Ablation Study)
    /// - 앱별/시나리오별 가중치 조정 (Adaptive Weight)
    /// - 머신러닝 기반 가중치 학습 (Feature Importance)
    /// </remarks>
    // ⚠️ static readonly 제거됨! 이제 인스턴스 필드 _eventTypeWeights 사용
    // 기존 값들은 ConfigurationProvider.GetDefault()에서 제공됩니다.
    
    private const double DefaultWeight = 0.1; // 알 수 없는 타입의 기본 가중치

    /// <summary>
    /// 기본 생성자 (Backward Compatibility 보장)
    /// </summary>
    /// <param name="logger">로거</param>
    /// <remarks>
    /// 기존 테스트 코드 호환성을 위해 유지됩니다.
    /// 내부적으로 ConfigurationProvider.GetDefault()를 사용하여 기본값을 제공합니다.
    /// </remarks>
    public ConfidenceCalculator(ILogger<ConfidenceCalculator> logger)
        : this(logger, ConfigurationProvider.GetDefault())
    {
    }

    /// <summary>
    /// Configuration 주입 생성자
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="config">아티팩트 탐지 설정</param>
    /// <remarks>
    /// DI 컨테이너에서 Configuration을 주입받아 동적으로 가중치를 설정합니다.
    /// YAML 파일 기반 설정 변경이 가능합니다.
    /// </remarks>
    public ConfidenceCalculator(
        ILogger<ConfidenceCalculator> logger,
        ArtifactDetectionConfig config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        // 세션 및 촬영 가중치 통합
        _eventTypeWeights = new Dictionary<string, double>();
        
        foreach (var kv in config.ArtifactWeights.Session)
            _eventTypeWeights[kv.Key] = kv.Value;
        
        foreach (var kv in config.ArtifactWeights.Capture)
            _eventTypeWeights[kv.Key] = kv.Value;
        
        _logger.LogInformation(
            "[ConfidenceCalculator] 초기화 완료: 가중치 {Count}개 로드 (세션: {SessionCount}, 촬영: {CaptureCount})",
            _eventTypeWeights.Count,
            config.ArtifactWeights.Session.Count,
            config.ArtifactWeights.Capture.Count);
    }

    /// <inheritdoc/>
    public double CalculateConfidence(IReadOnlyList<NormalizedLogEvent> artifactEvents)
    {
        if (artifactEvents == null || artifactEvents.Count == 0)
        {
            _logger.LogDebug("증거 이벤트가 없으므로 신뢰도 0.0 반환");
            return 0.0;
        }

        // 이벤트 타입별 가중치 합산
        double totalWeight = 0.0;
        var uniqueTypes = new HashSet<string>();
        var detectedArtifacts = new List<string>();

        _logger.LogInformation("════════════════════════════════════════════════════════════");
        _logger.LogInformation("[SCORE_CALC_START] 촬영 탐지 점수 계산 시작");
        _logger.LogInformation("[SCORE_CALC_START] 전체 아티팩트 수: {Count}개", artifactEvents.Count);

        foreach (var evt in artifactEvents)
        {
            // 동일 타입은 한 번만 계산 (중복 방지)
            if (!uniqueTypes.Add(evt.EventType))
            {
                _logger.LogInformation(
                    "[ARTIFACT_SKIP] EventType='{EventType}' (중복, 이미 계산됨)",
                    evt.EventType);
                continue;
            }

            var weight = GetEventTypeWeight(evt.EventType);
            totalWeight += weight;
            detectedArtifacts.Add(evt.EventType);

            _logger.LogInformation(
                "[ARTIFACT_DETECTED] EventType='{EventType}', Weight={Weight:F2}, 누적합계={Total:F2}",
                evt.EventType, weight, totalWeight);
        }

        _logger.LogInformation("────────────────────────────────────────────────────────────");
        _logger.LogInformation("[SCORE_SUMMARY] 탐지된 고유 아티팩트: {Count}개", uniqueTypes.Count);
        _logger.LogInformation("[SCORE_SUMMARY] 아티팩트 목록: [{Artifacts}]", string.Join(", ", detectedArtifacts));
        _logger.LogInformation("[SCORE_SUMMARY] 최종 탐지 점수: {Score:F2}", totalWeight);
        _logger.LogInformation("════════════════════════════════════════════════════════════");

        return totalWeight;
    }

    /// <inheritdoc/>
    public double GetEventTypeWeight(string eventType)
    {
        if (_eventTypeWeights.TryGetValue(eventType, out var weight))
            return weight;

        _logger.LogTrace("알 수 없는 EventType '{EventType}', 기본 가중치 {Weight} 반환",
            eventType, DefaultWeight);
        
        return DefaultWeight;
    }
}
