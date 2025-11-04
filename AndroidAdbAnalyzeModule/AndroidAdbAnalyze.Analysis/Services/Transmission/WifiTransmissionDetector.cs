using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Transmission;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Transmission;

/// <summary>
/// WiFi 기반 전송 탐지 서비스 구현
/// </summary>
/// <remarks>
/// sem_wifi 로그의 TX 패킷 증가량을 분석하여 전송 발생 여부를 탐지합니다.
/// </remarks>
public sealed class WifiTransmissionDetector : ITransmissionDetector
{
    private readonly ILogger<WifiTransmissionDetector> _logger;

    /// <summary>
    /// WifiTransmissionDetector 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="logger">로거</param>
    public WifiTransmissionDetector(ILogger<WifiTransmissionDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public TransmissionResult DetectTransmission(
        CameraCaptureEvent capture,
        IReadOnlyList<NormalizedLogEvent> allEvents,
        AnalysisOptions options)
    {
        if (capture == null)
            throw new ArgumentNullException(nameof(capture));
        if (allEvents == null)
            throw new ArgumentNullException(nameof(allEvents));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        // 전송 탐지 기능이 비활성화된 경우
        if (!options.EnableTransmissionDetection)
        {
            _logger.LogTrace(
                "[WifiTransmissionDetector] 전송 탐지 비활성화: CaptureId={CaptureId}",
                capture.CaptureId);
            return TransmissionResult.Empty;
        }

        // sem_wifi 이벤트 필터링 (촬영 패키지와 일치하는 것만)
#pragma warning disable CS0618 // WIFI_PACKET_TRANSMISSION은 연구 범위에서 제외되었지만 호환성을 위해 유지
        var wifiEvents = allEvents
            .Where(e => e.EventType == LogEventTypes.WIFI_PACKET_TRANSMISSION)
#pragma warning restore CS0618
            .Where(e => e.Attributes.ContainsKey("packageName") && 
                       e.Attributes["packageName"].ToString() == capture.PackageName)
            .Where(e => e.Timestamp >= capture.CaptureTime &&
                       e.Timestamp <= capture.CaptureTime + options.TransmissionDetectionWindow)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (wifiEvents.Count == 0)
        {
            _logger.LogDebug(
                "[WifiTransmissionDetector] sem_wifi 이벤트 없음: CaptureId={CaptureId}, Package={Package}",
                capture.CaptureId, capture.PackageName);
            return TransmissionResult.Empty;
        }

        _logger.LogDebug(
            "[WifiTransmissionDetector] sem_wifi 이벤트 {Count}개 발견: CaptureId={CaptureId}, " +
            "Package={Package}, Window={Window}초",
            wifiEvents.Count, capture.CaptureId, capture.PackageName,
            options.TransmissionDetectionWindow.TotalSeconds);

        // 패킷 델타 분석
        var threshold = GetPacketThreshold(capture.PackageName, options);
        
        // 베이스라인 TX 패킷 수 추출 (촬영 시간 이전의 마지막 값)
        var baselineTx = GetBaselineTxPackets(capture, allEvents);
        
        int startIndex = 0;  // 비교 시작 인덱스
        if (baselineTx == null)
        {
            _logger.LogDebug(
                "[WifiTransmissionDetector] 베이스라인 없음: CaptureId={CaptureId}, Package={Package}. " +
                "촬영 시간 이전 sem_wifi 이벤트가 없어 윈도우 내 첫 이벤트를 베이스라인으로 사용합니다.",
                capture.CaptureId, capture.PackageName);
            
            // 베이스라인이 없으면 윈도우 내 첫 이벤트를 베이스라인으로 사용
            if (wifiEvents.Count > 0 && TryGetTxPackets(wifiEvents[0], out int firstTx))
            {
                baselineTx = firstTx;
                startIndex = 1;  // 첫 이벤트를 베이스라인으로 사용했으므로 두 번째부터 비교 시작
            }
            else
            {
                _logger.LogDebug(
                    "[WifiTransmissionDetector] 베이스라인 설정 실패: CaptureId={CaptureId}",
                    capture.CaptureId);
                return TransmissionResult.Empty;
            }
        }

        _logger.LogTrace(
            "[WifiTransmissionDetector] 베이스라인 TX: {BaselineTx}, 임계값: {Threshold}, 시작 인덱스: {StartIndex}/{TotalCount}",
            baselineTx, threshold, startIndex, wifiEvents.Count);

        // 윈도우 내 이벤트를 베이스라인과 비교 (startIndex부터 시작)
        for (int i = startIndex; i < wifiEvents.Count; i++)
        {
            var currentEvent = wifiEvents[i];
            
            if (!TryGetTxPackets(currentEvent, out int currentTx))
                continue;

            var delta = currentTx - baselineTx.Value;

            _logger.LogTrace(
                "[WifiTransmissionDetector] [#{Index}] 시간={Time:HH:mm:ss.fff}, TX={CurrentTx}, " +
                "델타={Delta} (베이스라인={Baseline}, 임계값={Threshold})",
                i, currentEvent.Timestamp, currentTx, delta, baselineTx, threshold);

            if (delta >= threshold)
            {
                _logger.LogInformation(
                    "[WifiTransmissionDetector] 전송 탐지 성공: CaptureId={CaptureId}, Package={Package}, " +
                    "TransmissionTime={Time:HH:mm:ss.fff}, Packets={Packets} (베이스라인={Baseline})",
                    capture.CaptureId, capture.PackageName, currentEvent.Timestamp, delta, baselineTx);

                return new TransmissionResult
                {
                    IsTransmitted = true,
                    TransmissionTime = currentEvent.Timestamp,
                    TransmittedPackets = delta,
                    DetectionMethod = "WiFi",
                    DetectedUid = TryGetUid(currentEvent, out int uid) ? uid : null
                };
            }
        }

        _logger.LogDebug(
            "[WifiTransmissionDetector] 전송 미탐지: CaptureId={CaptureId}, Package={Package}, " +
            "Threshold={Threshold}, Baseline={Baseline}, 검사한 이벤트={CheckedCount}개",
            capture.CaptureId, capture.PackageName, threshold, baselineTx, wifiEvents.Count - startIndex);

        return TransmissionResult.Empty;
    }

    /// <summary>
    /// 촬영 시간 이전의 베이스라인 TX 패킷 수를 추출합니다.
    /// </summary>
    /// <param name="capture">촬영 이벤트</param>
    /// <param name="allEvents">전체 로그 이벤트</param>
    /// <returns>베이스라인 TX 패킷 수 (없으면 null)</returns>
    /// <remarks>
    /// 정확한 전송량 측정을 위해 촬영 시간 이전의 마지막 sem_wifi 측정값을 사용합니다.
    /// 이를 통해 촬영 후 실제로 증가한 패킷량만 정확히 측정할 수 있습니다.
    /// </remarks>
    private int? GetBaselineTxPackets(
        CameraCaptureEvent capture, 
        IReadOnlyList<NormalizedLogEvent> allEvents)
    {
        // 촬영 시간 이전의 마지막 sem_wifi 이벤트 찾기
#pragma warning disable CS0618 // WIFI_PACKET_TRANSMISSION은 연구 범위에서 제외되었지만 호환성을 위해 유지
        var baselineEvent = allEvents
            .Where(e => e.EventType == LogEventTypes.WIFI_PACKET_TRANSMISSION)
#pragma warning restore CS0618
            .Where(e => e.Attributes.ContainsKey("packageName") && 
                       e.Attributes["packageName"].ToString() == capture.PackageName)
            .Where(e => e.Timestamp < capture.CaptureTime)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        if (baselineEvent != null && TryGetTxPackets(baselineEvent, out int baselineTx))
        {
            _logger.LogTrace(
                "[WifiTransmissionDetector] 베이스라인 발견: CaptureId={CaptureId}, " +
                "BaselineTime={Time:HH:mm:ss.fff}, TX={Tx}",
                capture.CaptureId, baselineEvent.Timestamp, baselineTx);
            return baselineTx;
        }

        _logger.LogTrace(
            "[WifiTransmissionDetector] 베이스라인 미발견: CaptureId={CaptureId}, " +
            "촬영 시간 이전 sem_wifi 이벤트 없음",
            capture.CaptureId);
        return null;
    }

    /// <summary>
    /// 패키지명 기반 패킷 임계값 결정
    /// </summary>
    /// <remarks>
    /// 향후 확장: ITransmissionDetectionPolicy 인터페이스 도입 가능
    /// 현재는 단순하게 기본값 사용
    /// </remarks>
    private int GetPacketThreshold(string packageName, AnalysisOptions options)
    {
        // 기본 임계값 사용 (향후 패키지별 정책 추가 가능)
        return options.DefaultTransmissionPacketThreshold;
    }

    /// <summary>
    /// sem_wifi 이벤트에서 TX 패킷 수 추출
    /// </summary>
    private bool TryGetTxPackets(NormalizedLogEvent wifiEvent, out int txPackets)
    {
        txPackets = 0;
        
        if (!wifiEvent.Attributes.TryGetValue("txPackets", out var txObj))
            return false;

        if (txObj is int txInt)
        {
            txPackets = txInt;
            return true;
        }

        if (int.TryParse(txObj.ToString(), out txPackets))
            return true;

        return false;
    }

    /// <summary>
    /// sem_wifi 이벤트에서 UID 추출
    /// </summary>
    private bool TryGetUid(NormalizedLogEvent wifiEvent, out int uid)
    {
        uid = 0;
        
        if (!wifiEvent.Attributes.TryGetValue("uid", out var uidObj))
            return false;

        if (uidObj is int uidInt)
        {
            uid = uidInt;
            return true;
        }

        if (int.TryParse(uidObj.ToString(), out uid))
            return true;

        return false;
    }
}

