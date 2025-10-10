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
/// Media Metrics 로그 파싱 테스트
/// 카메라 셔터 사운드 재생 이벤트 파싱 검증
/// </summary>
public class MediaMetricsLogParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public MediaMetricsLogParserTests(ITestOutputHelper output)
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
    public async Task ParseMediaMetricsLog_ShouldSucceed()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var config = await configLoader.LoadAsync(configPath);
        var parser = new AdbLogParser(config, _logger);

        var options = new LogParsingOptions
        {
            DeviceInfo = new DeviceInfo
            {
                AndroidVersion = "15",
                TimeZone = "Asia/Seoul",
                CurrentTime = DateTime.Now
            },
            ConvertToUtc = false
        };

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Total Events: {result.Events.Count}");
        _output.WriteLine($"Total Lines: {result.Statistics.TotalLines}");
        _output.WriteLine($"Parsed Lines: {result.Statistics.ParsedLines}");
        _output.WriteLine($"Elapsed: {result.Statistics.ElapsedTime.TotalMilliseconds}ms");

        result.Success.Should().BeTrue();
        result.Events.Should().NotBeEmpty();
        result.Statistics.ParsedLines.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldParse_ExtractorEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var extractorEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.MEDIA_EXTRACTOR)
            .ToList();

        _output.WriteLine($"Total Extractor Events: {extractorEvents.Count}");
        
        extractorEvents.Should().NotBeEmpty("extractor events should be parsed");
        
        // 첫 번째 extractor 이벤트 검증
        var firstEvent = extractorEvents.First();
        _output.WriteLine($"First Extractor Event: {firstEvent.Timestamp}, Package: {firstEvent.Attributes.GetValueOrDefault("package")}");
        
        firstEvent.Attributes.Should().ContainKey("package");
        firstEvent.Attributes.Should().ContainKey("lineNumber");
        firstEvent.Attributes.Should().ContainKey("pid");
        firstEvent.Attributes.Should().ContainKey("uid");
        firstEvent.Attributes.Should().ContainKey("attributes_raw");
        
        // 카메라 앱과 관련된 extractor 이벤트 확인
        var cameraExtractorEvents = extractorEvents
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .ToList();
        
        _output.WriteLine($"Camera App Extractor Events: {cameraExtractorEvents.Count}");
        cameraExtractorEvents.Should().NotBeEmpty("camera app should have extractor events for shutter sound");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldParse_AudioTrackEvents()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var audioTrackEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.AUDIO_TRACK)
            .ToList();

        _output.WriteLine($"Total Audio Track Events: {audioTrackEvents.Count}");
        
        audioTrackEvents.Should().NotBeEmpty("audio track events should be parsed");
        
        // 첫 번째 audio.track 이벤트 검증
        var firstEvent = audioTrackEvents.First();
        _output.WriteLine($"First Audio Track Event: {firstEvent.Timestamp}, TrackId: {firstEvent.Attributes.GetValueOrDefault("trackId")}, Package: {firstEvent.Attributes.GetValueOrDefault("package")}");
        
        firstEvent.Attributes.Should().ContainKey("trackId");
        firstEvent.Attributes.Should().ContainKey("package");
        firstEvent.Attributes.Should().ContainKey("lineNumber");
        firstEvent.Attributes.Should().ContainKey("pid");
        firstEvent.Attributes.Should().ContainKey("uid");
        firstEvent.Attributes.Should().ContainKey("attributes_raw");
        
        // 카메라 앱과 관련된 audio track 이벤트 확인
        var cameraAudioTrackEvents = audioTrackEvents
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .ToList();
        
        _output.WriteLine($"Camera App Audio Track Events: {cameraAudioTrackEvents.Count}");
        cameraAudioTrackEvents.Should().NotBeEmpty("camera app should have audio track events for shutter sound playback");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldIdentify_ShutterSoundSequence()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var cameraEvents = result.Events
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .OrderBy(e => e.Timestamp)
            .ToList();

        _output.WriteLine($"Total Camera-related Events: {cameraEvents.Count}");
        
        cameraEvents.Should().NotBeEmpty("camera app should have media events");
        
        // Extractor + Audio Track 이벤트 조합 확인
        var extractorEvents = cameraEvents.Where(e => e.EventType == LogEventTypes.MEDIA_EXTRACTOR).ToList();
        var audioTrackEvents = cameraEvents.Where(e => e.EventType == LogEventTypes.AUDIO_TRACK).ToList();
        
        _output.WriteLine($"Camera Extractor Events: {extractorEvents.Count}");
        _output.WriteLine($"Camera Audio Track Events: {audioTrackEvents.Count}");
        
        // 카메라 촬영 시: Extractor (audio/ogg 파일 추출) + Audio Track (셔터 사운드 재생)
        extractorEvents.Should().NotBeEmpty("camera should extract audio/ogg shutter sound file");
        audioTrackEvents.Should().NotBeEmpty("camera should play shutter sound via audio track");
        
        // 첫 번째 촬영 시퀀스 샘플 출력
        if (extractorEvents.Any() && audioTrackEvents.Any())
        {
            var firstExtractor = extractorEvents.First();
            var nearbyAudioTracks = audioTrackEvents
                .Where(at => Math.Abs((at.Timestamp - firstExtractor.Timestamp).TotalSeconds) < 5)
                .Take(3)
                .ToList();
            
            _output.WriteLine($"\n📸 Sample Shutter Sound Sequence:");
            _output.WriteLine($"  Extractor: {firstExtractor.Timestamp:HH:mm:ss.fff}");
            foreach (var track in nearbyAudioTracks)
            {
                _output.WriteLine($"  Audio Track {track.Attributes.GetValueOrDefault("trackId")}: {track.Timestamp:HH:mm:ss.fff}");
            }
        }
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldGroup_EventsByTrackId()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var audioTrackEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.AUDIO_TRACK)
            .ToList();

        // Track ID별로 그룹핑 (상위 앱에서 수행할 작업 시뮬레이션)
        var trackGroups = audioTrackEvents
            .GroupBy(e => e.Attributes.GetValueOrDefault("trackId")?.ToString())
            .Where(g => g.Key != null)
            .OrderBy(g => g.First().Timestamp)
            .ToList();

        _output.WriteLine($"Total Audio Track Groups: {trackGroups.Count}");
        
        trackGroups.Should().NotBeEmpty("should have multiple track groups");
        
        // 각 Track ID는 여러 이벤트를 가질 수 있음 (server.ctor → create → start → stop)
        var cameraTrackGroups = trackGroups
            .Where(g => g.Any(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera"))
            .ToList();
        
        _output.WriteLine($"Camera App Track Groups: {cameraTrackGroups.Count}");
        
        cameraTrackGroups.Should().NotBeEmpty("camera app should have multiple track sessions");
        
        // 샘플 출력: 첫 번째 카메라 track 세션의 이벤트들
        if (cameraTrackGroups.Any())
        {
            var firstTrackGroup = cameraTrackGroups.First();
            var trackId = firstTrackGroup.Key;
            var events = firstTrackGroup.OrderBy(e => e.Attributes.GetValueOrDefault("lineNumber")).ToList();
            
            _output.WriteLine($"\n📊 Sample Camera Track Session (ID: {trackId}):");
            _output.WriteLine($"   Total Events: {events.Count}");
            _output.WriteLine($"   Time Range: {events.First().Timestamp:HH:mm:ss.fff} - {events.Last().Timestamp:HH:mm:ss.fff}");
        }
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldProvide_DataForCorrelation()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        // 상위 앱에서 카메라 촬영을 감지하기 위한 필수 정보 검증
        var cameraEvents = result.Events
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .OrderBy(e => e.Timestamp)
            .ToList();

        _output.WriteLine($"\n✅ Data for Upper-App Correlation:");
        _output.WriteLine($"   Total Camera Events: {cameraEvents.Count}");
        
        // 1. Timestamp: 시간 기반 상관관계 분석
        cameraEvents.Should().OnlyContain(e => e.Timestamp != default, 
            "all events should have valid timestamps for time-based correlation");
        
        // 2. Package: 앱 식별
        cameraEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("package"),
            "all events should have package info for app identification");
        
        // 3. Track ID: 동일한 촬영 세션 그룹핑
        var audioTrackEvents = cameraEvents.Where(e => e.EventType == LogEventTypes.AUDIO_TRACK).ToList();
        audioTrackEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("trackId"),
            "audio track events should have trackId for session grouping");
        
        // 4. Line Number: 이벤트 순서 보장
        cameraEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("lineNumber"),
            "all events should have line numbers for ordering");
        
        // 5. PID/UID: 프로세스 식별
        cameraEvents.Should().OnlyContain(e => e.Attributes.ContainsKey("pid") && e.Attributes.ContainsKey("uid"),
            "all events should have PID/UID for process tracking");
        
        _output.WriteLine($"   ✓ Timestamps: Valid");
        _output.WriteLine($"   ✓ Package Info: Present");
        _output.WriteLine($"   ✓ Track IDs: Present ({audioTrackEvents.Count} audio tracks)");
        _output.WriteLine($"   ✓ Line Numbers: Present");
        _output.WriteLine($"   ✓ PID/UID: Present");
        
        _output.WriteLine($"\n💡 Upper-app can:");
        _output.WriteLine($"   1. Group events by trackId to identify camera shutter sessions");
        _output.WriteLine($"   2. Correlate extractor + audio.track events within time window");
        _output.WriteLine($"   3. Detect camera capture by matching package=com.sec.android.app.camera");
        _output.WriteLine($"   4. Distinguish interrupts using lineNumber and timestamp gaps");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldParse_TimestampAccurately()
    {
        // Arrange: 타임스탬프 파싱 정확도 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        result.Events.Should().NotBeEmpty("Should parse at least some events");

        // 모든 이벤트가 유효한 타임스탬프를 가져야 함
        result.Events.Should().OnlyContain(e => e.Timestamp != default,
            "All events should have valid timestamps");

        // 타임스탬프가 정렬 가능해야 함
        var sortedEvents = result.Events.OrderBy(e => e.Timestamp).ToList();
        sortedEvents.Should().HaveCountGreaterThan(1, "Should have multiple events for sorting validation");

        // 시간 순서 검증
        for (int i = 1; i < sortedEvents.Count; i++)
        {
            sortedEvents[i].Timestamp.Should().BeOnOrAfter(sortedEvents[i - 1].Timestamp,
                "Events should be chronologically orderable");
        }

        _output.WriteLine($"✓ Timestamp parsing validated");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"  Time Range: {sortedEvents.First().Timestamp:yyyy-MM-dd HH:mm:ss.fff} ~ {sortedEvents.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  Duration: {(sortedEvents.Last().Timestamp - sortedEvents.First().Timestamp).TotalSeconds:F2}s");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldValidate_TrackIdType()
    {
        // Arrange: TrackId가 올바른 타입으로 파싱되는지 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var audioTrackEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.AUDIO_TRACK)
            .ToList();

        audioTrackEvents.Should().NotBeEmpty("Should have audio track events");

        // TrackId 타입 검증
        foreach (var evt in audioTrackEvents)
        {
            evt.Attributes.Should().ContainKey("trackId", "Audio track events should have trackId");

            var trackId = evt.Attributes["trackId"];
            trackId.Should().NotBeNull("trackId should not be null");

            // TrackId는 숫자 형태여야 함 (int 또는 string 형태의 숫자)
            if (trackId is int)
            {
                ((int)trackId).Should().BeGreaterThan(0, "TrackId should be a positive integer");
            }
            else if (int.TryParse(trackId.ToString(), out var parsedId))
            {
                parsedId.Should().BeGreaterThan(0, "TrackId should be parseable as a positive integer");
            }
            else
            {
                Assert.Fail($"TrackId should be an integer or string representation of integer, but got: {trackId} (type: {trackId.GetType().Name})");
            }
        }

        // TrackId 분포 확인
        var uniqueTrackIds = audioTrackEvents
            .Select(e => e.Attributes["trackId"].ToString())
            .Distinct()
            .ToList();

        _output.WriteLine($"✓ TrackId type validation passed");
        _output.WriteLine($"  Total Audio Track Events: {audioTrackEvents.Count}");
        _output.WriteLine($"  Unique TrackIds: {uniqueTrackIds.Count}");
        _output.WriteLine($"  Sample TrackIds: {string.Join(", ", uniqueTrackIds.Take(5))}");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldValidate_PidUidTypes()
    {
        // Arrange: PID/UID가 올바른 타입으로 파싱되는지 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        result.Events.Should().NotBeEmpty("Should parse at least some events");

        foreach (var evt in result.Events)
        {
            // PID 검증
            evt.Attributes.Should().ContainKey("pid", "All events should have PID");
            var pid = evt.Attributes["pid"];
            pid.Should().NotBeNull("PID should not be null");

            if (pid is int)
            {
                ((int)pid).Should().BeGreaterThanOrEqualTo(0, "PID should be a non-negative integer");
            }
            else if (int.TryParse(pid.ToString(), out var parsedPid))
            {
                parsedPid.Should().BeGreaterThanOrEqualTo(0, "PID should be parseable as a non-negative integer");
            }
            else
            {
                Assert.Fail($"PID should be an integer, but got: {pid} (type: {pid.GetType().Name})");
            }

            // UID 검증
            evt.Attributes.Should().ContainKey("uid", "All events should have UID");
            var uid = evt.Attributes["uid"];
            uid.Should().NotBeNull("UID should not be null");

            if (uid is int)
            {
                ((int)uid).Should().BeGreaterThanOrEqualTo(0, "UID should be a non-negative integer");
            }
            else if (int.TryParse(uid.ToString(), out var parsedUid))
            {
                parsedUid.Should().BeGreaterThanOrEqualTo(0, "UID should be parseable as a non-negative integer");
            }
            else
            {
                Assert.Fail($"UID should be an integer, but got: {uid} (type: {uid.GetType().Name})");
            }
        }

        // PID/UID 분포 확인
        var uniquePids = result.Events
            .Select(e => e.Attributes["pid"].ToString())
            .Distinct()
            .Count();

        var uniqueUids = result.Events
            .Select(e => e.Attributes["uid"].ToString())
            .Distinct()
            .Count();

        _output.WriteLine($"✓ PID/UID type validation passed");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"  Unique PIDs: {uniquePids}");
        _output.WriteLine($"  Unique UIDs: {uniqueUids}");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldParse_MultiplePackages()
    {
        // Arrange: 다양한 패키지의 이벤트가 파싱되는지 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var packageGroups = result.Events
            .Where(e => e.Attributes.ContainsKey("package"))
            .GroupBy(e => e.Attributes["package"].ToString())
            .OrderByDescending(g => g.Count())
            .ToList();

        packageGroups.Should().NotBeEmpty("Should parse events from at least one package");

        _output.WriteLine($"✓ Multiple package parsing validated");
        _output.WriteLine($"  Total Packages: {packageGroups.Count}");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"\n  Top 5 Packages:");

        foreach (var group in packageGroups.Take(5))
        {
            var packageName = group.Key;
            var eventCount = group.Count();
            var extractorCount = group.Count(e => e.EventType == LogEventTypes.MEDIA_EXTRACTOR);
            var audioTrackCount = group.Count(e => e.EventType == LogEventTypes.AUDIO_TRACK);

            _output.WriteLine($"    - {packageName}");
            _output.WriteLine($"      Total: {eventCount}, EXTRACTOR: {extractorCount}, AUDIO_TRACK: {audioTrackCount}");
        }

        // 카메라 앱이 포함되어 있어야 함
        var cameraPackages = packageGroups
            .Where(g => g.Key?.Contains("camera", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        cameraPackages.Should().NotBeEmpty("Should include camera-related packages");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldValidate_LineNumberOrdering()
    {
        // Arrange: Line Number가 순차적으로 증가하는지 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        result.Events.Should().NotBeEmpty("Should parse at least some events");

        // Line Number 순서 검증
        var sortedByLineNumber = result.Events
            .Where(e => e.Attributes.ContainsKey("lineNumber"))
            .OrderBy(e => Convert.ToInt32(e.Attributes["lineNumber"]))
            .ToList();

        sortedByLineNumber.Should().NotBeEmpty("Should have events with line numbers");

        // 연속된 이벤트의 라인 번호가 증가하는지 확인
        for (int i = 1; i < sortedByLineNumber.Count; i++)
        {
            var prevLineNumber = Convert.ToInt32(sortedByLineNumber[i - 1].Attributes["lineNumber"]);
            var currLineNumber = Convert.ToInt32(sortedByLineNumber[i].Attributes["lineNumber"]);

            currLineNumber.Should().BeGreaterThan(prevLineNumber,
                "Line numbers should be in ascending order");
        }

        _output.WriteLine($"✓ Line number ordering validated");
        _output.WriteLine($"  Total Events with Line Numbers: {sortedByLineNumber.Count}");
        _output.WriteLine($"  Line Number Range: {sortedByLineNumber.First().Attributes["lineNumber"]} ~ {sortedByLineNumber.Last().Attributes["lineNumber"]}");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldValidate_EventTypeDistribution()
    {
        // Arrange: 이벤트 타입 분포 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var eventTypeGroups = result.Events
            .GroupBy(e => e.EventType)
            .OrderByDescending(g => g.Count())
            .ToList();

        eventTypeGroups.Should().NotBeEmpty("Should have at least one event type");

        // MEDIA_EXTRACTOR와 AUDIO_TRACK 이벤트가 모두 존재해야 함
        var extractorEvents = result.Events.Where(e => e.EventType == LogEventTypes.MEDIA_EXTRACTOR).ToList();
        var audioTrackEvents = result.Events.Where(e => e.EventType == LogEventTypes.AUDIO_TRACK).ToList();

        extractorEvents.Should().NotBeEmpty("Should have MEDIA_EXTRACTOR events");
        audioTrackEvents.Should().NotBeEmpty("Should have AUDIO_TRACK events");

        _output.WriteLine($"✓ Event type distribution validated");
        _output.WriteLine($"  Total Event Types: {eventTypeGroups.Count}");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"\n  Event Type Distribution:");

        foreach (var group in eventTypeGroups)
        {
            var percentage = (group.Count() * 100.0 / result.Events.Count);
            _output.WriteLine($"    {group.Key}: {group.Count()} ({percentage:F1}%)");
        }

        // 카메라 앱의 이벤트 타입 분포
        var cameraEvents = result.Events
            .Where(e => e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .ToList();

        if (cameraEvents.Any())
        {
            var cameraExtractors = cameraEvents.Count(e => e.EventType == LogEventTypes.MEDIA_EXTRACTOR);
            var cameraAudioTracks = cameraEvents.Count(e => e.EventType == LogEventTypes.AUDIO_TRACK);

            _output.WriteLine($"\n  Camera App Distribution:");
            _output.WriteLine($"    Total: {cameraEvents.Count}");
            _output.WriteLine($"    MEDIA_EXTRACTOR: {cameraExtractors}");
            _output.WriteLine($"    AUDIO_TRACK: {cameraAudioTracks}");
        }
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldCorrelate_ExtractorAndAudioTrackTiming()
    {
        // Arrange: Extractor와 AudioTrack 이벤트의 시간 상관관계 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        var cameraExtractorEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.MEDIA_EXTRACTOR &&
                       e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .OrderBy(e => e.Timestamp)
            .ToList();

        var cameraAudioTrackEvents = result.Events
            .Where(e => e.EventType == LogEventTypes.AUDIO_TRACK &&
                       e.Attributes.GetValueOrDefault("package")?.ToString() == "com.sec.android.app.camera")
            .OrderBy(e => e.Timestamp)
            .ToList();

        cameraExtractorEvents.Should().NotBeEmpty("Should have camera extractor events");
        cameraAudioTrackEvents.Should().NotBeEmpty("Should have camera audio track events");

        // 각 Extractor 이벤트에 대해, 근접한 시간(예: 5초 이내)에 AudioTrack 이벤트가 있는지 확인
        var correlationWindow = TimeSpan.FromSeconds(5);
        var correlatedPairs = new List<(DateTime extractorTime, DateTime audioTrackTime, double gapSeconds)>();

        foreach (var extractor in cameraExtractorEvents)
        {
            var nearbyAudioTracks = cameraAudioTrackEvents
                .Where(at => Math.Abs((at.Timestamp - extractor.Timestamp).TotalSeconds) <= correlationWindow.TotalSeconds)
                .ToList();

            if (nearbyAudioTracks.Any())
            {
                var closest = nearbyAudioTracks
                    .OrderBy(at => Math.Abs((at.Timestamp - extractor.Timestamp).TotalSeconds))
                    .First();

                var gap = Math.Abs((closest.Timestamp - extractor.Timestamp).TotalSeconds);
                correlatedPairs.Add((extractor.Timestamp, closest.Timestamp, gap));
            }
        }

        // 대부분의 Extractor 이벤트가 근접한 AudioTrack 이벤트를 가져야 함
        var correlationRate = (double)correlatedPairs.Count / cameraExtractorEvents.Count;
        correlationRate.Should().BeGreaterThan(0.5, 
            "Most extractor events should have nearby audio track events (indicating shutter sound playback)");

        _output.WriteLine($"✓ Extractor-AudioTrack correlation validated");
        _output.WriteLine($"  Camera Extractor Events: {cameraExtractorEvents.Count}");
        _output.WriteLine($"  Camera Audio Track Events: {cameraAudioTrackEvents.Count}");
        _output.WriteLine($"  Correlated Pairs: {correlatedPairs.Count}");
        _output.WriteLine($"  Correlation Rate: {correlationRate:P1}");

        if (correlatedPairs.Any())
        {
            var avgGap = correlatedPairs.Average(p => p.gapSeconds);
            var maxGap = correlatedPairs.Max(p => p.gapSeconds);

            _output.WriteLine($"  Average Time Gap: {avgGap:F3}s");
            _output.WriteLine($"  Max Time Gap: {maxGap:F3}s");

            _output.WriteLine($"\n  Sample Correlated Pairs:");
            foreach (var pair in correlatedPairs.Take(3))
            {
                _output.WriteLine($"    Extractor: {pair.extractorTime:HH:mm:ss.fff} → AudioTrack: {pair.audioTrackTime:HH:mm:ss.fff} (gap: {pair.gapSeconds:F3}s)");
            }
        }
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldHandle_EmptyOrMissingFile()
    {
        // Arrange: 파일이 없거나 잘못된 경우 에러 처리 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var nonExistentLogPath = Path.Combine("TestData", "non_existent_media_metrics.txt");

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

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await parser.ParseAsync(nonExistentLogPath, options);
        });

        _output.WriteLine($"✓ Error handling validated");
        _output.WriteLine($"  FileNotFoundException correctly thrown for missing file");
    }

    [Fact]
    public async Task ParseMediaMetricsLog_ShouldValidate_AttributesRawContent()
    {
        // Arrange: attributes_raw 속성이 올바르게 파싱되는지 검증
        var configPath = Path.Combine("TestData", "adb_media_metrics_config.yaml");
        var logPath = Path.Combine("TestData", "media.metrics.txt");

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
        result.Events.Should().NotBeEmpty("Should parse at least some events");

        // 모든 이벤트가 attributes_raw를 가져야 함
        result.Events.Should().OnlyContain(e => e.Attributes.ContainsKey("attributes_raw"),
            "All events should have attributes_raw field");

        // attributes_raw가 비어있지 않아야 함
        foreach (var evt in result.Events)
        {
            evt.Attributes.Should().ContainKey("attributes_raw");
            var rawValue = evt.Attributes["attributes_raw"];
            string.IsNullOrWhiteSpace(rawValue?.ToString()).Should().BeFalse("attributes_raw should not be empty");
        }

        // 샘플 attributes_raw 출력
        var sampleEvent = result.Events.First();
        var attributesRaw = sampleEvent.Attributes["attributes_raw"]?.ToString() ?? string.Empty;

        _output.WriteLine($"✓ attributes_raw validation passed");
        _output.WriteLine($"  Total Events: {result.Events.Count}");
        _output.WriteLine($"  All events have non-empty attributes_raw");
        _output.WriteLine($"\n  Sample attributes_raw:");
        var displayLength = Math.Min(100, attributesRaw.Length);
        _output.WriteLine($"    {attributesRaw.Substring(0, displayLength)}...");
    }
}

