using AndroidAdbAnalyze.Console.Executor.Models;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Console.Executor.Services.Device;

/// <summary>
/// ADB 디바이스 관리 서비스 인터페이스
/// </summary>
public interface IDeviceManager
{
    /// <summary>
    /// 연결된 모든 ADB 디바이스 목록 조회
    /// </summary>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>연결된 디바이스 목록</returns>
    Task<IReadOnlyList<AdbDevice>> GetConnectedDevicesAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 단일 디바이스 연결 확인 및 검증
    /// - 디바이스가 없으면 DeviceNotConnectedException
    /// - 여러 디바이스가 연결되어 있으면 MultipleDevicesException
    /// </summary>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>사용 가능한 단일 디바이스</returns>
    /// <exception cref="Core.Exceptions.DeviceNotConnectedException">연결된 디바이스 없음</exception>
    /// <exception cref="Core.Exceptions.MultipleDevicesException">여러 디바이스 연결됨</exception>
    Task<AdbDevice> EnsureSingleDeviceAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 디바이스 정보 추출 (Parser 모듈이 필요로 하는 DeviceInfo 생성)
    /// </summary>
    /// <param name="device">대상 디바이스</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>Parser 모듈용 DeviceInfo 객체</returns>
    Task<DeviceInfo> ExtractDeviceInfoAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default);
}

