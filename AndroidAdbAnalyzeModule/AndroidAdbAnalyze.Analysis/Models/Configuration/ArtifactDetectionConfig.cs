namespace AndroidAdbAnalyze.Analysis.Models.Configuration;

/// <summary>
/// 아티팩트 기반 촬영 탐지 설정
/// </summary>
/// <remarks>
/// YAML 파일 또는 코드 기반으로 아티팩트 가중치, 분류, 검증 상수를 관리합니다.
/// 런타임 설정 변경을 통해 빌드 없이 탐지 로직을 조정할 수 있습니다.
/// </remarks>
public sealed class ArtifactDetectionConfig
{
    /// <summary>
    /// 아티팩트 가중치 설정
    /// </summary>
    public ArtifactWeightsConfig ArtifactWeights { get; set; } = new();
    
    /// <summary>
    /// 전략별 아티팩트 분류 설정
    /// </summary>
    public Dictionary<string, StrategyConfig> Strategies { get; set; } = new();
    
    /// <summary>
    /// 검증 상수 설정
    /// </summary>
    public ValidationConstantsConfig Validation { get; set; } = new();
    
    /// <summary>
    /// 분석 옵션 기본값
    /// </summary>
    public AnalysisOptionsConfig? AnalysisOptions { get; set; }
}

/// <summary>
/// 아티팩트 가중치 설정
/// </summary>
/// <remarks>
/// 세션 완전성 점수 및 촬영 탐지 점수 계산에 사용되는 이벤트 타입별 가중치를 정의합니다.
/// </remarks>
public sealed class ArtifactWeightsConfig
{
    /// <summary>
    /// 세션 관련 이벤트 가중치
    /// </summary>
    /// <remarks>
    /// 세션 완전성 점수 계산에 사용됩니다.
    /// 예: ACTIVITY_RESUMED: 0.7, CAMERA_CONNECT: 0.6
    /// </remarks>
    public Dictionary<string, double> Session { get; set; } = new();
    
    /// <summary>
    /// 촬영 관련 이벤트 가중치
    /// </summary>
    /// <remarks>
    /// 촬영 탐지 점수 계산에 사용됩니다.
    /// 예: DATABASE_INSERT: 0.5, VIBRATION_EVENT: 0.4
    /// </remarks>
    public Dictionary<string, double> Capture { get; set; } = new();
}

/// <summary>
/// Strategy별 아티팩트 분류 설정
/// </summary>
public sealed class StrategyConfig
{
    /// <summary>
    /// 패키지 패턴 (null이면 기본 전략)
    /// </summary>
    public string? PackagePattern { get; set; }
    
    /// <summary>
    /// 핵심 아티팩트 타입 목록 (촬영 100% 확정)
    /// </summary>
    /// <remarks>
    /// 예: DATABASE_INSERT, DATABASE_EVENT
    /// </remarks>
    public List<string> KeyArtifacts { get; set; } = new();
    
    /// <summary>
    /// 조건부 핵심 아티팩트 타입 목록 (특정 조건 만족 시 촬영 확정)
    /// </summary>
    /// <remarks>
    /// 예: VIBRATION_EVENT (hapticType=50061), PLAYER_EVENT (PostProcessService)
    /// </remarks>
    public List<string> ConditionalKeyArtifacts { get; set; } = new();
    
    /// <summary>
    /// 보조 아티팩트 타입 목록 (보조 증거)
    /// </summary>
    /// <remarks>
    /// 예: PLAYER_CREATED, SHUTTER_SOUND, MEDIA_EXTRACTOR
    /// </remarks>
    public List<string> SupportingArtifacts { get; set; } = new();
}

/// <summary>
/// 검증 상수 설정
/// </summary>
/// <remarks>
/// 조건부 핵심 아티팩트 검증에 사용되는 상수값들을 정의합니다.
/// </remarks>
public sealed class ValidationConstantsConfig
{
    /// <summary>
    /// 카메라 셔터 햅틱 타입 코드
    /// </summary>
    /// <remarks>
    /// VIBRATION_EVENT 검증 시 사용됩니다.
    /// 기본값: 50061 (촬영 버튼 터치)
    /// </remarks>
    public int HapticTypeCameraShutter { get; set; } = 50061;
    
    /// <summary>
    /// PLAYER_EVENT 상태: 시작됨
    /// </summary>
    /// <remarks>
    /// PLAYER_EVENT 검증 시 event 속성 값으로 사용됩니다.
    /// 기본값: "started"
    /// </remarks>
    public string PlayerEventStateStarted { get; set; } = "started";
    
    /// <summary>
    /// PLAYER_CREATED 태그: 카메라
    /// </summary>
    /// <remarks>
    /// PLAYER_EVENT 검증 시 PLAYER_CREATED의 tags 속성 값으로 사용됩니다.
    /// 기본값: "CAMERA"
    /// </remarks>
    public string PlayerTagCamera { get; set; } = "CAMERA";
    
    /// <summary>
    /// Foreground Service 클래스명: PostProcessService
    /// </summary>
    /// <remarks>
    /// PLAYER_EVENT 검증 시 ForegroundServices 확인에 사용됩니다.
    /// 기본값: "PostProcessService"
    /// 
    /// ⚠️ 주의: 이 속성은 기존 코드 호환성을 위해 유지됩니다.
    /// 새로운 FOREGROUND_SERVICE 검증 로직은 ServiceClassCaptureConfirmed와 
    /// ServiceClassCapturePossible를 사용합니다.
    /// </remarks>
    public string ServiceClassPostProcess { get; set; } = "PostProcessService";
    
    /// <summary>
    /// 촬영 확정 서비스 클래스 목록 (FOREGROUND_SERVICE 검증용)
    /// </summary>
    /// <remarks>
    /// <para>
    /// usagestats 로그 기반 촬영 탐지에 사용됩니다.
    /// 재부팅 휘발성 환경에서 다른 로그(vibrator, audio, media_camera_worker)가 
    /// 사라졌을 때, usagestats 로그만으로 촬영 여부를 판단합니다.
    /// </para>
    /// <para>
    /// <b>매칭 방식:</b> 부분 문자열 포함 (Contains, OrdinalIgnoreCase)
    /// </para>
    /// <para>
    /// <b>촬영 확정 서비스:</b>
    /// - PostProcessService: 기본 카메라 앱의 촬영 후처리 서비스
    ///   (전체 클래스명: com.samsung.android.camera.core2.processor.PostProcessService)
    /// </para>
    /// <para>
    /// <b>예시:</b>
    /// <code>
    /// serviceClassCaptureConfirmed:
    ///   - "PostProcessService"  # 부분 문자열 매칭
    /// </code>
    /// </para>
    /// <para>
    /// <b>추가 (2025-10-26):</b> 재부팅 휘발성 대응
    /// </para>
    /// </remarks>
    public List<string> ServiceClassCaptureConfirmed { get; set; } = new()
    {
        "PostProcessService"
    };
    
    /// <summary>
    /// 촬영 가능성 서비스 클래스 목록 (FOREGROUND_SERVICE 검증용)
    /// </summary>
    /// <remarks>
    /// <para>
    /// usagestats 로그 기반 촬영 탐지에 사용됩니다.
    /// 촬영 확정보다는 낮은 신뢰도이지만, 촬영 가능성이 높은 서비스들을 정의합니다.
    /// </para>
    /// <para>
    /// <b>매칭 방식:</b> 부분 문자열 포함 (Contains, OrdinalIgnoreCase)
    /// </para>
    /// <para>
    /// <b>촬영 가능성 서비스:</b>
    /// - NotificationService: 기본 카메라 앱 및 카카오톡 호출 시 사용
    ///   (전체 클래스명: com.sec.android.app.camera.service.NotificationService)
    /// </para>
    /// <para>
    /// <b>주의:</b>
    /// - 카카오톡은 기본 카메라 앱을 호출하므로 package=com.sec.android.app.camera로 기록됨
    /// - 따라서 BasePatternStrategy가 적용되며, NotificationService 검증이 유효함
    /// </para>
    /// <para>
    /// <b>예시:</b>
    /// <code>
    /// serviceClassCapturePossible:
    ///   - "NotificationService"  # 부분 문자열 매칭
    /// </code>
    /// </para>
    /// <para>
    /// <b>추가 (2025-10-26):</b> 재부팅 휘발성 대응
    /// </para>
    /// </remarks>
    public List<string> ServiceClassCapturePossible { get; set; } = new()
    {
        "NotificationService"
    };
}

/// <summary>
/// 분석 옵션 기본값 설정
/// </summary>
/// <remarks>
/// YAML 파일에서 분석 옵션 기본값을 제공할 수 있습니다.
/// 실제 AnalysisOptions 객체는 런타임에 이 값들을 기반으로 생성됩니다.
/// </remarks>
public sealed class AnalysisOptionsConfig
{
    /// <summary>
    /// 임계값 설정
    /// </summary>
    public ThresholdsConfig Thresholds { get; set; } = new();
    
    /// <summary>
    /// 시간 윈도우 설정
    /// </summary>
    public TimeWindowsConfig TimeWindows { get; set; } = new();
    
    /// <summary>
    /// 패키지 필터 설정
    /// </summary>
    public PackagesConfig Packages { get; set; } = new();
    
    /// <summary>
    /// 경로 패턴 설정
    /// </summary>
    public PathPatternsConfig PathPatterns { get; set; } = new();
}

/// <summary>
/// 임계값 설정
/// </summary>
public sealed class ThresholdsConfig
{
    /// <summary>
    /// 최소 신뢰도 임계값
    /// </summary>
    /// <remarks>
    /// 기본값: 0.3 (30%)
    /// </remarks>
    public double MinConfidence { get; set; } = 0.3;
    
    /// <summary>
    /// 중복 제거 유사도 임계값 (Jaccard Similarity)
    /// </summary>
    /// <remarks>
    /// 기본값: 0.8 (80%)
    /// </remarks>
    public double DeduplicationSimilarity { get; set; } = 0.8;
}

/// <summary>
/// 시간 윈도우 설정 (초 단위)
/// </summary>
public sealed class TimeWindowsConfig
{
    /// <summary>
    /// 세션 간 최대 간격 (분)
    /// </summary>
    /// <remarks>
    /// 기본값: 5 (5분)
    /// </remarks>
    public int MaxSessionGapMinutes { get; set; } = 5;
    
    /// <summary>
    /// 이벤트 상관관계 윈도우 (초)
    /// </summary>
    /// <remarks>
    /// 보조 아티팩트 수집 범위
    /// 기본값: 30 (30초)
    /// </remarks>
    public int EventCorrelationSeconds { get; set; } = 30;
    
    /// <summary>
    /// 촬영 중복 제거 윈도우 (초)
    /// </summary>
    /// <remarks>
    /// 동일 촬영의 여러 핵심 아티팩트를 1개로 통합
    /// 기본값: 1 (1초)
    /// </remarks>
    public int CaptureDeduplicationSeconds { get; set; } = 1;
}

/// <summary>
/// 패키지 필터 설정
/// </summary>
public sealed class PackagesConfig
{
    /// <summary>
    /// 화이트리스트 (null 또는 빈 배열이면 모든 패키지)
    /// </summary>
    public List<string>? Whitelist { get; set; }
    
    /// <summary>
    /// 블랙리스트
    /// </summary>
    public List<string> Blacklist { get; set; } = new();
}

/// <summary>
/// 경로 패턴 설정
/// </summary>
public sealed class PathPatternsConfig
{
    /// <summary>
    /// 스크린샷 경로 패턴 (오탐 방지)
    /// </summary>
    public List<string> Screenshot { get; set; } = new()
    {
        "/Screenshots/",
        "/screenshot/",
        "Screenshot_"
    };
    
    /// <summary>
    /// 다운로드 경로 패턴 (오탐 방지)
    /// </summary>
    public List<string> Download { get; set; } = new()
    {
        "/Download/",
        "/download/",
        "Download_"
    };
}

