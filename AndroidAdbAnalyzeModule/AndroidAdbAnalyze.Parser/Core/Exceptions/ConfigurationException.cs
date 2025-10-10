namespace AndroidAdbAnalyze.Parser.Core.Exceptions;

/// <summary>
/// 설정 파일 관련 예외 기본 클래스
/// </summary>
public class ConfigurationException : Exception
{
    /// <summary>
    /// 지정된 오류 메시지를 사용하여 <see cref="ConfigurationException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    public ConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 지정된 오류 메시지와 내부 예외를 사용하여 <see cref="ConfigurationException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    /// <param name="innerException">현재 예외의 원인이 되는 내부 예외입니다.</param>
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
    /// <summary>
    /// 찾을 수 없는 설정 파일의 경로입니다.
    /// </summary>
    public string ConfigPath { get; }

    /// <summary>
    /// 지정된 설정 파일 경로를 사용하여 <see cref="ConfigurationNotFoundException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="configPath">찾을 수 없는 설정 파일의 경로입니다.</param>
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
    /// <summary>
    /// 검증 중 발생한 오류 메시지 목록입니다.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>
    /// 지정된 오류 메시지를 사용하여 <see cref="ConfigurationValidationException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    public ConfigurationValidationException(string message)
        : base(message)
    {
        ValidationErrors = Array.Empty<string>();
    }

    /// <summary>
    /// 지정된 오류 메시지와 검증 오류 목록을 사용하여 <see cref="ConfigurationValidationException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    /// <param name="validationErrors">검증 중 발생한 오류 메시지 컬렉션입니다.</param>
    public ConfigurationValidationException(string message, IEnumerable<string> validationErrors)
        : base(message)
    {
        ValidationErrors = validationErrors.ToList();
    }

    /// <summary>
    /// 지정된 오류 메시지와 내부 예외를 사용하여 <see cref="ConfigurationValidationException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="message">예외의 원인을 설명하는 오류 메시지입니다.</param>
    /// <param name="innerException">현재 예외의 원인이 되는 내부 예외입니다.</param>
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
    /// <summary>
    /// 로드에 실패한 설정 파일의 경로입니다.
    /// </summary>
    public string ConfigPath { get; }

    /// <summary>
    /// 지정된 설정 파일 경로와 오류 메시지를 사용하여 <see cref="ConfigurationLoadException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="configPath">로드에 실패한 설정 파일의 경로입니다.</param>
    /// <param name="message">로드 실패의 원인을 설명하는 오류 메시지입니다.</param>
    public ConfigurationLoadException(string configPath, string message)
        : base($"Failed to load configuration from '{configPath}': {message}")
    {
        ConfigPath = configPath;
    }

    /// <summary>
    /// 지정된 설정 파일 경로, 오류 메시지, 내부 예외를 사용하여 <see cref="ConfigurationLoadException"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="configPath">로드에 실패한 설정 파일의 경로입니다.</param>
    /// <param name="message">로드 실패의 원인을 설명하는 오류 메시지입니다.</param>
    /// <param name="innerException">현재 예외의 원인이 되는 내부 예외입니다.</param>
    public ConfigurationLoadException(string configPath, string message, Exception innerException)
        : base($"Failed to load configuration from '{configPath}': {message}", innerException)
    {
        ConfigPath = configPath;
    }
}

