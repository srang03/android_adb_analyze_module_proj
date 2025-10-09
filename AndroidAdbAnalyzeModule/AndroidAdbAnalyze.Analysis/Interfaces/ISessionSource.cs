using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Options;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 세션 소스 인터페이스
/// </summary>
/// <remarks>
/// 다양한 로그 소스(usagestats, media_camera 등)에서 세션을 추출하기 위한 인터페이스입니다.
/// 각 구현체는 자신만의 로직으로 세션을 추출하며, 우선순위에 따라 병합됩니다.
/// </remarks>
public interface ISessionSource
{
    /// <summary>
    /// 세션 소스의 우선순위 (높을수록 우선)
    /// </summary>
    /// <remarks>
    /// Primary 소스(usagestats): 100
    /// Secondary 소스(media_camera): 50
    /// </remarks>
    int Priority { get; }
    
    /// <summary>
    /// 세션 소스 이름 (로깅 및 디버깅용)
    /// </summary>
    string SourceName { get; }
    
    /// <summary>
    /// 이벤트 목록에서 세션을 추출합니다.
    /// </summary>
    /// <param name="events">분석할 이벤트 목록</param>
    /// <param name="options">분석 옵션</param>
    /// <returns>추출된 세션 목록</returns>
    IReadOnlyList<CameraSession> ExtractSessions(
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options);
}

