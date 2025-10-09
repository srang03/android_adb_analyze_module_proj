using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 촬영 이벤트 감지 서비스 인터페이스
/// </summary>
public interface ICaptureDetector
{
    /// <summary>
    /// 세션 내에서 카메라 촬영 이벤트를 감지합니다.
    /// </summary>
    /// <param name="session">분석 대상 세션</param>
    /// <param name="events">중복 제거된 전체 이벤트 목록</param>
    /// <param name="options">분석 옵션</param>
    /// <returns>감지된 촬영 이벤트 목록</returns>
    IReadOnlyList<CameraCaptureEvent> DetectCaptures(
        CameraSession session,
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options);
}
