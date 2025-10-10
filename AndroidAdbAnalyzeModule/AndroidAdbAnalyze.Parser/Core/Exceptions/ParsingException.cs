namespace AndroidAdbAnalyze.Parser.Core.Exceptions;

/// <summary>
/// 파싱 관련 예외 기본 클래스
/// </summary>
public class ParsingException : Exception
{
    /// <summary>
    /// 지정된 오류 메시지를 사용하여 <see cref="ParsingException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    public ParsingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 지정된 오류 메시지와 내부 예외를 사용하여 <see cref="ParsingException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    /// <param name="innerException">현재 예외의 원인이 되는 내부 예외입니다.</param>
    public ParsingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 로그 파일 크기 초과 시 발생
/// </summary>
public sealed class LogFileTooLargeException : ParsingException
{
    /// <summary>
    /// 크기 초과가 발생한 로그 파일의 경로입니다.
    /// </summary>
    public string FilePath { get; }
    
    /// <summary>
    /// 실제 로그 파일의 크기(바이트 단위)입니다.
    /// </summary>
    public long FileSizeBytes { get; }
    
    /// <summary>
    /// 허용된 최대 파일 크기(바이트 단위)입니다.
    /// </summary>
    public long MaxSizeBytes { get; }

    /// <summary>
    /// 지정된 파일 경로, 실제 크기, 최대 허용 크기를 사용하여 <see cref="LogFileTooLargeException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="filePath">크기 초과가 발생한 로그 파일의 경로입니다.</param>
    /// <param name="fileSizeBytes">실제 로그 파일의 크기(바이트 단위)입니다.</param>
    /// <param name="maxSizeBytes">허용된 최대 파일 크기(바이트 단위)입니다.</param>
    public LogFileTooLargeException(string filePath, long fileSizeBytes, long maxSizeBytes)
        : base($"Log file '{filePath}' is too large ({fileSizeBytes / 1024.0 / 1024.0:F2} MB). Maximum allowed: {maxSizeBytes / 1024.0 / 1024.0:F2} MB")
    {
        FilePath = filePath;
        FileSizeBytes = fileSizeBytes;
        MaxSizeBytes = maxSizeBytes;
    }
}

/// <summary>
/// 파싱 중 치명적 에러 발생 시
/// </summary>
public sealed class CriticalParsingException : ParsingException
{
    /// <summary>
    /// 오류가 발생한 로그 파일의 라인 번호입니다.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// 지정된 오류 메시지와 라인 번호를 사용하여 <see cref="CriticalParsingException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    /// <param name="lineNumber">오류가 발생한 로그 파일의 라인 번호입니다.</param>
    public CriticalParsingException(string message, int lineNumber)
        : base($"Critical parsing error at line {lineNumber}: {message}")
    {
        LineNumber = lineNumber;
    }

    /// <summary>
    /// 지정된 오류 메시지, 라인 번호, 내부 예외를 사용하여 <see cref="CriticalParsingException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    /// <param name="lineNumber">오류가 발생한 로그 파일의 라인 번호입니다.</param>
    /// <param name="innerException">현재 예외의 원인이 되는 내부 예외입니다.</param>
    public CriticalParsingException(string message, int lineNumber, Exception innerException)
        : base($"Critical parsing error at line {lineNumber}: {message}", innerException)
    {
        LineNumber = lineNumber;
    }
}

