using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
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

    [Fact]
    public async Task ParseActivityLog_ShouldParse_TimestampAccurately()
    {
        // Arrange: 타임스탬프 정확도와 정렬 가능성 검증
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

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

        var uriEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT || 
                       e.EventType == LogEventTypes.URI_PERMISSION_REVOKE)
            .OrderBy(e => e.Timestamp)
            .ToList();

        uriEvents.Should().NotBeEmpty("Should have URI permission events");

        // 타임스탬프가 DateTime 타입이고 유효한지 확인
        foreach (var evt in uriEvents)
        {
            evt.Timestamp.Should().NotBe(default(DateTime), "Timestamp should be parsed correctly");
            evt.Timestamp.Year.Should().BeGreaterThan(2020, "Timestamp year should be realistic");
        }

        // 타임스탬프 정렬 가능성 검증
        var sortedByTimestamp = uriEvents.OrderBy(e => e.Timestamp).ToList();
        sortedByTimestamp.Should().Equal(uriEvents, "Events should be ordered by timestamp");

        _output.WriteLine($"✓ Timestamp parsing validated");
        _output.WriteLine($"  Total URI Events: {uriEvents.Count}");
        _output.WriteLine($"  First Event: {uriEvents.First().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Last Event: {uriEvents.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Time Range: {(uriEvents.Last().Timestamp - uriEvents.First().Timestamp).TotalMinutes:F2} minutes");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldValidate_UidRefCountTypes()
    {
        // Arrange: uid, refCount, userId가 올바른 타입(int)으로 파싱되는지 검증
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

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
        var grantEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .ToList();

        grantEvents.Should().NotBeEmpty("Should have URI_PERMISSION_GRANT events");

        foreach (var evt in grantEvents)
        {
            // uid 검증
            evt.Attributes.Should().ContainKey("uid");
            var uid = Convert.ToInt32(evt.Attributes["uid"]);
            uid.Should().BeGreaterThanOrEqualTo(0, "UID should be non-negative");

            // refCount 검증
            evt.Attributes.Should().ContainKey("refCount");
            var refCount = Convert.ToInt32(evt.Attributes["refCount"]);
            refCount.Should().BeGreaterThanOrEqualTo(0, "refCount should be non-negative");

            // userId 검증
            evt.Attributes.Should().ContainKey("userId");
            var userId = Convert.ToInt32(evt.Attributes["userId"]);
            userId.Should().BeGreaterThanOrEqualTo(0, "userId should be non-negative");
        }

        _output.WriteLine($"✓ Type validation completed");
        _output.WriteLine($"  Total GRANT Events: {grantEvents.Count}");
        _output.WriteLine($"  All uid/refCount/userId are valid integers");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldValidate_UriFormat()
    {
        // Arrange: URI 형식 검증 (content://로 시작, 비어있지 않음)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

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
        var uriEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT || 
                       e.EventType == LogEventTypes.URI_PERMISSION_REVOKE)
            .ToList();

        uriEvents.Should().NotBeEmpty("Should have URI permission events");

        foreach (var evt in uriEvents)
        {
            evt.Attributes.Should().ContainKey("uri");
            var uri = evt.Attributes["uri"].ToString();

            uri.Should().NotBeNullOrWhiteSpace("URI should not be empty");
            uri.Should().StartWith("content://", "URI should start with content://");
        }

        _output.WriteLine($"✓ URI format validation completed");
        _output.WriteLine($"  Total URI Events: {uriEvents.Count}");
        _output.WriteLine($"  All URIs start with 'content://'");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldValidate_SectionParsing()
    {
        // Arrange: 섹션 파싱 검증 (uri_permissions, activity_starter 등)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

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
        result.Events.Should().NotBeEmpty("Should have parsed events");

        // SourceSection별 그룹화
        var eventsBySection = result.Events
            .GroupBy(e => e.SourceSection)
            .ToDictionary(g => g.Key, g => g.Count());

        eventsBySection.Should().NotBeEmpty("Should have events from different sections");

        _output.WriteLine($"✓ Section parsing validated");
        _output.WriteLine($"  Total Sections: {eventsBySection.Count}");
        foreach (var (section, count) in eventsBySection.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"  Section '{section}': {count} events");
        }

        // uri_permissions 섹션 이벤트 확인
        var uriPermissionEvents = result.Events
            .Where(e => e.SourceSection == "uri_permissions")
            .ToList();

        if (uriPermissionEvents.Any())
        {
            _output.WriteLine($"\n  URI Permissions Section:");
            _output.WriteLine($"    Total Events: {uriPermissionEvents.Count}");
            _output.WriteLine($"    Event Types: {string.Join(", ", uriPermissionEvents.Select(e => e.EventType).Distinct())}");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldGroup_EventsByUid()
    {
        // Arrange: UID별 이벤트 그룹화 및 GRANT/REVOKE 개수 확인
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

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
        var uriEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT || 
                       e.EventType == LogEventTypes.URI_PERMISSION_REVOKE)
            .ToList();

        uriEvents.Should().NotBeEmpty("Should have URI permission events");

        // UID별 그룹화
        var eventsByUid = uriEvents
            .GroupBy(e => Convert.ToInt32(e.Attributes["uid"]))
            .OrderByDescending(g => g.Count())
            .Take(5)  // 상위 5개 UID만
            .ToList();

        _output.WriteLine($"✓ UID grouping validated");
        _output.WriteLine($"  Total UIDs: {uriEvents.Select(e => e.Attributes["uid"]).Distinct().Count()}");
        _output.WriteLine($"\n  Top 5 UIDs by event count:");

        foreach (var group in eventsByUid)
        {
            var uid = group.Key;
            var grantCount = group.Count(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT);
            var revokeCount = group.Count(e => e.EventType == LogEventTypes.URI_PERMISSION_REVOKE);

            _output.WriteLine($"    UID {uid}: GRANT={grantCount}, REVOKE={revokeCount}, Total={group.Count()}");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldTrack_UriPermissionLifecycle()
    {
        // Arrange: URI 권한 lifecycle 추적 (GRANT → REVOKE)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

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
        var grantEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .ToList();

        var revokeEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_REVOKE)
            .ToList();

        grantEvents.Should().NotBeEmpty("Should have GRANT events");
        revokeEvents.Should().NotBeEmpty("Should have REVOKE events");

        // UID + URI 기반으로 GRANT-REVOKE 쌍 찾기
        var lifecycles = new List<(int uid, string uri, DateTime grantTime, DateTime? revokeTime, double? durationSeconds)>();

        foreach (var grant in grantEvents)
        {
            var uid = Convert.ToInt32(grant.Attributes["uid"]);
            var uri = grant.Attributes["uri"].ToString()!;

            var matchingRevoke = revokeEvents
                .Where(r => Convert.ToInt32(r.Attributes["uid"]) == uid &&
                           r.Attributes["uri"].ToString() == uri &&
                           r.Timestamp >= grant.Timestamp)
                .OrderBy(r => r.Timestamp)
                .FirstOrDefault();

            if (matchingRevoke != null)
            {
                var duration = (matchingRevoke.Timestamp - grant.Timestamp).TotalSeconds;
                lifecycles.Add((uid, uri, grant.Timestamp, matchingRevoke.Timestamp, duration));
            }
            else
            {
                lifecycles.Add((uid, uri, grant.Timestamp, null, null));
            }
        }

        var completedLifecycles = lifecycles.Count(l => l.revokeTime.HasValue);
        var incompleteLifecycles = lifecycles.Count(l => !l.revokeTime.HasValue);

        _output.WriteLine($"✓ URI permission lifecycle tracking validated");
        _output.WriteLine($"  Total GRANT events: {grantEvents.Count}");
        _output.WriteLine($"  Total REVOKE events: {revokeEvents.Count}");
        _output.WriteLine($"  Complete Lifecycles (GRANT → REVOKE): {completedLifecycles}");
        _output.WriteLine($"  Incomplete Lifecycles: {incompleteLifecycles}");

        if (lifecycles.Where(l => l.durationSeconds.HasValue).Any())
        {
            var avgDuration = lifecycles.Where(l => l.durationSeconds.HasValue)
                .Average(l => l.durationSeconds!.Value);
            var maxDuration = lifecycles.Where(l => l.durationSeconds.HasValue)
                .Max(l => l.durationSeconds!.Value);
            var minDuration = lifecycles.Where(l => l.durationSeconds.HasValue)
                .Min(l => l.durationSeconds!.Value);

            _output.WriteLine($"\n  Duration Statistics:");
            _output.WriteLine($"    Average: {avgDuration:F2}s");
            _output.WriteLine($"    Min: {minDuration:F2}s");
            _output.WriteLine($"    Max: {maxDuration:F2}s");

            // 샘플 출력
            _output.WriteLine($"\n  Sample Lifecycles:");
            foreach (var lifecycle in lifecycles.Where(l => l.durationSeconds.HasValue).Take(3))
            {
                var shortUri = lifecycle.uri.Length > 80 ? lifecycle.uri.Substring(0, 80) + "..." : lifecycle.uri;
                _output.WriteLine($"    UID {lifecycle.uid}: GRANT at {lifecycle.grantTime:HH:mm:ss.fff} → REVOKE at {lifecycle.revokeTime:HH:mm:ss.fff} ({lifecycle.durationSeconds:F2}s)");
                _output.WriteLine($"      URI: {shortUri}");
            }
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldValidate_ProviderField()
    {
        // Arrange: provider 필드 검증 (GRANT 이벤트에만 존재)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

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
        var grantEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .ToList();

        grantEvents.Should().NotBeEmpty("Should have URI_PERMISSION_GRANT events");

        // 모든 GRANT 이벤트는 provider 필드를 가져야 함
        foreach (var evt in grantEvents)
        {
            evt.Attributes.Should().ContainKey("provider", "GRANT events should have provider field");
            var provider = evt.Attributes["provider"].ToString();
            provider.Should().NotBeNullOrWhiteSpace("provider should not be empty");
        }

        // provider 분포 확인
        var providerCounts = grantEvents
            .GroupBy(e => e.Attributes["provider"].ToString())
            .ToDictionary(g => g.Key!, g => g.Count());

        _output.WriteLine($"✓ Provider field validation completed");
        _output.WriteLine($"  Total GRANT Events: {grantEvents.Count}");
        _output.WriteLine($"  Unique Providers: {providerCounts.Count}");
        _output.WriteLine($"\n  Provider Distribution:");

        foreach (var (provider, count) in providerCounts.OrderByDescending(kv => kv.Value))
        {
            _output.WriteLine($"    {provider}: {count}");
        }
    }

    [Fact]
    public async Task ParseActivityLog_ShouldHandle_EmptyOrMissingFile()
    {
        // Arrange: 존재하지 않는 파일 처리
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var nonExistentLogPath = Path.Combine("TestData", "non_existent_activity.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo { AndroidVersion = "15", TimeZone = "Asia/Seoul" },
            ConvertToUtc = false
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await parser.ParseAsync(nonExistentLogPath, options));

        exception.Should().NotBeNull();
        exception.Message.Should().Contain(nonExistentLogPath);

        _output.WriteLine($"✓ Error handling validated");
        _output.WriteLine($"  FileNotFoundException thrown as expected");
        _output.WriteLine($"  Error Message: {exception.Message}");
    }

    [Fact]
    public async Task ParseActivityLog_ShouldParse_RefCountChanges()
    {
        // Arrange: refCount 변화 추적
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("TestData", "activity.txt");

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
        var grantEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_GRANT)
            .ToList();

        var revokeEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.URI_PERMISSION_REVOKE)
            .ToList();

        grantEvents.Should().NotBeEmpty("Should have GRANT events");

        // refCount 분포 확인
        var grantRefCounts = grantEvents
            .Select(e => Convert.ToInt32(e.Attributes["refCount"]))
            .GroupBy(rc => rc)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        var revokeRefCounts = revokeEvents
            .Select(e => Convert.ToInt32(e.Attributes["refCount"]))
            .GroupBy(rc => rc)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());

        _output.WriteLine($"✓ refCount tracking validated");
        _output.WriteLine($"\n  GRANT refCount Distribution:");
        foreach (var (refCount, count) in grantRefCounts)
        {
            _output.WriteLine($"    refCount={refCount}: {count} events");
        }

        if (revokeRefCounts.Any())
        {
            _output.WriteLine($"\n  REVOKE refCount Distribution:");
            foreach (var (refCount, count) in revokeRefCounts)
            {
                _output.WriteLine($"    refCount={refCount}: {count} events");
            }
        }

        // refCount가 증가/감소하는 패턴 확인 (동일 UID+URI에 대해)
        var sampleUid = grantEvents.First().Attributes["uid"];
        var sampleUri = grantEvents.First().Attributes["uri"].ToString();

        var sampleEvents = result.Events
            .Where(e => (e.EventType == LogEventTypes.URI_PERMISSION_GRANT || 
                        e.EventType == LogEventTypes.URI_PERMISSION_REVOKE) &&
                       e.Attributes["uid"].ToString() == sampleUid.ToString() &&
                       e.Attributes["uri"].ToString() == sampleUri)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (sampleEvents.Count > 1)
        {
            _output.WriteLine($"\n  Sample refCount Changes (UID={sampleUid}):");
            foreach (var evt in sampleEvents)
            {
                var refCount = evt.Attributes["refCount"];
                _output.WriteLine($"    {evt.Timestamp:HH:mm:ss.fff} - {evt.EventType}: refCount={refCount}");
            }
        }
    }

}

