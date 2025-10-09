using AndroidAdbAnalyzeModule.Configuration.Loaders;
using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// Media Camera Service 로그 파싱 테스트
/// </summary>
public class MediaCameraLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public MediaCameraLogParserTests(ITestOutputHelper output)
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

    [Fact]
    public async Task ParseMediaCameraLog_ShouldSucceed()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul"
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();
        result.Statistics.TotalLines.Should().BeGreaterThan(0);
        result.Statistics.ParsedLines.Should().BeGreaterThan(0);
        result.Statistics.SuccessRate.Should().BeGreaterThan(0);
        
        _output.WriteLine($"총 이벤트: {result.Events.Count}");
        _output.WriteLine($"파싱된 라인: {result.Statistics.ParsedLines}");
        _output.WriteLine($"성공률: {result.Statistics.SuccessRate:P2}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldParse_ConnectEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        
        var connectEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT)
            .ToList();

        connectEvents.Should().NotBeEmpty("CAMERA_CONNECT 이벤트가 파싱되어야 합니다");
        
        // 첫 번째 CONNECT 이벤트 검증
        var firstConnect = connectEvents.First();
        firstConnect.Attributes.Should().ContainKey("deviceId");
        firstConnect.Attributes.Should().ContainKey("package");
        firstConnect.Attributes.Should().ContainKey("pid");
        firstConnect.Attributes.Should().ContainKey("priority");
        
        _output.WriteLine($"CONNECT 이벤트 수: {connectEvents.Count}");
        _output.WriteLine($"첫 CONNECT - Device: {firstConnect.Attributes["deviceId"]}, Package: {firstConnect.Attributes["package"]}, PID: {firstConnect.Attributes["pid"]}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldParse_DisconnectEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        
        var disconnectEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT)
            .ToList();

        disconnectEvents.Should().NotBeEmpty("CAMERA_DISCONNECT 이벤트가 파싱되어야 합니다");
        
        // 첫 번째 DISCONNECT 이벤트 검증
        var firstDisconnect = disconnectEvents.First();
        firstDisconnect.Attributes.Should().ContainKey("deviceId");
        firstDisconnect.Attributes.Should().ContainKey("package");
        firstDisconnect.Attributes.Should().ContainKey("pid");
        
        _output.WriteLine($"DISCONNECT 이벤트 수: {disconnectEvents.Count}");
        _output.WriteLine($"첫 DISCONNECT - Device: {firstDisconnect.Attributes["deviceId"]}, Package: {firstDisconnect.Attributes["package"]}, PID: {firstDisconnect.Attributes["pid"]}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldIdentify_CameraUsageSession()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        
        var connectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_CONNECT).ToList();
        var disconnectEvents = result.Events.Where(e => e.EventType == LogEventTypes.CAMERA_DISCONNECT).ToList();

        connectEvents.Should().NotBeEmpty();
        disconnectEvents.Should().NotBeEmpty();
        
        // 카메라 세션 매칭 가능 여부 검증 (PID 기반)
        var connectPids = connectEvents
            .Select(e => e.Attributes["pid"].ToString())
            .ToHashSet();
        
        var disconnectPids = disconnectEvents
            .Select(e => e.Attributes["pid"].ToString())
            .ToHashSet();
        
        var matchingPids = connectPids.Intersect(disconnectPids).ToList();
        
        matchingPids.Should().NotBeEmpty("CONNECT와 DISCONNECT가 매칭되는 PID가 있어야 합니다");
        
        _output.WriteLine($"총 CONNECT 이벤트: {connectEvents.Count}");
        _output.WriteLine($"총 DISCONNECT 이벤트: {disconnectEvents.Count}");
        _output.WriteLine($"매칭되는 PID 수: {matchingPids.Count}");
        _output.WriteLine($"매칭 PID 목록: {string.Join(", ", matchingPids)}");
    }

    [Fact]
    public async Task ParseMediaCameraLog_ShouldProvide_DataForCorrelation()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_camera_config.yaml");
        var logPath = Path.Combine("TestData", "media.camera.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        
        // 상위 애플리케이션에서 상관관계 분석에 필요한 데이터 검증
        var allEvents = result.Events.ToList();
        
        // 1. 타임스탬프가 파싱되어야 함
        allEvents.All(e => e.Timestamp != default).Should().BeTrue("모든 이벤트에 타임스탬프가 있어야 합니다");
        
        // 2. DeviceId, Package, PID 필드가 있어야 함
        allEvents.All(e => 
            e.Attributes.ContainsKey("deviceId") &&
            e.Attributes.ContainsKey("package") &&
            e.Attributes.ContainsKey("pid")
        ).Should().BeTrue("상관관계 분석에 필요한 필드(deviceId, package, pid)가 모두 있어야 합니다");
        
        // 3. 카메라 앱 패키지 필터링 가능 여부
        var cameraAppEvents = allEvents
            .Where(e => e.Attributes["package"].ToString()!.Contains("camera", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        cameraAppEvents.Should().NotBeEmpty("카메라 앱 이벤트가 있어야 합니다");
        
        _output.WriteLine($"전체 이벤트: {allEvents.Count}");
        _output.WriteLine($"카메라 앱 이벤트: {cameraAppEvents.Count}");
        _output.WriteLine("✅ 상위 애플리케이션에서 상관관계 분석(CONNECT→DISCONNECT 세션 추적)에 필요한 모든 데이터 제공됨");
    }
}

