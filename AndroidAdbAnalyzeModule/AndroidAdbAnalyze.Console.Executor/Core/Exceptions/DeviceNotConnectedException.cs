namespace AndroidAdbAnalyze.Console.Executor.Core.Exceptions;

/// <summary>
/// 연결된 디바이스가 없는 경우 발생하는 예외
/// </summary>
public class DeviceNotConnectedException : AndroidAdbAnalyzeException
{
    public DeviceNotConnectedException()
        : base(
            "연결된 ADB 디바이스가 없습니다.",
            ExitCode.NoDeviceConnected)
    {
        UserFriendlyHelp = @"
확인 사항:
1. USB 케이블 연결 확인
2. USB 디버깅 활성화 (설정 > 개발자 옵션)
3. 디바이스에서 ADB 권한 승인
4. 'adb devices' 명령으로 연결 확인

무선 디버깅 (WiFi):
1. 디바이스와 PC가 같은 네트워크에 연결
2. adb pair <IP>:<PORT> (최초 1회)
3. adb connect <IP>:<PORT>

참고:
- USB 디버깅이 활성화되어 있어야 합니다
- 일부 제조사는 추가 설정이 필요할 수 있습니다
";
    }
}

