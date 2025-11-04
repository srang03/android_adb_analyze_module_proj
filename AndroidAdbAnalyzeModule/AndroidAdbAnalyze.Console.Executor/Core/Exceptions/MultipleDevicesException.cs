namespace AndroidAdbAnalyze.Console.Executor.Core.Exceptions;

/// <summary>
/// 다중 디바이스가 연결된 경우 발생하는 예외
/// </summary>
public class MultipleDevicesException : AndroidAdbAnalyzeException
{
    public IReadOnlyList<string> DeviceSerials { get; }

    public MultipleDevicesException(IReadOnlyList<string> deviceSerials)
        : base(
            $"{deviceSerials.Count}개의 디바이스가 연결되어 있습니다.",
            ExitCode.MultipleDevicesConnected)
    {
        DeviceSerials = deviceSerials;
        
        var deviceList = string.Join("\n", deviceSerials.Select(s => $"  - {s}"));
        
        UserFriendlyHelp = $@"
연결된 디바이스:
{deviceList}

현재 버전은 단일 디바이스만 지원합니다.
하나의 디바이스만 남겨주세요:

1. USB 디바이스 연결 해제
2. 무선 디바이스 연결 해제:
   adb disconnect <IP>:<PORT>

3. 특정 디바이스만 사용 (향후 버전에서 지원 예정):
   --device-serial <SERIAL>
";
    }
}

