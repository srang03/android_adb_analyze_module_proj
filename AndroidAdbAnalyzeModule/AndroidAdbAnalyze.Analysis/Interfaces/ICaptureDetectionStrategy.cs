using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 촬영 탐지 전략 인터페이스
/// </summary>
/// <remarks>
/// 앱별로 다른 촬영 탐지 로직을 캡슐화합니다.
/// - 기본 카메라: PostProcessService + PLAYER_EVENT 필터링
/// - Telegram: VIBRATION_EVENT + URI_PERMISSION 조합
/// - KakaoTalk: URI_PERMISSION (임시 파일 경로)
/// </remarks>
public interface ICaptureDetectionStrategy
{
    /// <summary>
    /// 전략이 지원하는 패키지명 패턴
    /// </summary>
    /// <remarks>
    /// 예: "com.sec.android.app.camera" (기본 카메라)
    ///      "org.telegram.messenger" (Telegram)
    ///      "com.kakao.talk" (KakaoTalk)
    /// null이면 기본 전략으로 동작합니다.
    /// </remarks>
    string? PackageNamePattern { get; }
    
    /// <summary>
    /// 세션에서 촬영 이벤트 탐지
    /// </summary>
    /// <param name="context">세션 컨텍스트</param>
    /// <param name="options">분석 옵션 (신뢰도 임계값, 상관관계 윈도우 등)</param>
    /// <returns>탐지된 촬영 이벤트 목록</returns>
    /// <remarks>
    /// SessionContext를 사용하여:
    /// - 필요한 로그 이벤트 조회
    /// - Foreground Service 정보 확인
    /// - Activity 상태 확인
    /// - 시간대별 이벤트 상관관계 분석
    /// </remarks>
    IReadOnlyList<CameraCaptureEvent> DetectCaptures(SessionContext context, AnalysisOptions options);
}
