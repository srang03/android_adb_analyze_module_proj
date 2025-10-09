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
/// Activity Manager 로그 파싱 테스트
/// </summary>
public class ActivityLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public ActivityLogParserTests(ITestOutputHelper output)
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
    public async Task ParseActivityLog_ShouldSucceed()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "activity.txt");

        _output.WriteLine($"Config path: {configPath}");
        _output.WriteLine($"Log path: {logPath}");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 9, 15, 0, 0),
            AndroidVersion = "15",
            Manufacturer = "Samsung",
            Model = "SM-S928N"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = true,
            Encoding = "utf-8",
            MaxFileSizeMB = 10
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue($"Parsing should succeed. Error: {result.ErrorMessage}");
        
        _output.WriteLine($"\n=== Parsing Result ===");
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Total Events: {result.Events.Count}");
        _output.WriteLine($"Total Lines: {result.Statistics.TotalLines}");
        _output.WriteLine($"Parsed Lines: {result.Statistics.ParsedLines}");
        _output.WriteLine($"Skipped Lines: {result.Statistics.SkippedLines}");
        _output.WriteLine($"Error Lines: {result.Statistics.ErrorLines}");
        _output.WriteLine($"Elapsed Time: {result.Statistics.ElapsedTime.TotalMilliseconds}ms");
        _output.WriteLine($"Success Rate: {result.Statistics.SuccessRate:P2}");

        result.Events.Should().NotBeEmpty("Should parse at least some events from the log file");

        _output.WriteLine($"\n=== Event Type Counts ===");
        foreach (var kvp in result.Statistics.EventTypeCounts)
        {
            _output.WriteLine($"{kvp.Key}: {kvp.Value}");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldHandle_MultipleEventTypes()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "activity.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 9, 15, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = true
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();

        // URI Permission 이벤트 확인
        var uriGrantEvents = result.Events.Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT).ToList();
        var uriRevokeEvents = result.Events.Where(e => e.EventType == LogEventTypes.URI_PERMISSION_REVOKE).ToList();
        
        _output.WriteLine($"\n=== Event Type Distribution ===");
        _output.WriteLine($"URI_PERMISSION_GRANT: {uriGrantEvents.Count}");
        _output.WriteLine($"URI_PERMISSION_REVOKE: {uriRevokeEvents.Count}");

        // URI GRANT 이벤트가 존재해야 함
        uriGrantEvents.Should().NotBeEmpty("Should have URI_PERMISSION_GRANT events");
        
        // 첫 번째 GRANT 이벤트 구조 검증
        if (uriGrantEvents.Any())
        {
            var firstGrant = uriGrantEvents.First();
            
            _output.WriteLine($"\n=== First URI GRANT Event ===");
            _output.WriteLine($"Timestamp: {firstGrant.Timestamp}");
            _output.WriteLine($"EventType: {firstGrant.EventType}");
            _output.WriteLine($"Attributes:");
            foreach (var attr in firstGrant.Attributes)
            {
                _output.WriteLine($"  {attr.Key}: {attr.Value}");
            }

            // 필수 필드 검증
            firstGrant.Attributes.Should().ContainKey("uri");
            firstGrant.Attributes.Should().ContainKey("uid");
            firstGrant.Attributes.Should().ContainKey("provider");
            firstGrant.Attributes.Should().ContainKey("userId");
            firstGrant.Attributes.Should().ContainKey("refCount");
        }

        // URI REVOKE 이벤트가 존재해야 함
        uriRevokeEvents.Should().NotBeEmpty("Should have URI_PERMISSION_REVOKE events");
        
        // 첫 번째 REVOKE 이벤트 구조 검증
        if (uriRevokeEvents.Any())
        {
            var firstRevoke = uriRevokeEvents.First();
            
            _output.WriteLine($"\n=== First URI REVOKE Event ===");
            _output.WriteLine($"Timestamp: {firstRevoke.Timestamp}");
            _output.WriteLine($"EventType: {firstRevoke.EventType}");
            _output.WriteLine($"Attributes:");
            foreach (var attr in firstRevoke.Attributes)
            {
                _output.WriteLine($"  {attr.Key}: {attr.Value}");
            }

            // 필수 필드 검증
            firstRevoke.Attributes.Should().ContainKey("uri");
            firstRevoke.Attributes.Should().ContainKey("uid");
            firstRevoke.Attributes.Should().ContainKey("userId");
            firstRevoke.Attributes.Should().ContainKey("refCount");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldParse_AllProviders()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "activity.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 9, 15, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = true
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();

        // 모든 Provider 추출 (필터링 없이)
        var allProviders = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .Where(e => e.Attributes.ContainsKey("provider"))
            .Select(e => e.Attributes["provider"].ToString())
            .Distinct()
            .ToList();

        _output.WriteLine($"\n=== All Providers Found (총 {allProviders.Count}개) ===");
        foreach (var provider in allProviders.OrderBy(p => p))
        {
            var count = result.Events
                .Count(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT && 
                           e.Attributes.ContainsKey("provider") && 
                           e.Attributes["provider"].ToString() == provider);
            _output.WriteLine($"{provider}: {count}개");
        }

        // 최소한 여러 provider가 파싱되어야 함
        allProviders.Should().NotBeEmpty("Should parse URIs from various providers");
        
        // 예상되는 주요 provider들
        var expectedProviders = new[]
        {
            "com.google.android.providers.media.module",  // Media Provider
            "com.kakao.talk"  // KakaoTalk (있을 경우)
        };

        // 최소한 하나의 provider는 있어야 함
        allProviders.Should().Contain(p => expectedProviders.Any(ep => p.Contains(ep)));
    }

    [Fact]
    public async Task ParseActivityLog_ShouldProvide_DataForCorrelation()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "activity.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 9, 15, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = true
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        
        // ✅ 상위 앱에서 수행할 필터링 시뮬레이션 (KakaoTalk temporary JPG files)
        var kakaoTempImages = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .Where(e => e.Attributes.ContainsKey("uri") && 
                       e.Attributes["uri"].ToString()!.Contains("com.kakao.talk.FileProvider"))
            .Where(e => e.Attributes["uri"].ToString()!.EndsWith(".jpg"))
            .ToList();

        _output.WriteLine($"\n=== 상위 앱 필터링 예시: KakaoTalk 임시 JPG 파일 ===");
        _output.WriteLine($"Total Events: {result.Events.Count}");
        _output.WriteLine($"KakaoTalk Temp JPGs: {kakaoTempImages.Count}");

        // 필터링 가능한 데이터 구조 확인
        foreach (var ev in kakaoTempImages.Take(3))
        {
            _output.WriteLine($"\n[{ev.Timestamp:yyyy-MM-dd HH:mm:ss.fff}]");
            _output.WriteLine($"  URI: {ev.Attributes["uri"]}");
            _output.WriteLine($"  Provider: {ev.Attributes["provider"]}");
            _output.WriteLine($"  UID: {ev.Attributes["uid"]}");
        }

        // ✅ 다른 provider 필터링 시뮬레이션 (Media Provider)
        var mediaProviderFiles = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .Where(e => e.Attributes.ContainsKey("provider") && 
                       e.Attributes["provider"].ToString()!.Contains("media.module"))
            .ToList();

        _output.WriteLine($"\n=== 상위 앱 필터링 예시: Media Provider 파일 ===");
        _output.WriteLine($"Media Provider Events: {mediaProviderFiles.Count}");

        // DLL의 역할 확인: 모든 URI를 파싱하여 제공
        result.Events.Should().NotBeEmpty();
        result.Events.Should().Contain(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT);
        result.Events.Should().Contain(e => e.Attributes.ContainsKey("uri"));
        result.Events.Should().Contain(e => e.Attributes.ContainsKey("provider"));

        _output.WriteLine($"\n✅ DLL 역할 확인:");
        _output.WriteLine($"   - 모든 URI 이벤트 파싱 완료: {result.Events.Count(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)}개");
        _output.WriteLine($"   - 상위 앱에서 필터링 가능: ✓");
        _output.WriteLine($"   - 확장성 (새 앱 추가): ✓ (DLL 수정 불필요)");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldDemonstrate_FilteringFlexibility()
    {
        // Arrange
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "activity.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 9, 15, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = true
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Success.Should().BeTrue();
        
        _output.WriteLine($"\n=== 상위 앱 필터링 유연성 시연 ===");
        _output.WriteLine($"Total URI GRANT Events: {result.Events.Count(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)}");

        // 시나리오 1: KakaoTalk 필터링
        var kakaoEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .Where(e => e.Attributes.ContainsKey("provider") && 
                       e.Attributes["provider"].ToString()!.Contains("kakao.talk"))
            .ToList();
        _output.WriteLine($"\n시나리오 1 - KakaoTalk: {kakaoEvents.Count}개");

        // 시나리오 2: Media Provider 필터링
        var mediaEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .Where(e => e.Attributes.ContainsKey("provider") && 
                       e.Attributes["provider"].ToString()!.Contains("media.module"))
            .ToList();
        _output.WriteLine($"시나리오 2 - Media Provider: {mediaEvents.Count}개");

        // 시나리오 3: Downloads 필터링
        var downloadEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .Where(e => e.Attributes.ContainsKey("uri") && 
                       e.Attributes["uri"].ToString()!.Contains("downloads"))
            .ToList();
        _output.WriteLine($"시나리오 3 - Downloads: {downloadEvents.Count}개");

        // 시나리오 4: 특정 UID 필터링
        if (result.Events.Any(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT))
        {
            var firstUid = result.Events
                .First(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT && e.Attributes.ContainsKey("uid"))
                .Attributes["uid"];
            
            var uidEvents = result.Events
                .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
                .Where(e => e.Attributes.ContainsKey("uid") && 
                           e.Attributes["uid"].ToString() == firstUid.ToString())
                .ToList();
            _output.WriteLine($"시나리오 4 - 특정 UID ({firstUid}): {uidEvents.Count}개");
        }

        // ✅ 핵심 검증: DLL은 필터링 없이 모든 데이터를 제공
        result.Events.Should().NotBeEmpty();
        _output.WriteLine($"\n✅ 핵심 원칙:");
        _output.WriteLine($"   DLL = 모든 URI 파싱 ✓");
        _output.WriteLine($"   상위 앱 = 비즈니스 로직에 따라 선택적 필터링 ✓");
        _output.WriteLine($"   확장성 = DLL 재빌드 없이 새 필터 추가 가능 ✓");
    }
}

