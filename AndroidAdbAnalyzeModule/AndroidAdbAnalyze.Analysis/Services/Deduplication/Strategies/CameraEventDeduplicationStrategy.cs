using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyze.Analysis.Interfaces;

namespace AndroidAdbAnalyze.Analysis.Services.Deduplication.Strategies;

/// <summary>
/// 카메라 이벤트 전용 중복 판정 전략
/// </summary>
/// <remarks>
/// 카메라 이벤트의 특성:
/// - 배타적 이벤트: 같은 앱이 같은 카메라를 동시에 2번 열 수 없음 (Android HAL 제약)
/// - 중복 판정 조건: package + cameraId/deviceId + eventType + 시간 근접성
/// </remarks>
public sealed class CameraEventDeduplicationStrategy : IDeduplicationStrategy
{
    private readonly int _timeThresholdMs;

    /// <summary>
    /// CameraEventDeduplicationStrategy 생성자
    /// </summary>
    /// <param name="timeThresholdMs">시간 임계값 (밀리초). 기본값 1000ms (1초)</param>
    public CameraEventDeduplicationStrategy(int timeThresholdMs = 1000)
    {
        _timeThresholdMs = timeThresholdMs;
    }

    /// <inheritdoc/>
    public bool IsDuplicate(NormalizedLogEvent event1, NormalizedLogEvent event2)
    {
        if (event1 == null || event2 == null)
            return false;

        // 1. EventType 동일 확인 (CONNECT vs CONNECT, DISCONNECT vs DISCONNECT)
        if (event1.EventType != event2.EventType)
            return false;

        // 2. Package 동일 확인 (있는 경우만)
        var package1 = GetAttributeValue(event1, "package");
        var package2 = GetAttributeValue(event2, "package");
        
        // 둘 다 package가 있는 경우: package도 일치해야 중복
        if (!string.IsNullOrEmpty(package1) && !string.IsNullOrEmpty(package2))
        {
            if (!string.Equals(package1, package2, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        // 하나만 package가 있는 경우: 다른 소스이므로 중복 아님
        else if (!string.IsNullOrEmpty(package1) != !string.IsNullOrEmpty(package2))
        {
            return false;
        }
        // 둘 다 package가 없는 경우: cameraId 확인으로 진행

        // 3. CameraId/DeviceId 동일 확인 (있는 경우만)
        var cameraId1 = GetCameraOrDeviceId(event1);
        var cameraId2 = GetCameraOrDeviceId(event2);
        
        // 둘 다 cameraId가 있는 경우: cameraId도 일치해야 중복
        if (cameraId1.HasValue && cameraId2.HasValue)
        {
            if (cameraId1.Value != cameraId2.Value)
                return false;
        }
        // 하나만 cameraId가 있는 경우: 다른 로그 소스이므로 중복 아님
        else if (cameraId1.HasValue != cameraId2.HasValue)
        {
            return false;
        }
        // 둘 다 cameraId가 없는 경우: package + eventType + 시간으로만 판정 (다음 단계로 진행)

        // 4. 시간 근접성 확인 (보조 검증)
        var timeDiff = Math.Abs((event1.Timestamp - event2.Timestamp).TotalMilliseconds);
        return timeDiff <= _timeThresholdMs;
    }

    /// <summary>
    /// CameraId 또는 DeviceId 추출
    /// </summary>
    /// <remarks>
    /// 다양한 로그 파일에서 사용되는 카메라 ID 속성명을 모두 지원합니다:
    /// - "cameraId": media_camera_worker.log
    /// - "deviceId": media_camera.log
    /// - "camera_id": 향후 추가될 수 있는 로그
    /// - "device_id": 향후 추가될 수 있는 로그
    /// </remarks>
    private int? GetCameraOrDeviceId(NormalizedLogEvent evt)
    {
        // 표준 카메라 ID 속성명 목록 (우선순위 순)
        var cameraIdKeys = new[] { "cameraId", "deviceId", "camera_id", "device_id" };

        foreach (var key in cameraIdKeys)
        {
            if (evt.Attributes.TryGetValue(key, out var idObj))
            {
                // int 타입 직접 반환
                if (idObj is int id)
                    return id;
                
                // 문자열 파싱 시도
                if (int.TryParse(idObj?.ToString(), out var parsed))
                    return parsed;
            }
        }

        return null;
    }

    /// <summary>
    /// Attribute 값 추출 (문자열 변환)
    /// </summary>
    private string? GetAttributeValue(NormalizedLogEvent evt, string key)
    {
        if (evt.Attributes.TryGetValue(key, out var value))
            return value?.ToString();
        return null;
    }
}

