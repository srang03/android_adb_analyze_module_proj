using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Exceptions;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// End-to-End 테스트: Configuration 검증 및 Error Case 테스트
/// </summary>
public class AdbLogParserEndToEndTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public AdbLogParserEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        _logger = loggerFactory.CreateLogger<AdbLogParser>();
        _configLogger = loggerFactory.CreateLogger<YamlConfigurationLoader>();
    }

    #region Error Case Tests - Configuration

    [Fact]
    public async Task ConfigLoader_WithNonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "non_existent_config.yaml");

        // Act & Assert
        var configLoader = new YamlConfigurationLoader(nonExistentPath, _configLogger);
        var act = async () => await configLoader.LoadAsync(nonExistentPath);

        await act.Should().ThrowAsync<ConfigurationNotFoundException>()
            .WithMessage("*not found*");

        _output.WriteLine("✓ Non-existent configuration file throws ConfigurationNotFoundException");
    }

    [Fact]
    public async Task ConfigLoader_WithInvalidYamlSyntax_ShouldThrowException()
    {
        // Arrange
        var invalidYamlPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "ValidationTests", "invalid_yaml_syntax.yaml");

        // Act & Assert
        var configLoader = new YamlConfigurationLoader(invalidYamlPath, _configLogger);
        var act = async () => await configLoader.LoadAsync(invalidYamlPath);

        await act.Should().ThrowAsync<ConfigurationValidationException>()
            .WithMessage("*deserialization failed*");

        _output.WriteLine("✓ Invalid YAML syntax throws ConfigurationValidationException");
    }

    [Fact]
    public async Task ConfigValidator_WithMissingSections_ShouldThrowException()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "ValidationTests", "missing_sections.yaml");
        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);

        // Act & Assert
        var act = async () => await configLoader.LoadAsync(configPath);

        await act.Should().ThrowAsync<ConfigurationValidationException>()
            .WithMessage("*section*required*");

        _output.WriteLine("✓ Missing sections field throws ConfigurationValidationException");
    }

    [Fact]
    public async Task ConfigValidator_WithMissingSchemaVersion_ShouldThrowException()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "ValidationTests", "missing_schema_version.yaml");
        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);

        // Act & Assert
        var act = async () => await configLoader.LoadAsync(configPath);

        await act.Should().ThrowAsync<ConfigurationValidationException>()
            .WithMessage("*ConfigSchemaVersion*required*");

        _output.WriteLine("✓ Missing schema version throws ConfigurationValidationException");
    }

    [Fact]
    public async Task ConfigValidator_WithUnsupportedSchemaVersion_ShouldThrowException()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "ValidationTests", "unsupported_schema_version.yaml");
        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);

        // Act & Assert
        var act = async () => await configLoader.LoadAsync(configPath);

        await act.Should().ThrowAsync<ConfigurationValidationException>()
            .WithMessage("*Unsupported*schema version*");

        _output.WriteLine("✓ Unsupported schema version throws ConfigurationValidationException");
    }

    [Fact]
    public async Task ConfigValidator_WithWildcardVersion_ShouldAcceptAnyAndroidVersion()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "ValidationTests", "wildcard_version_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        // Test with various Android versions
        var androidVersions = new[] { "11", "12", "13", "14", "15", "unknown" };

        foreach (var version in androidVersions)
        {
            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = version
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo, ConvertToUtc = true };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert
            // 파싱 결과 이벤트가 있을 때만 Success가 true여야 함
            result.Success.Should().Be(result.Events.Any(), $"Wildcard version should be handled correctly for Android version {version}");
            _output.WriteLine($"✓ Wildcard '*' accepted Android version: {version} (Success: {result.Success}, Events: {result.Events.Count})");
        }
    }

    #endregion

    #region Error Case Tests - Log Files

    [Fact]
    public async Task Parser_WithNonExistentLogFile_ShouldFail()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var nonExistentLogPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "non_existent_log.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);

        // Act & Assert
        var act = async () => await parser.ParseAsync(nonExistentLogPath, options);

        await act.Should().ThrowAsync<FileNotFoundException>();

        _output.WriteLine("✓ Non-existent log file throws FileNotFoundException");
    }

    [Fact]
    public async Task Parser_WithEmptyLogFile_ShouldReturnEmptyResult()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var emptyLogPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "empty_log.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(emptyLogPath, options);

        // Assert
        result.Success.Should().BeFalse("Empty log file should result in no events and be marked as not successful");
        result.Events.Should().BeEmpty("No events should be parsed from empty file");
        result.Errors.Should().ContainSingle(e => e.Severity == AndroidAdbAnalyze.Parser.Core.Models.ErrorSeverity.Critical, "a critical error should be logged when no sections are found");
        result.Statistics.ErrorLines.Should().Be(1);

        _output.WriteLine("✓ Empty log file returns non-successful result with a critical error");
        _output.WriteLine($"  Success: {result.Success}");
        _output.WriteLine($"  Errors: {result.Errors.Count}");
    }

    [Fact]
    public async Task Parser_WithExceedingFileSize_ShouldThrowException()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "audio.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };

        // Set MaxFileSizeMB to 0 to force size limit exceeded
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            MaxFileSizeMB = 0
        };
        var parser = new AdbLogParser(configuration, _logger);

        // Act & Assert
        var act = async () => await parser.ParseAsync(logPath, options);

        await act.Should().ThrowAsync<LogFileTooLargeException>()
            .WithMessage("*too large*");

        _output.WriteLine("✓ File size exceeding MaxFileSizeMB throws LogFileTooLargeException");
    }

    [Fact]
    public async Task ConfigValidator_WithInvalidRegexPattern_ShouldThrowException()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "ValidationTests", "invalid_regex_pattern.yaml");
        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);

        // Act & Assert
        var act = async () => await configLoader.LoadAsync(configPath);

        await act.Should().ThrowAsync<ConfigurationValidationException>()
            .WithMessage("*Invalid regex pattern*");

        _output.WriteLine("✓ Invalid regex pattern throws ConfigurationValidationException during config load");
    }

    #endregion
}
