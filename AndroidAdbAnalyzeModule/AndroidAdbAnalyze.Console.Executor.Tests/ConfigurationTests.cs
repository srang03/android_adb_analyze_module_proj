using AndroidAdbAnalyze.Console.Executor.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AndroidAdbAnalyze.Console.Executor.Tests;

/// <summary>
/// 설정 파일 로드 및 바인딩 테스트
/// </summary>
public class ConfigurationTests
{
    [Fact]
    public void AppsettingsJson_ShouldLoad_Successfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        // Act
        var adbConfig = configuration.GetSection("Adb").Get<AdbConfiguration>();
        var logCollectionConfig = configuration.GetSection("LogCollection").Get<LogCollectionConfiguration>();
        var analysisConfig = configuration.GetSection("Analysis").Get<AnalysisConfiguration>();

        // Assert
        Assert.NotNull(adbConfig);
        Assert.NotNull(logCollectionConfig);
        Assert.NotNull(analysisConfig);
        
        Assert.Equal(60, adbConfig.CommandTimeout);
        Assert.Equal(3, adbConfig.RetryCount);
        Assert.Equal("./logs", logCollectionConfig.OutputDirectory);
        Assert.Equal(7, logCollectionConfig.Logs.Count);  // 7개 로그
        Assert.Equal(0.3, analysisConfig.MinConfidenceThreshold);
    }

    [Fact]
    public void LogDefinitions_ShouldContain_AllRequiredLogs()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var logCollectionConfig = configuration.GetSection("LogCollection").Get<LogCollectionConfiguration>();

        // Act
        var logNames = logCollectionConfig!.Logs.Select(l => l.Name).ToList();

        // Assert
        Assert.Contains("activity", logNames);
        Assert.Contains("audio", logNames);
        Assert.Contains("media.camera", logNames);
        Assert.Contains("media.camera.worker", logNames);
        Assert.Contains("media.metrics", logNames);
        Assert.Contains("usagestats", logNames);
        Assert.Contains("vibrator_manager", logNames);
    }

    [Fact]
    public void YamlParserConfigs_ShouldExist()
    {
        // Arrange
        var expectedConfigs = new[]
        {
            "Configs/Parser/adb_activity_config.yaml",
            "Configs/Parser/adb_audio_config.yaml",
            "Configs/Parser/adb_media_camera_config.yaml",
            "Configs/Parser/adb_media_camera_worker_config.yaml",
            "Configs/Parser/adb_media_metrics_config.yaml",
            "Configs/Parser/adb_usagestats_config.yaml",
            "Configs/Parser/adb_vibrator_config.yaml"
        };

        // Act & Assert
        foreach (var configPath in expectedConfigs)
        {
            var fullPath = Path.Combine(AppContext.BaseDirectory, configPath);
            Assert.True(File.Exists(fullPath), $"Parser config not found: {configPath}");
        }
    }
}

