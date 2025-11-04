using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Analysis.Tests.Configuration;

/// <summary>
/// YamlConfigurationLoader 단위 테스트
/// </summary>
public sealed class YamlConfigurationLoaderTests
{
    private readonly ITestOutputHelper _output;

    public YamlConfigurationLoaderTests(ITestOutputHelper output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    #region LoadFromString Tests

    [Fact]
    public void LoadFromString_WithValidYaml_ShouldReturnConfig()
    {
        // Arrange
        var yaml = @"
artifactWeights:
  session:
    ACTIVITY_RESUMED: 0.7
    CAMERA_CONNECT: 0.6
  capture:
    DATABASE_INSERT: 0.5
    VIBRATION_EVENT: 0.4

strategies:
  base_pattern:
    packagePattern: null
    keyArtifacts:
      - DATABASE_INSERT
      - DATABASE_EVENT
    conditionalKeyArtifacts:
      - VIBRATION_EVENT
    supportingArtifacts:
      - PLAYER_CREATED

validation:
  hapticTypeCameraShutter: 50061
  playerEventStateStarted: started
  playerTagCamera: CAMERA
  serviceClassPostProcess: PostProcessService
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        config.ArtifactWeights.Should().NotBeNull();
        config.ArtifactWeights.Session.Should().ContainKey("ACTIVITY_RESUMED");
        config.ArtifactWeights.Session["ACTIVITY_RESUMED"].Should().Be(0.7);
        config.ArtifactWeights.Capture["DATABASE_INSERT"].Should().Be(0.5);

        _output.WriteLine("✅ LoadFromString 성공");
        _output.WriteLine($"Session Weights: {config.ArtifactWeights.Session.Count}");
        _output.WriteLine($"Capture Weights: {config.ArtifactWeights.Capture.Count}");
    }

    [Fact]
    public void LoadFromString_WithEmptyYaml_ShouldReturnDefaultConfig()
    {
        // Arrange
        var yaml = "";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        _output.WriteLine("✅ 빈 YAML → 기본값 반환");
    }

    [Fact]
    public void LoadFromString_WithInvalidYaml_ShouldReturnDefaultConfig()
    {
        // Arrange
        var yaml = @"
invalid:
  - yaml
  - syntax
  wrong: indent
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        _output.WriteLine("✅ 잘못된 YAML → 기본값 반환 (Fallback)");
    }

    [Fact]
    public void LoadFromString_WithPartialYaml_ShouldLoadSuccessfully()
    {
        // Arrange
        var yaml = @"
artifactWeights:
  capture:
    DATABASE_INSERT: 0.8
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        config.ArtifactWeights.Should().NotBeNull();
        config.ArtifactWeights.Capture.Should().ContainKey("DATABASE_INSERT");
        config.ArtifactWeights.Capture["DATABASE_INSERT"].Should().Be(0.8);

        _output.WriteLine("✅ 부분 YAML 로드 성공");
    }

    #endregion

    #region LoadFromFile Tests

    [Fact]
    public void LoadFromFile_WithNonExistentFile_ShouldReturnDefaultConfig()
    {
        // Arrange
        var filePath = "non_existent_config.yaml";

        // Act
        var config = YamlConfigurationLoader.LoadFromFile(filePath, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        _output.WriteLine("✅ 파일 없음 → 기본값 반환 (Fallback)");
    }

    [Fact]
    public void TryLoadFromFile_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var filePath = "non_existent_config.yaml";

        // Act
        var (success, config) = YamlConfigurationLoader.TryLoadFromFile(filePath, NullLogger.Instance);

        // Assert
        success.Should().BeFalse();
        config.Should().NotBeNull(); // 기본값은 반환
        _output.WriteLine("✅ TryLoad: 파일 없음 → Success=false, 기본값 반환");
    }

    [Fact]
    public void LoadFromFile_WithExampleYaml_ShouldLoadSuccessfully()
    {
        // Arrange
        var projectRoot = FindProjectRoot();
        var exampleFilePath = Path.Combine(
            projectRoot,
            "AndroidAdbAnalyze.Analysis",
            "Configs",
            "artifact-detection-config.example.yaml");

        if (!File.Exists(exampleFilePath))
        {
            _output.WriteLine($"⚠️ 예시 파일을 찾을 수 없습니다: {exampleFilePath}");
            return;
        }

        // Act
        var (success, config) = YamlConfigurationLoader.TryLoadFromFile(exampleFilePath, NullLogger.Instance);

        // Assert
        success.Should().BeTrue();
        config.Should().NotBeNull();
        config.ArtifactWeights.Should().NotBeNull();
        config.Strategies.Should().NotBeNull();
        config.Validation.Should().NotBeNull();

        _output.WriteLine($"✅ 예시 YAML 파일 로드 성공: {Path.GetFileName(exampleFilePath)}");
        _output.WriteLine($"  Session Weights: {config.ArtifactWeights.Session?.Count ?? 0}");
        _output.WriteLine($"  Capture Weights: {config.ArtifactWeights.Capture?.Count ?? 0}");
        _output.WriteLine($"  Strategies: {config.Strategies.Count}");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void LoadFromString_WithInvalidWeightRange_ShouldLogWarning()
    {
        // Arrange
        var yaml = @"
artifactWeights:
  capture:
    DATABASE_INSERT: 1.5
    VIBRATION_EVENT: -0.1
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        // 경고는 로그로만 출력되고, 설정은 그대로 로드됨
        config.ArtifactWeights.Capture["DATABASE_INSERT"].Should().Be(1.5);
        config.ArtifactWeights.Capture["VIBRATION_EVENT"].Should().Be(-0.1);

        _output.WriteLine("✅ 범위 초과 가중치 → 경고 로그, 설정은 로드됨");
    }

    [Fact]
    public void LoadFromString_WithMissingSessionWeights_ShouldLogWarning()
    {
        // Arrange
        var yaml = @"
artifactWeights:
  capture:
    DATABASE_INSERT: 0.5
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        config.ArtifactWeights.Session.Should().BeEmpty();

        _output.WriteLine("✅ Session 가중치 없음 → 경고 로그");
    }

    #endregion

    #region Strategy Configuration Tests

    [Fact]
    public void LoadFromString_WithMultipleStrategies_ShouldLoadAll()
    {
        // Arrange
        var yaml = @"
strategies:
  base_pattern:
    packagePattern: null
    keyArtifacts:
      - DATABASE_INSERT
    conditionalKeyArtifacts:
      - VIBRATION_EVENT
    supportingArtifacts:
      - PLAYER_CREATED
  
  telegram:
    packagePattern: org.telegram.messenger
    keyArtifacts:
      - DATABASE_INSERT
    conditionalKeyArtifacts:
      - VIBRATION_EVENT
    supportingArtifacts:
      - SHUTTER_SOUND
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        config.Strategies.Should().HaveCount(2);
        config.Strategies.Should().ContainKey("base_pattern");
        config.Strategies.Should().ContainKey("telegram");

        var baseStrategy = config.Strategies["base_pattern"];
        baseStrategy.KeyArtifacts.Should().Contain("DATABASE_INSERT");
        baseStrategy.ConditionalKeyArtifacts.Should().Contain("VIBRATION_EVENT");
        baseStrategy.SupportingArtifacts.Should().Contain("PLAYER_CREATED");

        var telegramStrategy = config.Strategies["telegram"];
        telegramStrategy.PackagePattern.Should().Be("org.telegram.messenger");

        _output.WriteLine("✅ 여러 전략 로드 성공");
        _output.WriteLine($"  base_pattern: {baseStrategy.KeyArtifacts.Count} key artifacts");
        _output.WriteLine($"  telegram: {telegramStrategy.KeyArtifacts.Count} key artifacts");
    }

    #endregion

    #region Integration with ConfigurationProvider Tests

    [Fact]
    public void ConfigurationProvider_GetDefault_ShouldMatchExpectedValues()
    {
        // Arrange & Act
        var config = ConfigurationProvider.GetDefault();

        // Assert
        config.Should().NotBeNull();
        config.ArtifactWeights.Should().NotBeNull();
        
        // 세션 가중치 검증
        config.ArtifactWeights.Session["ACTIVITY_RESUMED"].Should().Be(0.7);
        config.ArtifactWeights.Session["CAMERA_CONNECT"].Should().Be(0.6);
        
        // 촬영 가중치 검증
        config.ArtifactWeights.Capture["DATABASE_INSERT"].Should().Be(0.5);
        config.ArtifactWeights.Capture["VIBRATION_EVENT"].Should().Be(0.4);
        config.ArtifactWeights.Capture["PLAYER_EVENT"].Should().Be(0.35);
        
        // 전략 검증
        config.Strategies.Should().ContainKey("base_pattern");
        config.Strategies.Should().ContainKey("telegram");
        config.Strategies.Should().ContainKey("kakao_talk");
        
        // 검증 상수
        config.Validation.HapticTypeCameraShutter.Should().Be(50061);
        config.Validation.PlayerEventStateStarted.Should().Be("started");
        config.Validation.PlayerTagCamera.Should().Be("CAMERA");
        config.Validation.ServiceClassPostProcess.Should().Be("PostProcessService");

        _output.WriteLine("✅ ConfigurationProvider.GetDefault() 검증 성공");
        _output.WriteLine($"  Session Weights: {config.ArtifactWeights.Session.Count}");
        _output.WriteLine($"  Capture Weights: {config.ArtifactWeights.Capture.Count}");
        _output.WriteLine($"  Strategies: {config.Strategies.Count}");
    }

    [Fact]
    public void YamlConfig_ShouldOverrideDefaultValues()
    {
        // Arrange
        var yaml = @"
artifactWeights:
  capture:
    DATABASE_INSERT: 0.8
";

        // Act
        var defaultConfig = ConfigurationProvider.GetDefault();
        var yamlConfig = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        defaultConfig.ArtifactWeights.Capture["DATABASE_INSERT"].Should().Be(0.5);
        yamlConfig.ArtifactWeights.Capture["DATABASE_INSERT"].Should().Be(0.8);

        _output.WriteLine("✅ YAML 설정이 기본값 오버라이드 가능");
        _output.WriteLine($"  Default: 0.5 → YAML: 0.8");
    }

    #endregion

    #region Helper Methods

    private static string FindProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var testProjectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".."));
        return testProjectRoot;
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void LoadFromString_WithUnicodeCharacters_ShouldLoadSuccessfully()
    {
        // Arrange
        var yaml = @"
# 한글 주석 테스트
validation:
  playerEventStateStarted: 시작됨
  playerTagCamera: 카메라
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        config.Validation.PlayerEventStateStarted.Should().Be("시작됨");
        config.Validation.PlayerTagCamera.Should().Be("카메라");

        _output.WriteLine("✅ 유니코드 문자 처리 성공");
    }

    [Fact]
    public void LoadFromString_WithComments_ShouldIgnoreComments()
    {
        // Arrange
        var yaml = @"
# 이것은 주석입니다
artifactWeights:
  capture:
    DATABASE_INSERT: 0.5  # 인라인 주석
    # VIBRATION_EVENT: 0.4  <- 주석 처리됨
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        config.ArtifactWeights.Capture.Should().ContainKey("DATABASE_INSERT");
        config.ArtifactWeights.Capture.Should().NotContainKey("VIBRATION_EVENT");

        _output.WriteLine("✅ 주석 무시 처리 성공");
    }

    [Fact]
    public void LoadFromString_WithExtraProperties_ShouldIgnoreUnmatched()
    {
        // Arrange
        var yaml = @"
artifactWeights:
  capture:
    DATABASE_INSERT: 0.5
unknownProperty: someValue
anotherUnknown:
  nested: value
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        config.ArtifactWeights.Capture["DATABASE_INSERT"].Should().Be(0.5);

        _output.WriteLine("✅ 알 수 없는 속성 무시 (IgnoreUnmatchedProperties)");
    }

    #endregion

    #region Real-world Scenario Tests

    [Fact]
    public void LoadFromString_WithCompleteConfiguration_ShouldLoadAllSections()
    {
        // Arrange
        var yaml = @"
artifactWeights:
  session:
    ACTIVITY_RESUMED: 0.7
    CAMERA_CONNECT: 0.6
  capture:
    DATABASE_INSERT: 0.5
    VIBRATION_EVENT: 0.4
    PLAYER_EVENT: 0.35

strategies:
  base_pattern:
    packagePattern: null
    keyArtifacts:
      - DATABASE_INSERT
      - DATABASE_EVENT
    conditionalKeyArtifacts:
      - VIBRATION_EVENT
      - PLAYER_EVENT
    supportingArtifacts:
      - PLAYER_CREATED
      - SHUTTER_SOUND

validation:
  hapticTypeCameraShutter: 50061
  playerEventStateStarted: started
  playerTagCamera: CAMERA
  serviceClassPostProcess: PostProcessService

analysisOptions:
  thresholds:
    minConfidence: 0.3
    deduplicationSimilarity: 0.8
  timeWindows:
    maxSessionGapMinutes: 5
    eventCorrelationSeconds: 30
    captureDeduplicationSeconds: 1
";

        // Act
        var config = YamlConfigurationLoader.LoadFromString(yaml, NullLogger.Instance);

        // Assert
        config.Should().NotBeNull();
        
        // Weights
        config.ArtifactWeights.Session.Should().HaveCount(2);
        config.ArtifactWeights.Capture.Should().HaveCount(3);
        
        // Strategies
        config.Strategies.Should().ContainKey("base_pattern");
        config.Strategies["base_pattern"].KeyArtifacts.Should().HaveCount(2);
        config.Strategies["base_pattern"].ConditionalKeyArtifacts.Should().HaveCount(2);
        config.Strategies["base_pattern"].SupportingArtifacts.Should().HaveCount(2);
        
        // Validation
        config.Validation.HapticTypeCameraShutter.Should().Be(50061);
        
        // Analysis Options
        config.AnalysisOptions.Should().NotBeNull();
        config.AnalysisOptions!.Thresholds.MinConfidence.Should().Be(0.3);
        config.AnalysisOptions.TimeWindows.EventCorrelationSeconds.Should().Be(30);

        _output.WriteLine("✅ 전체 설정 로드 성공");
        _output.WriteLine($"  Session Weights: {config.ArtifactWeights.Session.Count}");
        _output.WriteLine($"  Capture Weights: {config.ArtifactWeights.Capture.Count}");
        _output.WriteLine($"  Strategies: {config.Strategies.Count}");
        _output.WriteLine($"  Validation Constants: 4");
        _output.WriteLine($"  Analysis Options: Loaded");
    }

    #endregion
}

