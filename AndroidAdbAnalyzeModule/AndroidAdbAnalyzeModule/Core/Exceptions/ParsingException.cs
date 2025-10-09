namespace AndroidAdbAnalyzeModule.Core.Exceptions;

/// <summary>
/// 파싱 관련 예외 기본 클래스
/// </summary>
public class ParsingException : Exception
{
    public ParsingException(string message)
        : base(message)
    {
    }

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
    public string FilePath { get; }
    public long FileSizeBytes { get; }
    public long MaxSizeBytes { get; }

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
    public int LineNumber { get; }

    public CriticalParsingException(string message, int lineNumber)
        : base($"Critical parsing error at line {lineNumber}: {message}")
    {
        LineNumber = lineNumber;
    }

    public CriticalParsingException(string message, int lineNumber, Exception innerException)
        : base($"Critical parsing error at line {lineNumber}: {message}", innerException)
    {
        LineNumber = lineNumber;
    }
}

