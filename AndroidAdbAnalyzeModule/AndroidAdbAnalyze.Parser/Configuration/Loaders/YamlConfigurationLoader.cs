using AndroidAdbAnalyze.Parser.Configuration.Models;
using AndroidAdbAnalyze.Parser.Core.Exceptions;
using AndroidAdbAnalyze.Parser.Core.Interfaces;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AndroidAdbAnalyze.Parser.Configuration.Loaders;

/// <summary>
/// YAML 설정 파일 로더
/// </summary>
public sealed class YamlConfigurationLoader : IConfigurationLoader<LogConfiguration>
{
    private readonly string _configPath;
    private LogConfiguration? _currentConfiguration;
    private volatile bool _isReloading = false;
    private readonly object _lock = new();
    private readonly ILogger<YamlConfigurationLoader>? _logger;

    /// <summary>
    /// 설정 변경 이벤트
    /// </summary>
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    /// <summary>
    /// 현재 로드된 설정
    /// </summary>
    public LogConfiguration? CurrentConfiguration
    {
        get
        {
            lock (_lock)
            {
                return _currentConfiguration;
            }
        }
    }

    /// <summary>
    /// YamlConfigurationLoader 생성자
    /// </summary>
    /// <param name="configPath">설정 파일 경로</param>
    /// <param name="logger">로거 (선택사항)</param>
    public YamlConfigurationLoader(string configPath, ILogger<YamlConfigurationLoader>? logger = null)
    {
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        _logger = logger;
    }

    /// <summary>
    /// 설정 파일 동기 로드
    /// </summary>
    public LogConfiguration Load(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("Config path cannot be null or empty", nameof(configPath));

        if (!File.Exists(configPath))
        {
            _logger?.LogError("Configuration file not found: {ConfigPath}", configPath);
            throw new ConfigurationNotFoundException(configPath);
        }

        _logger?.LogInformation("Loading configuration from: {ConfigPath}", configPath);
        
        try
        {
            var yamlContent = File.ReadAllText(configPath);
            _logger?.LogDebug("Read {CharCount} characters from configuration file", yamlContent.Length);
            
            var config = DeserializeYaml(yamlContent);

            lock (_lock)
            {
                _currentConfiguration = config;
            }

            _logger?.LogInformation("Configuration loaded successfully: {LogType} v{Version}",
                config.Metadata.LogType, config.ConfigSchemaVersion);

            return config;
        }
        catch (ConfigurationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load configuration from: {ConfigPath}", configPath);
            throw new ConfigurationLoadException(configPath, "Failed to load configuration", ex);
        }
    }

    /// <summary>
    /// 설정 파일 비동기 로드
    /// </summary>
    public async Task<LogConfiguration> LoadAsync(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("Config path cannot be null or empty", nameof(configPath));

        if (!File.Exists(configPath))
        {
            _logger?.LogError("Configuration file not found: {ConfigPath}", configPath);
            throw new ConfigurationNotFoundException(configPath);
        }

        _logger?.LogInformation("Loading configuration asynchronously from: {ConfigPath}", configPath);
        
        try
        {
            var yamlContent = await File.ReadAllTextAsync(configPath);
            _logger?.LogDebug("Read {CharCount} characters from configuration file", yamlContent.Length);
            
            var config = DeserializeYaml(yamlContent);

            lock (_lock)
            {
                _currentConfiguration = config;
            }

            _logger?.LogInformation("Configuration loaded successfully: {LogType} v{Version}",
                config.Metadata.LogType, config.ConfigSchemaVersion);

            return config;
        }
        catch (ConfigurationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load configuration from: {ConfigPath}", configPath);
            throw new ConfigurationLoadException(configPath, "Failed to load configuration", ex);
        }
    }

    /// <summary>
    /// 설정 재로드
    /// 파싱 작업 중에는 호출 불가
    /// </summary>
    public void Reload()
    {
        if (_isReloading)
        {
            _logger?.LogWarning("Reload already in progress, ignoring request");
            throw new InvalidOperationException("Reload already in progress");
        }

        _logger?.LogInformation("Reloading configuration from: {ConfigPath}", _configPath);
        
        _isReloading = true;
        try
        {
            var newConfig = Load(_configPath);

            lock (_lock)
            {
                _currentConfiguration = newConfig;
            }

            _logger?.LogInformation("Configuration reloaded successfully");
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(newConfig));
        }
        finally
        {
            _isReloading = false;
        }
    }

    /// <summary>
    /// YAML 문자열을 LogConfiguration으로 역직렬화
    /// </summary>
    private LogConfiguration DeserializeYaml(string yamlContent)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<LogConfiguration>(yamlContent);

            if (config == null)
                throw new ConfigurationValidationException("Configuration is null after deserialization");

            // 설정 검증 수행
            _logger?.LogDebug("Validating deserialized configuration");
            var validator = new Configuration.Validators.ConfigurationValidator(null);
            validator.Validate(config);

            return config;
        }
        catch (Exception ex) when (ex is not ConfigurationException)
        {
            throw new ConfigurationValidationException($"YAML deserialization failed: {ex.Message}", ex);
        }
    }
}

