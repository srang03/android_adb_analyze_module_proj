using System.Globalization;
using System.Text.RegularExpressions;
using AndroidAdbAnalyze.Console.Executor.Core.Exceptions;
using AndroidAdbAnalyze.Console.Executor.Models;
using AndroidAdbAnalyze.Console.Executor.Services.Adb;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Console.Executor.Services.Device;

/// <summary>
/// ADB 디바이스 관리 서비스 구현체
/// </summary>
public sealed class DeviceManager : IDeviceManager
{
    private readonly IAdbCommandExecutor _adbExecutor;
    private readonly ILogger<DeviceManager> _logger;
    
    // adb devices 출력 파싱 정규식
    // 예: "1234567890\tdevice" 또는 "192.168.0.100:5555\tdevice product:SM-S926N model:SM_S926N device:s5e9945"
    private static readonly Regex DeviceLineRegex = new(
        @"^(?<serial>[^\s]+)\s+(?<state>\w+)(?:\s+product:(?<product>[^\s]+))?" +
        @"(?:\s+model:(?<model>[^\s]+))?(?:\s+device:(?<device>[^\s]+))?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public DeviceManager(
        IAdbCommandExecutor adbExecutor,
        ILogger<DeviceManager> logger)
    {
        _adbExecutor = adbExecutor ?? throw new ArgumentNullException(nameof(adbExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<AdbDevice>> GetConnectedDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("연결된 ADB 디바이스 목록 조회 중...");
        
        var result = await _adbExecutor.ExecuteAsync("devices -l", cancellationToken: cancellationToken);
        
        if (!result.Success)
        {
            _logger.LogWarning("adb devices 명령 실패: {StandardError}", result.StandardError);
            return Array.Empty<AdbDevice>();
        }

        var devices = new List<AdbDevice>();
        var matches = DeviceLineRegex.Matches(result.StandardOutput);
        
        foreach (Match match in matches)
        {
            var serial = match.Groups["serial"].Value;
            var state = match.Groups["state"].Value;
            var model = match.Groups["model"].Success 
                ? match.Groups["model"].Value.Replace("_", " ")  // SM_S926N → SM S926N
                : null;
            
            var device = new AdbDevice
            {
                Serial = serial,
                State = state,
                Model = model,
                ConnectionType = AdbDevice.DetermineConnectionType(serial)
            };
            
            devices.Add(device);
            
            _logger.LogDebug(
                "디바이스 발견: Serial={Serial}, State={State}, Model={Model}, Type={Type}",
                device.Serial, device.State, device.Model, device.ConnectionType);
        }
        
        _logger.LogInformation("총 {Count}개의 디바이스가 연결되어 있습니다.", devices.Count);
        
        return devices.AsReadOnly();
    }

    public async Task<AdbDevice> EnsureSingleDeviceAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = await GetConnectedDevicesAsync(cancellationToken);
        
        // 사용 가능한 디바이스만 필터링
        var availableDevices = devices.Where(d => d.IsAvailable).ToList();
        
        if (availableDevices.Count == 0)
        {
            _logger.LogError("연결된 디바이스가 없거나 모두 사용 불가능 상태입니다.");
            throw new DeviceNotConnectedException();
        }
        
        if (availableDevices.Count > 1)
        {
            var serials = availableDevices.Select(d => d.Serial).ToList();
            _logger.LogError(
                "여러 디바이스가 연결되어 있습니다: {Devices}",
                string.Join(", ", serials));
            throw new MultipleDevicesException(serials);
        }
        
        var device = availableDevices[0];
        _logger.LogInformation(
            "단일 디바이스 확인됨: {Serial} ({Type})",
            device.Serial, device.ConnectionType);
        
        return device;
    }

    public async Task<DeviceInfo> ExtractDeviceInfoAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("디바이스 정보 추출 중: {Serial}", device.Serial);
        
        // 병렬로 모든 정보 추출 (성능 최적화)
        var timeZoneTask = GetPropertyAsync("persist.sys.timezone", cancellationToken);
        var androidVersionTask = GetPropertyAsync("ro.build.version.release", cancellationToken);
        var manufacturerTask = GetPropertyAsync("ro.product.manufacturer", cancellationToken);
        var modelTask = GetPropertyAsync("ro.product.model", cancellationToken);
        var currentTimeTask = GetDeviceCurrentTimeAsync(cancellationToken);
        
        await Task.WhenAll(
            timeZoneTask, androidVersionTask, manufacturerTask, 
            modelTask, currentTimeTask);
        
        var deviceInfo = new DeviceInfo
        {
            TimeZone = timeZoneTask.Result ?? "Asia/Seoul",  // 기본값
            CurrentTime = currentTimeTask.Result,
            AndroidVersion = androidVersionTask.Result,
            Manufacturer = manufacturerTask.Result,
            Model = modelTask.Result ?? device.Model  // AdbDevice에서 가져온 모델명 사용
        };
        
        _logger.LogInformation(
            "디바이스 정보 추출 완료: Android={AndroidVersion}, Manufacturer={Manufacturer}, " +
            "Model={Model}, TimeZone={TimeZone}, CurrentTime={CurrentTime}",
            deviceInfo.AndroidVersion, deviceInfo.Manufacturer, deviceInfo.Model,
            deviceInfo.TimeZone, deviceInfo.CurrentTime.ToString("yyyy-MM-dd HH:mm:ss"));
        
        return deviceInfo;
    }

    /// <summary>
    /// getprop 명령으로 디바이스 속성 추출
    /// </summary>
    private async Task<string?> GetPropertyAsync(
        string propertyName, 
        CancellationToken cancellationToken)
    {
        var result = await _adbExecutor.ExecuteAsync(
            $"shell getprop {propertyName}",
            cancellationToken: cancellationToken);
        
        if (result.Success)
        {
            var value = result.StandardOutput.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                _logger.LogDebug("속성 추출 성공: {Property} = {Value}", propertyName, value);
                return value;
            }
        }
        
        _logger.LogWarning("속성 추출 실패: {Property}", propertyName);
        return null;
    }

    /// <summary>
    /// 디바이스의 현재 시간 추출
    /// </summary>
    private async Task<DateTime> GetDeviceCurrentTimeAsync(CancellationToken cancellationToken)
    {
        // Android date 명령: "+%Y-%m-%d %H:%M:%S" 형식으로 출력
        var result = await _adbExecutor.ExecuteAsync(
            "shell date +\"%Y-%m-%d %H:%M:%S\"",
            cancellationToken: cancellationToken);
        
        if (result.Success)
        {
            var dateStr = result.StandardOutput.Trim();
            
            if (DateTime.TryParseExact(
                dateStr,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var deviceTime))
            {
                _logger.LogDebug("디바이스 시간 추출 성공: {DeviceTime}", deviceTime);
                return deviceTime;
            }
            
            _logger.LogWarning("디바이스 시간 파싱 실패: {DateStr}", dateStr);
        }
        
        // 실패 시 현재 시스템 시간 사용
        var fallbackTime = DateTime.Now;
        _logger.LogWarning("디바이스 시간 추출 실패, 시스템 시간 사용: {FallbackTime}", fallbackTime);
        return fallbackTime;
    }
}

