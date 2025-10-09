namespace AndroidAdbAnalyzeModule.Core.Exceptions;

/// <summary>
/// 설정 파일 관련 예외 기본 클래스
/// </summary>
public class ConfigurationException : Exception
{
    public ConfigurationException(string message)
        : base(message)
    {
    }

    public ConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 설정 파일을 찾을 수 없을 때 발생
/// </summary>
public sealed class ConfigurationNotFoundException : ConfigurationException
{
    public string ConfigPath { get; }

    public ConfigurationNotFoundException(string configPath)
        : base($"Configuration file not found: {configPath}")
    {
        ConfigPath = configPath;
    }
}

/// <summary>
/// 설정 파일 검증 실패 시 발생
/// </summary>
public sealed class ConfigurationValidationException : ConfigurationException
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public ConfigurationValidationException(string message)
        : base(message)
    {
        ValidationErrors = Array.Empty<string>();
    }

    public ConfigurationValidationException(string message, IEnumerable<string> validationErrors)
        : base(message)
    {
        ValidationErrors = validationErrors.ToList();
    }

    public ConfigurationValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationErrors = Array.Empty<string>();
    }
}

/// <summary>
/// 설정 파일 로드 중 예외 발생
/// </summary>
public sealed class ConfigurationLoadException : ConfigurationException
{
    public string ConfigPath { get; }

    public ConfigurationLoadException(string configPath, string message)
        : base($"Failed to load configuration from '{configPath}': {message}")
    {
        ConfigPath = configPath;
    }

    public ConfigurationLoadException(string configPath, string message, Exception innerException)
        : base($"Failed to load configuration from '{configPath}': {message}", innerException)
    {
        ConfigPath = configPath;
    }
}

