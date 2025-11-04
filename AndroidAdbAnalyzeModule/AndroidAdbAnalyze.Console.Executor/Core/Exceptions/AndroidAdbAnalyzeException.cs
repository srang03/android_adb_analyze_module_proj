namespace AndroidAdbAnalyze.Console.Executor.Core.Exceptions;

/// <summary>
/// AndroidAdbAnalyze 애플리케이션의 최상위 예외
/// </summary>
public class AndroidAdbAnalyzeException : Exception
{
    /// <summary>
    /// 종료 코드
    /// </summary>
    public ExitCode ExitCode { get; }
    
    /// <summary>
    /// 사용자 친화적 도움말 메시지
    /// </summary>
    public virtual string UserFriendlyHelp { get; protected set; } = string.Empty;

    public AndroidAdbAnalyzeException(
        string message, 
        ExitCode exitCode, 
        Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }
}

