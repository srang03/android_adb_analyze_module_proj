using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 세션 감지 서비스 인터페이스
/// </summary>
public interface ISessionDetector
{
    /// <summary>
    /// 이벤트 목록에서 카메라 세션을 감지합니다.
    /// </summary>
    /// <param name="events">중복 제거된 이벤트 목록</param>
    /// <param name="options">분석 옵션</param>
    /// <returns>감지된 세션 목록</returns>
    IReadOnlyList<CameraSession> DetectSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options);
}
