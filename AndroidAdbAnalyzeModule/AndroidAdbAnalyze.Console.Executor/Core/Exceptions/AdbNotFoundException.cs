namespace AndroidAdbAnalyze.Console.Executor.Core.Exceptions;

/// <summary>
/// ADB 실행 파일을 찾을 수 없는 경우 발생하는 예외
/// </summary>
public class AdbNotFoundException : AndroidAdbAnalyzeException
{
    public AdbNotFoundException(string? searchedPath = null)
        : base(
            "ADB 실행 파일을 찾을 수 없습니다.",
            ExitCode.AdbNotFound)
    {
        UserFriendlyHelp = @"
해결 방법:
1. Android Platform Tools 설치
   https://developer.android.com/tools/releases/platform-tools

2. PATH 환경 변수에 ADB 경로 추가
   - Windows: 시스템 속성 > 환경 변수 > Path에 추가
   - Linux/Mac: ~/.bashrc 또는 ~/.zshrc에 export PATH=$PATH:/path/to/platform-tools

3. 또는 appsettings.json에서 ADB 경로 직접 지정
   ""Adb"": {
     ""ExecutablePath"": ""C:\\platform-tools\\adb.exe""
   }
";

        if (!string.IsNullOrEmpty(searchedPath))
        {
            UserFriendlyHelp += $"\n검색한 경로: {searchedPath}";
        }
    }
}

