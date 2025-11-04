using AndroidAdbAnalyze.Analysis.Models.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AndroidAdbAnalyze.Analysis.Configuration;

/// <summary>
/// YAML 파일에서 아티팩트 탐지 설정을 로드하는 유틸리티
/// </summary>
/// <remarks>
/// 외부 YAML 파일을 통해 런타임에 설정을 변경할 수 있습니다.
/// 파일이 없거나 파싱 오류 발생 시 기본값(ConfigurationProvider.GetDefault())을 반환합니다.
/// </remarks>
public static class YamlConfigurationLoader
{
    /// <summary>
    /// YAML 파일에서 설정 로드
    /// </summary>
    /// <param name="filePath">YAML 파일 경로 (상대 또는 절대 경로)</param>
    /// <param name="logger">로거 (optional)</param>
    /// <returns>
    /// 로드된 설정 객체. 파일이 없거나 오류 발생 시 기본값 반환.
    /// </returns>
    /// <remarks>
    /// 안전한 fallback 전략:
    /// 1. YAML 파일 로드 시도
    /// 2. 실패 시 기본값 반환 (ConfigurationProvider.GetDefault())
    /// 3. 로그를 통해 상태 알림
    /// 
    /// 사용 예시:
    /// <code>
    /// var config = YamlConfigurationLoader.LoadFromFile(
    ///     "Configs/artifact-detection-config.yaml", 
    ///     logger);
    /// </code>
    /// </remarks>
    public static ArtifactDetectionConfig LoadFromFile(
        string filePath,
        ILogger? logger = null)
    {
        try
        {
            // 파일 존재 확인
            if (!File.Exists(filePath))
            {
                logger?.LogWarning(
                    "[YamlConfigurationLoader] 설정 파일을 찾을 수 없습니다: {FilePath}. 기본값을 사용합니다.",
                    filePath);
                return ConfigurationProvider.GetDefault();
            }

            // YAML 파일 읽기
            var yamlContent = File.ReadAllText(filePath);

            // YamlDotNet Deserializer 생성 (camelCase 네이밍 컨벤션)
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties() // 알 수 없는 속성 무시 (유연성)
                .Build();

            // YAML → ArtifactDetectionConfig 변환
            var config = deserializer.Deserialize<ArtifactDetectionConfig>(yamlContent);

            if (config == null)
            {
                logger?.LogWarning(
                    "[YamlConfigurationLoader] 설정 파일 파싱 결과가 null입니다: {FilePath}. 기본값을 사용합니다.",
                    filePath);
                return ConfigurationProvider.GetDefault();
            }

            // 유효성 검증
            ValidateConfiguration(config, logger);

            logger?.LogInformation(
                "[YamlConfigurationLoader] 설정 파일 로드 성공: {FilePath} " +
                "(가중치: {WeightCount}개, 전략: {StrategyCount}개)",
                filePath,
                (config.ArtifactWeights?.Session?.Count ?? 0) + (config.ArtifactWeights?.Capture?.Count ?? 0),
                config.Strategies?.Count ?? 0);

            return config;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            logger?.LogError(ex,
                "[YamlConfigurationLoader] YAML 파싱 오류: {FilePath}. 기본값을 사용합니다. " +
                "오류: {Message}",
                filePath, ex.Message);
            return ConfigurationProvider.GetDefault();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                "[YamlConfigurationLoader] 설정 파일 로드 중 예외 발생: {FilePath}. 기본값을 사용합니다.",
                filePath);
            return ConfigurationProvider.GetDefault();
        }
    }

    /// <summary>
    /// YAML 파일에서 설정 로드 (Optional 패턴)
    /// </summary>
    /// <param name="filePath">YAML 파일 경로</param>
    /// <param name="logger">로거 (optional)</param>
    /// <returns>
    /// (Success, Config) 튜플.
    /// Success=true이면 YAML 로드 성공, false이면 기본값 사용.
    /// </returns>
    /// <remarks>
    /// 호출자가 YAML 로드 성공 여부를 명확히 알고 싶을 때 사용합니다.
    /// </remarks>
    public static (bool Success, ArtifactDetectionConfig Config) TryLoadFromFile(
        string filePath,
        ILogger? logger = null)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                logger?.LogWarning(
                    "[YamlConfigurationLoader] 설정 파일을 찾을 수 없습니다: {FilePath}",
                    filePath);
                return (false, ConfigurationProvider.GetDefault());
            }

            var yamlContent = File.ReadAllText(filePath);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<ArtifactDetectionConfig>(yamlContent);

            if (config == null)
            {
                return (false, ConfigurationProvider.GetDefault());
            }

            ValidateConfiguration(config, logger);

            logger?.LogInformation(
                "[YamlConfigurationLoader] ✅ 설정 파일 로드 성공: {FilePath}",
                filePath);

            return (true, config);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                "[YamlConfigurationLoader] 설정 파일 로드 실패: {FilePath}",
                filePath);
            return (false, ConfigurationProvider.GetDefault());
        }
    }

    /// <summary>
    /// 설정 유효성 검증
    /// </summary>
    /// <remarks>
    /// 필수 필드 및 논리적 오류를 검사합니다.
    /// 경고만 로그하고 예외는 발생시키지 않습니다 (안전한 fallback).
    /// </remarks>
    private static void ValidateConfiguration(
        ArtifactDetectionConfig config,
        ILogger? logger)
    {
        // 아티팩트 가중치 검증
        if (config.ArtifactWeights?.Session == null || config.ArtifactWeights.Session.Count == 0)
        {
            logger?.LogWarning(
                "[YamlConfigurationLoader] Session 가중치가 정의되지 않았습니다. " +
                "세션 완전성 점수 계산에 영향을 줄 수 있습니다.");
        }

        if (config.ArtifactWeights?.Capture == null || config.ArtifactWeights.Capture.Count == 0)
        {
            logger?.LogWarning(
                "[YamlConfigurationLoader] Capture 가중치가 정의되지 않았습니다. " +
                "촬영 탐지 점수 계산에 영향을 줄 수 있습니다.");
        }

        // 전략 검증
        if (config.Strategies == null || config.Strategies.Count == 0)
        {
            logger?.LogWarning(
                "[YamlConfigurationLoader] Strategy가 정의되지 않았습니다. " +
                "촬영 탐지가 실패할 수 있습니다.");
        }
        else
        {
            // base_pattern 전략 필수 확인
            if (!config.Strategies.ContainsKey("base_pattern"))
            {
                logger?.LogWarning(
                    "[YamlConfigurationLoader] 'base_pattern' 전략이 정의되지 않았습니다. " +
                    "fallback 전략이 없어 일부 패키지의 촬영 탐지가 실패할 수 있습니다.");
            }
        }

        // 검증 상수 확인
        if (config.Validation == null)
        {
            logger?.LogWarning(
                "[YamlConfigurationLoader] Validation 상수가 정의되지 않았습니다. " +
                "조건부 아티팩트 검증에 영향을 줄 수 있습니다.");
        }

        // 가중치 범위 검증 (0.0 ~ 1.0)
        if (config.ArtifactWeights != null)
        {
            ValidateWeightRange(config.ArtifactWeights.Session, "Session", logger);
            ValidateWeightRange(config.ArtifactWeights.Capture, "Capture", logger);
        }
    }

    /// <summary>
    /// 가중치 범위 검증 (0.0 ~ 1.0)
    /// </summary>
    private static void ValidateWeightRange(
        Dictionary<string, double>? weights,
        string category,
        ILogger? logger)
    {
        if (weights == null) return;

        foreach (var (eventType, weight) in weights)
        {
            if (weight < 0.0 || weight > 1.0)
            {
                logger?.LogWarning(
                    "[YamlConfigurationLoader] {Category} 가중치 범위 오류: " +
                    "{EventType} = {Weight} (유효 범위: 0.0 ~ 1.0)",
                    category, eventType, weight);
            }
        }
    }

    /// <summary>
    /// YAML 문자열에서 설정 로드 (테스트용)
    /// </summary>
    /// <param name="yamlContent">YAML 문자열</param>
    /// <param name="logger">로거 (optional)</param>
    /// <returns>로드된 설정 객체</returns>
    /// <remarks>
    /// 단위 테스트에서 파일 I/O 없이 설정을 테스트할 때 사용합니다.
    /// </remarks>
    public static ArtifactDetectionConfig LoadFromString(
        string yamlContent,
        ILogger? logger = null)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var config = deserializer.Deserialize<ArtifactDetectionConfig>(yamlContent);

            if (config == null)
            {
                logger?.LogWarning(
                    "[YamlConfigurationLoader] YAML 문자열 파싱 결과가 null입니다. 기본값을 사용합니다.");
                return ConfigurationProvider.GetDefault();
            }

            ValidateConfiguration(config, logger);

            return config;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                "[YamlConfigurationLoader] YAML 문자열 파싱 중 예외 발생. 기본값을 사용합니다.");
            return ConfigurationProvider.GetDefault();
        }
    }
}

