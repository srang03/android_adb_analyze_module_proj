using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Transmission;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Interfaces;

/// <summary>
/// 전송 탐지 서비스 인터페이스
/// </summary>
/// <remarks>
/// 카메라 촬영 이벤트 이후 네트워크 전송 발생 여부를 탐지합니다.
/// sem_wifi 로그 분석을 통해 패킷 증가량을 측정합니다.
/// </remarks>
public interface ITransmissionDetector
{
    /// <summary>
    /// 촬영 이벤트 이후 전송 발생 여부를 탐지합니다.
    /// </summary>
    /// <param name="capture">촬영 이벤트</param>
    /// <param name="allEvents">세션 내 모든 로그 이벤트 (sem_wifi 로그 포함)</param>
    /// <param name="options">분석 옵션</param>
    /// <returns>전송 탐지 결과</returns>
    /// <remarks>
    /// - options.EnableTransmissionDetection이 false이면 Empty 결과 반환
    /// - sem_wifi 이벤트가 없으면 Empty 결과 반환
    /// - 촬영 시간 이후의 패킷만 검사하여 앨범 전송 자동 배제
    /// </remarks>
    TransmissionResult DetectTransmission(
        CameraCaptureEvent capture,
        IReadOnlyList<NormalizedLogEvent> allEvents,
        AnalysisOptions options);
}

