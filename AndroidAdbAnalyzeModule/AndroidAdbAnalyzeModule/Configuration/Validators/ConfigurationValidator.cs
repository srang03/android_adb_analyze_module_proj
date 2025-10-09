using AndroidAdbAnalyzeModule.Configuration.Models;
using AndroidAdbAnalyzeModule.Core.Exceptions;
using AndroidAdbAnalyzeModule.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AndroidAdbAnalyzeModule.Configuration.Validators;

/// <summary>
/// 설정 파일 검증기
/// </summary>
public sealed class ConfigurationValidator
{
    private readonly ILogger<ConfigurationValidator>? _logger;

    /// <summary>
    /// ConfigurationValidator 생성자
    /// </summary>
    /// <param name="logger">로거 (선택사항)</param>
    public ConfigurationValidator(ILogger<ConfigurationValidator>? logger = null)
    {
        _logger = logger;
    }
    /// <summary>
    /// 설정 검증
    /// </summary>
    /// <param name="config">검증할 설정</param>
    /// <exception cref="ConfigurationValidationException">검증 실패 시</exception>
    public void Validate(LogConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        _logger?.LogInformation("Starting configuration validation: {LogType} v{Version}",
            config.Metadata.LogType, config.ConfigSchemaVersion);

        var errors = new List<string>();

        // 1. 스키마 버전 검증
        _logger?.LogDebug("Validating configuration schema version");
        ValidateSchemaVersion(config.ConfigSchemaVersion, errors);

        // 2. 메타데이터 검증
        _logger?.LogDebug("Validating metadata");
        ValidateMetadata(config.Metadata, errors);

        // 3. 파일 패턴 검증
        _logger?.LogDebug("Validating file patterns");
        ValidateFilePatterns(config.FilePatterns, errors);

        // 4. 섹션 검증
        _logger?.LogDebug("Validating {SectionCount} sections", config.Sections.Count);
        ValidateSections(config.Sections, errors);

        // 5. 파서 검증
        _logger?.LogDebug("Validating {ParserCount} parsers", config.Parsers.Count);
        ValidateParsers(config.Parsers, config.Sections, errors);

        // 6. 성능 설정 검증
        _logger?.LogDebug("Validating performance settings");
        ValidatePerformanceSettings(config.Performance, errors);

        if (errors.Count > 0)
        {
            _logger?.LogError("Configuration validation failed with {ErrorCount} error(s)", errors.Count);
            foreach (var error in errors)
            {
                _logger?.LogError("  - {ValidationError}", error);
            }
            
            var errorMessage = $"Configuration validation failed with {errors.Count} error(s):\n" +
                              string.Join("\n", errors.Select((e, i) => $"{i + 1}. {e}"));
            throw new ConfigurationValidationException(errorMessage, errors);
        }

        _logger?.LogInformation("Configuration validation successful");
    }

    /// <summary>
    /// 디바이스 버전 호환성 검증
    /// </summary>
    /// <param name="deviceInfo">디바이스 정보</param>
    /// <param name="config">로그 설정</param>
    /// <exception cref="ConfigurationValidationException">디바이스 버전이 지원되지 않을 경우</exception>
    public void ValidateDeviceCompatibility(DeviceInfo deviceInfo, LogConfiguration config)
    {
        if (deviceInfo == null)
            throw new ArgumentNullException(nameof(deviceInfo));
        
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // AndroidVersion이 null인 경우 검증 생략 (선택사항)
        if (string.IsNullOrWhiteSpace(deviceInfo.AndroidVersion))
        {
            _logger?.LogDebug("Device Android version not provided, skipping compatibility check");
            return;
        }

        var supportedVersions = config.Metadata.SupportedVersions;
        
        // "*" 는 모든 버전 지원
        if (supportedVersions.Contains("*"))
        {
            _logger?.LogDebug("Configuration supports all Android versions (*)");
            return;
        }

        // 버전 호환성 체크
        if (!supportedVersions.Contains(deviceInfo.AndroidVersion))
        {
            var errorMessage = $"Device Android version '{deviceInfo.AndroidVersion}' is not supported by this configuration. " +
                              $"Supported versions: {string.Join(", ", supportedVersions)}. " +
                              $"Configuration: {config.Metadata.LogType}";
            
            _logger?.LogError("Device version incompatibility: Device={DeviceVersion}, Supported=[{SupportedVersions}]",
                deviceInfo.AndroidVersion, string.Join(", ", supportedVersions));
            
            throw new ConfigurationValidationException(errorMessage);
        }

        _logger?.LogDebug("Device Android version '{DeviceVersion}' is compatible with configuration",
            deviceInfo.AndroidVersion);
    }

    /// <summary>
    /// 설정 스키마 버전 검증
    /// </summary>
    private void ValidateSchemaVersion(string version, List<string> errors)
    {
        var supportedVersions = new[] { "1.0" };
        
        if (string.IsNullOrWhiteSpace(version))
        {
            errors.Add("ConfigSchemaVersion is required");
            return;
        }

        if (!supportedVersions.Contains(version))
        {
            errors.Add($"Unsupported configuration schema version: {version}. " +
                      $"Supported versions: {string.Join(", ", supportedVersions)}");
        }
    }

    /// <summary>
    /// 메타데이터 검증
    /// </summary>
    private void ValidateMetadata(ConfigMetadata metadata, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(metadata.LogType))
            errors.Add("Metadata.LogType is required");

        if (metadata.SupportedVersions.Count == 0)
            errors.Add("Metadata.SupportedVersions is required (at least one version or \"*\" for all)");

        // 빈 버전 문자열 체크
        for (int i = 0; i < metadata.SupportedVersions.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(metadata.SupportedVersions[i]))
                errors.Add($"Metadata.SupportedVersions[{i}] is empty");
        }

        if (string.IsNullOrWhiteSpace(metadata.DisplayName))
            errors.Add("Metadata.DisplayName is required");
    }

    /// <summary>
    /// 파일 패턴 검증
    /// </summary>
    private void ValidateFilePatterns(IReadOnlyList<string> patterns, List<string> errors)
    {
        if (patterns.Count == 0)
            errors.Add("At least one file pattern is required");

        for (int i = 0; i < patterns.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(patterns[i]))
                errors.Add($"File pattern at index {i} is empty");
        }
    }

    /// <summary>
    /// 섹션 검증
    /// </summary>
    private void ValidateSections(IReadOnlyList<SectionConfig> sections, List<string> errors)
    {
        if (sections.Count == 0)
        {
            errors.Add("At least one section is required");
            return;
        }

        var sectionIds = new HashSet<string>();

        foreach (var section in sections)
        {
            // ID 중복 체크
            if (!sectionIds.Add(section.Id))
            {
                errors.Add($"Duplicate section ID: {section.Id}");
            }

            // 필수 필드 체크
            if (string.IsNullOrWhiteSpace(section.Id))
                errors.Add("Section ID is required");

            if (string.IsNullOrWhiteSpace(section.Name))
                errors.Add($"Section '{section.Id}': Name is required");

            if (string.IsNullOrWhiteSpace(section.StartMarker))
                errors.Add($"Section '{section.Id}': StartMarker is required");

            if (string.IsNullOrWhiteSpace(section.EndMarker))
                errors.Add($"Section '{section.Id}': EndMarker is required");

            // 마커 타입 검증
            if (section.MarkerType != "text" && 
                section.MarkerType != "regex" && 
                section.MarkerType != "lineNumber")
            {
                errors.Add($"Section '{section.Id}': Invalid MarkerType '{section.MarkerType}'. Must be 'text', 'regex', or 'lineNumber'");
            }

            // Regex 마커 타입인 경우 패턴 검증
            if (section.MarkerType == "regex")
            {
                ValidateRegexPattern(section.StartMarker, $"Section '{section.Id}' StartMarker", errors);
                ValidateRegexPattern(section.EndMarker, $"Section '{section.Id}' EndMarker", errors);
            }
        }
    }

    /// <summary>
    /// 파서 검증
    /// </summary>
    private void ValidateParsers(IReadOnlyList<ParserConfig> parsers, IReadOnlyList<SectionConfig> sections, List<string> errors)
    {
        if (parsers.Count == 0)
        {
            errors.Add("At least one parser is required");
            return;
        }

        var parserIds = new HashSet<string>();
        var sectionIds = new HashSet<string>(sections.Select(s => s.Id));

        foreach (var parser in parsers)
        {
            // ID 중복 체크
            if (!parserIds.Add(parser.Id))
            {
                errors.Add($"Duplicate parser ID: {parser.Id}");
            }

            // 필수 필드 체크
            if (string.IsNullOrWhiteSpace(parser.Id))
                errors.Add("Parser ID is required");

            if (string.IsNullOrWhiteSpace(parser.Name))
                errors.Add($"Parser '{parser.Id}': Name is required");

            if (parser.TargetSections.Count == 0)
                errors.Add($"Parser '{parser.Id}': At least one target section is required");

            // 대상 섹션이 존재하는지 확인
            foreach (var targetSection in parser.TargetSections)
            {
                if (!sectionIds.Contains(targetSection))
                {
                    errors.Add($"Parser '{parser.Id}': Target section '{targetSection}' not found in section definitions");
                }
            }

            // 라인 패턴 검증
            if (parser.LinePatterns.Count == 0)
                errors.Add($"Parser '{parser.Id}': At least one line pattern is required");

            ValidateLinePatterns(parser.LinePatterns, parser.Id, errors);
        }
    }

    /// <summary>
    /// 라인 패턴 검증
    /// </summary>
    private void ValidateLinePatterns(IReadOnlyList<LinePatternConfig> patterns, string parserId, List<string> errors)
    {
        var patternIds = new HashSet<string>();

        foreach (var pattern in patterns)
        {
            // ID 중복 체크
            if (!string.IsNullOrWhiteSpace(pattern.Id) && !patternIds.Add(pattern.Id))
            {
                errors.Add($"Parser '{parserId}': Duplicate pattern ID '{pattern.Id}'");
            }

            // Regex 패턴 검증
            if (string.IsNullOrWhiteSpace(pattern.Regex))
            {
                errors.Add($"Parser '{parserId}', Pattern '{pattern.Id}': Regex is required");
            }
            else
            {
                ValidateRegexPattern(pattern.Regex, $"Parser '{parserId}', Pattern '{pattern.Id}'", errors);
            }

            // 필드 정의 검증
            if (pattern.Fields.Count == 0)
            {
                errors.Add($"Parser '{parserId}', Pattern '{pattern.Id}': At least one field definition is required");
            }

            foreach (var field in pattern.Fields)
            {
                if (field.Value.Group < 0)
                {
                    errors.Add($"Parser '{parserId}', Pattern '{pattern.Id}', Field '{field.Key}': Group number must be >= 0");
                }

                // 필드 타입 검증
                var validTypes = new[] { "string", "int", "long", "double", "datetime", "hex", "bool" };
                if (!validTypes.Contains(field.Value.Type.ToLower()))
                {
                    errors.Add($"Parser '{parserId}', Pattern '{pattern.Id}', Field '{field.Key}': Invalid type '{field.Value.Type}'. Must be one of: {string.Join(", ", validTypes)}");
                }
            }

            // 이벤트 타입 검증
            if (string.IsNullOrWhiteSpace(pattern.EventType))
            {
                errors.Add($"Parser '{parserId}', Pattern '{pattern.Id}': EventType is required");
            }
        }
    }

    /// <summary>
    /// 정규식 패턴 검증
    /// </summary>
    private void ValidateRegexPattern(string pattern, string context, List<string> errors)
    {
        try
        {
            // Regex 생성 테스트
            _ = new Regex(pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException ex)
        {
            errors.Add($"{context}: Invalid regex pattern - {ex.Message}");
        }
    }

    /// <summary>
    /// 성능 설정 검증
    /// </summary>
    private void ValidatePerformanceSettings(PerformanceSettings settings, List<string> errors)
    {
        if (settings.MaxFileSizeMB <= 0)
            errors.Add("Performance.MaxFileSizeMB must be greater than 0");

        if (settings.TimeoutSeconds <= 0)
            errors.Add("Performance.TimeoutSeconds must be greater than 0");

        if (settings.BufferSizeKB <= 0)
            errors.Add("Performance.BufferSizeKB must be greater than 0");
    }
}

