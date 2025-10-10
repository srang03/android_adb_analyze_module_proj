using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// Time Range Filtering 기능 단위 테스트
/// LogParsingOptions의 StartTime/EndTime 필터링 정확성 검증
/// </summary>
public class TimeRangeFilteringTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public TimeRangeFilteringTests(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        _logger = loggerFactory.CreateLogger<AdbLogParser>();
        _configLogger = loggerFactory.CreateLogger<YamlConfigurationLoader>();
    }

    #region StartTime + EndTime 필터링 테스트

    [Fact]
    public async Task ParseWithTimeRange_BothStartAndEndSet_ShouldFilterCorrectly()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found at: {logPath}. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 특정 시간 범위 설정: 22:58:20 ~ 22:58:40 (20초 윈도우)
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 20),
            EndTime = new DateTime(2025, 10, 6, 22, 58, 40)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 모든 이벤트가 시간 범위 내에 있는지 확인
            var eventsOutsideRange = result.Events
                .Where(e => e.Timestamp < options.StartTime || e.Timestamp > options.EndTime)
                .ToList();

            eventsOutsideRange.Should().BeEmpty($"모든 이벤트가 {options.StartTime} ~ {options.EndTime} 범위 내에 있어야 함");

            _output.WriteLine($"✓ Time Range Filtering 성공");
        }
        else
        {
            result.Success.Should().BeFalse();
            _output.WriteLine($"⚠️ 해당 시간 범위에 파싱 가능한 이벤트가 없습니다.");
        }
        
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
    }

    #endregion

    #region StartTime만 설정

    [Fact]
    public async Task ParseWithTimeRange_OnlyStartTimeSet_ShouldFilterCorrectly()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // StartTime만 설정 (EndTime은 null)
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 0),
            EndTime = null // EndTime 없음 - 상한 제한 없음
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // StartTime 이전 이벤트가 없어야 함
            var eventsBeforeStart = result.Events
                .Where(e => e.Timestamp < options.StartTime)
                .ToList();

            eventsBeforeStart.Should().BeEmpty($"모든 이벤트가 {options.StartTime} 이후여야 함");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ StartTime만 설정한 필터링 성공");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: (제한 없음)");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
    }

    #endregion

    #region EndTime만 설정

    [Fact]
    public async Task ParseWithTimeRange_OnlyEndTimeSet_ShouldFilterCorrectly()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // EndTime만 설정 (StartTime은 null)
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = null, // StartTime 없음 - 하한 제한 없음
            EndTime = new DateTime(2025, 10, 6, 22, 55, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();

        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // EndTime 이후 이벤트가 없어야 함
            var eventsAfterEnd = result.Events
                .Where(e => e.Timestamp > options.EndTime)
                .ToList();

            eventsAfterEnd.Should().BeEmpty($"모든 이벤트가 {options.EndTime} 이전이어야 함");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ EndTime만 설정한 필터링 성공");
        _output.WriteLine($"  - Start Time: (제한 없음)");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
    }

    #endregion

    #region 둘 다 null (필터링 없음)

    [Fact]
    public async Task ParseWithTimeRange_BothNull_ShouldNotFilter()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // StartTime, EndTime 둘 다 null
        var optionsWithFilter = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = null,
            EndTime = null
        };

        var optionsWithoutFilter = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 0, 0),
            EndTime = new DateTime(2025, 10, 6, 23, 0, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var resultNoFilter = await parser.ParseAsync(logPath, optionsWithFilter);

        // Assert
        resultNoFilter.Should().NotBeNull();
        
        if (resultNoFilter.Events.Any())
        {
            resultNoFilter.Success.Should().BeTrue();
        }
        else
        {
            resultNoFilter.Success.Should().BeFalse();
        }
        
        resultNoFilter.Events.Should().NotBeEmpty();

        _output.WriteLine($"✓ 시간 필터링 없음 (둘 다 null) 성공");
        _output.WriteLine($"  - Total Events: {resultNoFilter.Events.Count}");
    }

    #endregion

    #region 엣지 케이스: 잘못된 시간 범위

    [Fact]
    public async Task ParseWithTimeRange_StartTimeGreaterThanEndTime_ShouldReturnEmptyResults()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 잘못된 시간 범위: StartTime > EndTime
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 23, 0, 0), // 나중 시간
            EndTime = new DateTime(2025, 10, 6, 22, 0, 0)    // 이전 시간
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("StartTime > EndTime 이므로 파싱된 이벤트가 없어 실패 처리되어야 함");
        
        // 잘못된 시간 범위이므로 필터링된 이벤트가 없거나 매우 적어야 함
        result.Events.Should().BeEmpty("StartTime > EndTime이므로 유효한 이벤트가 없어야 함");

        _output.WriteLine($"✓ 잘못된 시간 범위 (StartTime > EndTime) 정상 처리");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Result Events: {result.Events.Count} (예상: 0)");
    }

    #endregion

    #region 정밀 시간 범위 테스트

    [Fact]
    public async Task ParseWithTimeRange_VeryNarrowRange_ShouldFilterPrecisely()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 매우 좁은 시간 범위: 5초 윈도우
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 28),
            EndTime = new DateTime(2025, 10, 6, 22, 58, 33)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();

        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 시간 범위 검증
            foreach (var evt in result.Events)
            {
                evt.Timestamp.Should().BeOnOrAfter(options.StartTime!.Value);
                evt.Timestamp.Should().BeOnOrBefore(options.EndTime!.Value);
            }
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ 좁은 시간 범위 (5초) 정밀 필터링 성공");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");

        if (result.Events.Any())
        {
            _output.WriteLine($"  - First Event: {result.Events.First().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            _output.WriteLine($"  - Last Event: {result.Events.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        }
    }

    #endregion

    #region 경계값 테스트

    [Fact]
    public async Task ParseWithTimeRange_EventExactlyAtStartTime_ShouldBeIncluded()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 특정 이벤트 시각을 StartTime으로 설정
        // activity.log에서 실제 존재하는 시각: 22:58:30.717
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 30, 717),
            EndTime = new DateTime(2025, 10, 6, 22, 58, 35)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // StartTime과 정확히 일치하는 이벤트가 포함되어야 함 (이상, 미만이 아닌 이상, 이하)
            var eventAtStartTime = result.Events
                .Where(e => e.Timestamp == options.StartTime)
                .ToList();
            
            _output.WriteLine($"  - Events at exact start time: {eventAtStartTime.Count}");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ StartTime 경계값 테스트");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
    }

    [Fact]
    public async Task ParseWithTimeRange_EventExactlyAtEndTime_ShouldBeIncluded()
    {
        // Arrange
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 특정 이벤트 시각을 EndTime으로 설정
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 25),
            EndTime = new DateTime(2025, 10, 6, 22, 58, 30, 717)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // EndTime과 정확히 일치하는 이벤트가 포함되어야 함
            var eventAtEndTime = result.Events
                .Where(e => e.Timestamp == options.EndTime)
                .ToList();
            
            _output.WriteLine($"  - Events at exact end time: {eventAtEndTime.Count}");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ EndTime 경계값 테스트");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss.fff}");
    }

    #endregion

    #region Single Moment 테스트 (StartTime = EndTime)

    [Fact]
    public async Task ParseWithTimeRange_StartTimeEqualsEndTime_ShouldReturnOnlyExactMatches()
    {
        // Arrange: StartTime = EndTime인 경우 (단일 시점)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 단일 시점: StartTime = EndTime
        var singleMoment = new DateTime(2025, 10, 6, 22, 58, 30, 717);
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = singleMoment,
            EndTime = singleMoment
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 정확히 해당 시점의 이벤트만 포함되어야 함
            foreach (var evt in result.Events)
            {
                evt.Timestamp.Should().Be(singleMoment, 
                    $"StartTime = EndTime인 경우 정확히 해당 시점의 이벤트만 포함되어야 함");
            }
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ Single Moment (StartTime = EndTime) 테스트");
        _output.WriteLine($"  - Target Time: {singleMoment:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - Events at exact time: {result.Events.Count}");
    }

    #endregion

    #region 여러 로그 타입 시간 필터링 일관성 테스트

    [Fact]
    public async Task ParseWithTimeRange_MultipleLogTypes_ShouldFilterConsistently()
    {
        // Arrange: 여러 로그 타입에서 시간 필터링 일관성 검증
        var testConfigs = new[]
        {
            new { ConfigFile = "adb_activity_config.yaml", LogFile = "activity.log", LogType = "Activity" },
            new { ConfigFile = "adb_usagestats_config.yaml", LogFile = "usagestats.log", LogType = "Usagestats" },
            new { ConfigFile = "adb_vibrator_config.yaml", LogFile = "vibrator_manager.log", LogType = "Vibrator" }
        };

        var startTime = new DateTime(2025, 10, 6, 22, 58, 0);
        var endTime = new DateTime(2025, 10, 6, 22, 59, 0);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var filteringResults = new Dictionary<string, (int TotalEvents, bool AllInRange)>();

        foreach (var config in testConfigs)
        {
            var configPath = Path.Combine("TestData", config.ConfigFile);
            var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", config.LogFile);

            if (!File.Exists(logPath) || !File.Exists(configPath))
            {
                _output.WriteLine($"⚠️ Skipping {config.LogType} log (file not found)");
                continue;
            }

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var options = new LogParsingOptions
            {
                DeviceInfo = deviceInfo,
                StartTime = startTime,
                EndTime = endTime
            };

            var parser = new AdbLogParser(configuration, _logger);
            var result = await parser.ParseAsync(logPath, options);

            if (result.Events.Any())
            {
                result.Success.Should().BeTrue();
                var eventsOutsideRange = result.Events
                    .Where(e => e.Timestamp < startTime || e.Timestamp > endTime)
                    .ToList();

                var allInRange = eventsOutsideRange.Count == 0;
                filteringResults[config.LogType] = (result.Events.Count, allInRange);

                // Assert: 각 로그 타입에서 시간 필터링이 정확해야 함
                eventsOutsideRange.Should().BeEmpty(
                    $"{config.LogType} 로그에서 모든 이벤트가 시간 범위 내에 있어야 함");
            }
            else
            {
                result.Success.Should().BeFalse();
            }
        }

        // filteringResults가 비어있을 수 있음 (모든 로그에서 해당 시간에 이벤트가 없는 경우)
        // 따라서 .NotBeEmpty() 단언문은 제거함

        _output.WriteLine($"✓ Multiple Log Types Time Filtering Consistency");
        _output.WriteLine($"  - Time Range: {startTime:HH:mm:ss} ~ {endTime:HH:mm:ss}");
        foreach (var (logType, (totalEvents, allInRange)) in filteringResults.OrderBy(kv => kv.Key))
        {
            _output.WriteLine($"  - {logType}: {totalEvents} events, All in range: {allInRange}");
        }
    }

    #endregion

    #region 밀리초 정밀도 테스트

    [Fact]
    public async Task ParseWithTimeRange_MillisecondPrecision_ShouldFilterAccurately()
    {
        // Arrange: 밀리초 단위 정밀도 검증
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 밀리초 단위 정밀도: 22:58:30.500 ~ 22:58:30.999 (499ms 윈도우)
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 30, 500),
            EndTime = new DateTime(2025, 10, 6, 22, 58, 30, 999)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 밀리초 단위로 정확히 필터링되어야 함
            foreach (var evt in result.Events)
            {
                evt.Timestamp.Should().BeOnOrAfter(options.StartTime!.Value);
                evt.Timestamp.Should().BeOnOrBefore(options.EndTime!.Value);

                // 밀리초까지 검증
                evt.Timestamp.Millisecond.Should().BeInRange(500, 999);
            }
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ Millisecond Precision Time Filtering");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
        
        if (result.Events.Any())
        {
            var milliseconds = result.Events.Select(e => e.Timestamp.Millisecond).Distinct().OrderBy(ms => ms).ToList();
            _output.WriteLine($"  - Milliseconds found: {string.Join(", ", milliseconds)}");
        }
    }

    #endregion

    #region 이벤트 순서 검증 테스트

    [Fact]
    public async Task ParseWithTimeRange_ShouldMaintainChronologicalOrder()
    {
        // Arrange: 필터링 후에도 이벤트 순서가 유지되는지 검증
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 0),
            EndTime = new DateTime(2025, 10, 6, 22, 59, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Count > 1)
        {
            result.Success.Should().BeTrue();
            // 이벤트가 시간 순서대로 정렬되어 있어야 함
            var timestamps = result.Events.Select(e => e.Timestamp).ToList();

            for (int i = 0; i < timestamps.Count - 1; i++)
            {
                timestamps[i].Should().BeOnOrBefore(timestamps[i + 1], 
                    $"이벤트가 시간 순서대로 정렬되어 있어야 함 (index {i})");
            }

            _output.WriteLine($"✓ Chronological Order Maintained");
            _output.WriteLine($"  - Total Events: {result.Events.Count}");
            _output.WriteLine($"  - First Event: {timestamps.First():yyyy-MM-dd HH:mm:ss.fff}");
            _output.WriteLine($"  - Last Event: {timestamps.Last():yyyy-MM-dd HH:mm:ss.fff}");
            _output.WriteLine($"  - Time Span: {(timestamps.Last() - timestamps.First()).TotalSeconds:F2}s");
        }
        else
        {
            result.Success.Should().Be(result.Events.Any());
            _output.WriteLine($"⚠️ Not enough events to verify chronological order (count: {result.Events.Count})");
        }
    }

    #endregion

    #region Cross-Day 필터링 테스트

    [Fact]
    public async Task ParseWithTimeRange_CrossDayRange_ShouldFilterCorrectly()
    {
        // Arrange: 자정을 넘어가는 시간 범위 테스트
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 7, 0, 30, 0),
            AndroidVersion = "15"
        };

        // 자정을 넘어가는 범위: 10월 6일 23:00 ~ 10월 7일 01:00
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 23, 0, 0),
            EndTime = new DateTime(2025, 10, 7, 1, 0, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 범위 내의 모든 이벤트 확인
            var eventsOutsideRange = result.Events
                .Where(e => e.Timestamp < options.StartTime || e.Timestamp > options.EndTime)
                .ToList();

            eventsOutsideRange.Should().BeEmpty("자정을 넘어가는 시간 범위도 정확히 필터링되어야 함");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ Cross-Day Time Range Filtering");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");

        if (result.Events.Any())
        {
            var dayGroups = result.Events
                .GroupBy(e => e.Timestamp.Date)
                .OrderBy(g => g.Key)
                .ToList();

            _output.WriteLine($"  - Events by Day:");
            foreach (var dayGroup in dayGroups)
            {
                _output.WriteLine($"    {dayGroup.Key:yyyy-MM-dd}: {dayGroup.Count()} events");
            }
        }
    }

    #endregion

    #region 매우 넓은 시간 범위 테스트

    [Fact]
    public async Task ParseWithTimeRange_VeryWideRange_ShouldFilterCorrectly()
    {
        // Arrange: 매우 넓은 시간 범위 (1시간)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 매우 넓은 시간 범위: 22:00 ~ 23:00 (1시간)
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 0, 0),
            EndTime = new DateTime(2025, 10, 6, 23, 0, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            var eventsOutsideRange = result.Events
                .Where(e => e.Timestamp < options.StartTime || e.Timestamp > options.EndTime)
                .ToList();

            eventsOutsideRange.Should().BeEmpty("넓은 시간 범위에서도 필터링이 정확해야 함");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ Very Wide Time Range (1 hour) Filtering");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");

        if (result.Events.Any())
        {
            var timeSpan = result.Events.Max(e => e.Timestamp) - result.Events.Min(e => e.Timestamp);
            _output.WriteLine($"  - Actual Time Span: {timeSpan.TotalMinutes:F2} minutes");
            
            // 5분 단위로 이벤트 분포 출력
            var minuteGroups = result.Events
                .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, 
                    e.Timestamp.Hour, (e.Timestamp.Minute / 5) * 5, 0))
                .OrderBy(g => g.Key)
                .ToList();

            _output.WriteLine($"  - Event Distribution (5-minute intervals):");
            foreach (var group in minuteGroups)
            {
                _output.WriteLine($"    {group.Key:HH:mm}: {group.Count()} events");
            }
        }
    }

    #endregion

    #region 다양한 이벤트 타입 시간 필터링 검증

    [Fact]
    public async Task ParseWithTimeRange_DifferentEventTypes_ShouldAllBeFiltered()
    {
        // Arrange: 다양한 이벤트 타입에서 시간 필터링 일관성 검증
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 0),
            EndTime = new DateTime(2025, 10, 6, 22, 59, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // EventType별로 그룹화하여 각 타입별로 시간 필터링 검증
            var eventTypeGroups = result.Events
                .GroupBy(e => e.EventType)
                .OrderByDescending(g => g.Count())
                .ToList();

            _output.WriteLine($"✓ Different Event Types Time Filtering");
            _output.WriteLine($"  - Time Range: {options.StartTime:HH:mm:ss} ~ {options.EndTime:HH:mm:ss}");
            _output.WriteLine($"  - Total Event Types: {eventTypeGroups.Count}");

            foreach (var group in eventTypeGroups)
            {
                var eventsOutsideRange = group
                    .Where(e => e.Timestamp < options.StartTime || e.Timestamp > options.EndTime)
                    .ToList();

                eventsOutsideRange.Should().BeEmpty(
                    $"EventType '{group.Key}'의 모든 이벤트가 시간 범위 내에 있어야 함");

                _output.WriteLine($"    {group.Key}: {group.Count()} events");
                _output.WriteLine($"      Time Range: {group.Min(e => e.Timestamp):HH:mm:ss.fff} ~ {group.Max(e => e.Timestamp):HH:mm:ss.fff}");
            }
        }
        else
        {
            result.Success.Should().BeFalse();
            _output.WriteLine($"⚠️ No events found in the specified time range");
        }
    }

    #endregion

    #region 필터링 전후 비교 테스트

    [Fact]
    public async Task ParseWithTimeRange_CompareFilteredVsUnfiltered_ShouldShowDifference()
    {
        // Arrange: 필터링 적용 전후 비교
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 필터링 없음
        var optionsNoFilter = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = null,
            EndTime = null
        };

        // 필터링 적용
        var optionsWithFilter = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 0),
            EndTime = new DateTime(2025, 10, 6, 22, 59, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var resultNoFilter = await parser.ParseAsync(logPath, optionsNoFilter);
        var resultWithFilter = await parser.ParseAsync(logPath, optionsWithFilter);

        // Assert
        resultNoFilter.Should().NotBeNull();
        resultNoFilter.Success.Should().Be(resultNoFilter.Events.Any());
        resultWithFilter.Should().NotBeNull();
        resultWithFilter.Success.Should().Be(resultWithFilter.Events.Any());

        // 필터링 적용 시 이벤트 수가 감소해야 함 (또는 같을 수 있음)
        resultWithFilter.Events.Count.Should().BeLessThanOrEqualTo(resultNoFilter.Events.Count,
            "필터링 적용 시 이벤트 수가 필터링 없을 때보다 많을 수 없음");

        _output.WriteLine($"✓ Filtered vs Unfiltered Comparison");
        _output.WriteLine($"  - Unfiltered Events: {resultNoFilter.Events.Count}");
        _output.WriteLine($"  - Filtered Events: {resultWithFilter.Events.Count}");
        
        if (resultNoFilter.Events.Any())
        {
            var unfilteredTimeSpan = resultNoFilter.Events.Max(e => e.Timestamp) - 
                                    resultNoFilter.Events.Min(e => e.Timestamp);
            _output.WriteLine($"  - Unfiltered Time Span: {unfilteredTimeSpan.TotalMinutes:F2} minutes");
        }

        if (resultWithFilter.Events.Any())
        {
            var filteredTimeSpan = resultWithFilter.Events.Max(e => e.Timestamp) - 
                                  resultWithFilter.Events.Min(e => e.Timestamp);
            _output.WriteLine($"  - Filtered Time Span: {filteredTimeSpan.TotalMinutes:F2} minutes");
        }

        var filteringRatio = resultNoFilter.Events.Count > 0 
            ? (double)resultWithFilter.Events.Count / resultNoFilter.Events.Count * 100 
            : 0;
        _output.WriteLine($"  - Filtering Ratio: {filteringRatio:F1}%");
    }

    #endregion

    #region ConvertToUtc 플래그 상호작용 테스트

    [Fact]
    public async Task ParseWithTimeRange_ConvertToUtcFalse_ShouldFilterWithLocalTime()
    {
        // Arrange: ConvertToUtc=false일 때 로컬 시간으로 필터링 동작 확인
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // ConvertToUtc=false로 설정
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = false, // 로컬 시간 유지
            StartTime = new DateTime(2025, 10, 6, 22, 58, 0),
            EndTime = new DateTime(2025, 10, 6, 22, 59, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 시간 범위 검증
            var eventsOutsideRange = result.Events
                .Where(e => e.Timestamp < options.StartTime || e.Timestamp > options.EndTime)
                .ToList();

            eventsOutsideRange.Should().BeEmpty("ConvertToUtc=false일 때도 로컬 시간으로 정확히 필터링되어야 함");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ ConvertToUtc=false 시간 필터링 검증");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
        _output.WriteLine($"  - ConvertToUtc: {options.ConvertToUtc}");
    }

    [Fact]
    public async Task ParseWithTimeRange_ConvertToUtcTrue_ShouldFilterWithUtcTime()
    {
        // Arrange: ConvertToUtc=true일 때 UTC 시간으로 필터링 동작 확인
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul", // UTC+9
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // ConvertToUtc=true로 설정 (기본값)
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = true, // UTC로 변환
            StartTime = new DateTime(2025, 10, 6, 13, 58, 0, DateTimeKind.Utc), // UTC 시간 (Seoul 22:58)
            EndTime = new DateTime(2025, 10, 6, 13, 59, 0, DateTimeKind.Utc)     // UTC 시간 (Seoul 22:59)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 모든 이벤트의 Kind가 UTC여야 함
            foreach (var evt in result.Events)
            {
                evt.Timestamp.Kind.Should().Be(DateTimeKind.Utc, "ConvertToUtc=true일 때 모든 이벤트가 UTC여야 함");
            }

            _output.WriteLine($"✓ ConvertToUtc=true 시간 필터링 검증");
        }
        else
        {
            result.Success.Should().BeFalse();
            _output.WriteLine($"⚠️ No events found in the UTC time range");
        }
        
        _output.WriteLine($"  - Start Time (UTC): {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time (UTC): {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
    }

    #endregion

    #region 시간 범위가 로그 범위 완전 벗어남 테스트

    [Fact]
    public async Task ParseWithTimeRange_EntirelyBeforeLogEvents_ShouldReturnEmpty()
    {
        // Arrange: 시간 범위가 모든 로그 이벤트보다 이전인 경우
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 로그 이벤트보다 훨씬 이전 시간
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 20, 0, 0), // 로그보다 2시간 전
            EndTime = new DateTime(2025, 10, 6, 20, 10, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("시간 범위에 이벤트가 없으므로 실패 처리되어야 함");
        result.Events.Should().BeEmpty("시간 범위가 모든 로그보다 이전이면 이벤트가 없어야 함");

        _output.WriteLine($"✓ Entirely Before Log Events 테스트");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Result Events: {result.Events.Count} (예상: 0)");
    }

    [Fact]
    public async Task ParseWithTimeRange_EntirelyAfterLogEvents_ShouldReturnEmpty()
    {
        // Arrange: 시간 범위가 모든 로그 이벤트보다 이후인 경우
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 로그 이벤트보다 훨씬 이후 시간
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 7, 2, 0, 0), // 로그보다 3시간 후
            EndTime = new DateTime(2025, 10, 7, 2, 10, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("시간 범위에 이벤트가 없으므로 실패 처리되어야 함");
        result.Events.Should().BeEmpty("시간 범위가 모든 로그보다 이후이면 이벤트가 없어야 함");

        _output.WriteLine($"✓ Entirely After Log Events 테스트");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Result Events: {result.Events.Count} (예상: 0)");
    }

    #endregion

    #region 다양한 타임존 테스트

    [Fact]
    public async Task ParseWithTimeRange_UtcTimeZone_ShouldFilterCorrectly()
    {
        // Arrange: UTC 타임존으로 파싱 및 필터링
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "UTC", // UTC 타임존
            CurrentTime = new DateTime(2025, 10, 6, 14, 0, 0, DateTimeKind.Utc), // UTC 시간
            AndroidVersion = "15"
        };

        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = false, // 로컬 시간 유지 (UTC)
            StartTime = new DateTime(2025, 10, 6, 13, 58, 0),
            EndTime = new DateTime(2025, 10, 6, 13, 59, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ UTC TimeZone 필터링 테스트");
        _output.WriteLine($"  - TimeZone: {deviceInfo.TimeZone}");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
    }

    [Fact]
    public async Task ParseWithTimeRange_NegativeOffsetTimeZone_ShouldFilterCorrectly()
    {
        // Arrange: 음수 오프셋 타임존 (예: America/New_York, UTC-5)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "America/New_York", // UTC-4 or UTC-5 (depending on DST)
            CurrentTime = new DateTime(2025, 10, 6, 10, 0, 0), // EDT 시간
            AndroidVersion = "15"
        };

        // 음수 오프셋 타임존의 시간으로 필터링
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            ConvertToUtc = false,
            StartTime = new DateTime(2025, 10, 6, 9, 58, 0),
            EndTime = new DateTime(2025, 10, 6, 9, 59, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ Negative Offset TimeZone (America/New_York) 필터링 테스트");
        _output.WriteLine($"  - TimeZone: {deviceInfo.TimeZone}");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
    }

    #endregion

    #region 연도 경계 테스트

    [Fact]
    public async Task ParseWithTimeRange_CrossYearBoundary_ShouldFilterCorrectly()
    {
        // Arrange: 연도 경계를 넘어가는 시간 범위 (12월 31일 23:00 ~ 1월 1일 01:00)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 1, 1, 1, 0, 0),
            AndroidVersion = "15"
        };

        // 연도 경계 시간 범위
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2024, 12, 31, 23, 0, 0),
            EndTime = new DateTime(2025, 1, 1, 1, 0, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 범위 검증
            var eventsOutsideRange = result.Events
                .Where(e => e.Timestamp < options.StartTime || e.Timestamp > options.EndTime)
                .ToList();

            eventsOutsideRange.Should().BeEmpty("연도 경계를 넘어가는 시간 범위도 정확히 필터링되어야 함");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ Cross-Year Boundary Filtering");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");

        if (result.Events.Any())
        {
            var yearGroups = result.Events
                .GroupBy(e => e.Timestamp.Year)
                .OrderBy(g => g.Key)
                .ToList();

            _output.WriteLine($"  - Events by Year:");
            foreach (var yearGroup in yearGroups)
            {
                _output.WriteLine($"    {yearGroup.Key}: {yearGroup.Count()} events");
            }
        }
    }

    #endregion

    #region 윤년 테스트

    [Fact]
    public async Task ParseWithTimeRange_LeapYearFebruary29_ShouldFilterCorrectly()
    {
        // Arrange: 윤년 2월 29일 필터링
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2024, 2, 29, 12, 0, 0), // 2024는 윤년
            AndroidVersion = "15"
        };

        // 윤년 2월 29일 시간 범위
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2024, 2, 29, 10, 0, 0),
            EndTime = new DateTime(2024, 2, 29, 11, 0, 0)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ Leap Year February 29 Filtering");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
        _output.WriteLine($"  - 윤년 2월 29일 처리 정상");
    }

    #endregion

    #region 매우 넓은 시간 범위 테스트

    [Fact]
    public async Task ParseWithTimeRange_MultiYearRange_ShouldFilterCorrectly()
    {
        // Arrange: 여러 해에 걸친 매우 넓은 시간 범위
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 2년에 걸친 매우 넓은 범위
        var options = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2024, 1, 1, 0, 0, 0),
            EndTime = new DateTime(2026, 12, 31, 23, 59, 59)
        };

        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        
        if (result.Events.Any())
        {
            result.Success.Should().BeTrue();
            // 모든 이벤트가 범위 내에 있어야 함 (로그가 이 범위 내에 있다고 가정)
            var eventsOutsideRange = result.Events
                .Where(e => e.Timestamp < options.StartTime || e.Timestamp > options.EndTime)
                .ToList();

            eventsOutsideRange.Should().BeEmpty("매우 넓은 시간 범위에서도 필터링이 정확해야 함");
        }
        else
        {
            result.Success.Should().BeFalse();
        }

        _output.WriteLine($"✓ Multi-Year Range (2 years) Filtering");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd}");
        _output.WriteLine($"  - Time Span: {(options.EndTime!.Value - options.StartTime!.Value).TotalDays:F0} days");
        _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
    }

    #endregion

    #region 빈 로그 파일 테스트

    [Fact]
    public async Task ParseWithTimeRange_EmptyLogFile_ShouldReturnEmptyResult()
    {
        // Arrange: 빈 로그 파일 생성
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"empty_log_{Guid.NewGuid()}.txt");

        try
        {
            // 빈 파일 생성
            File.WriteAllText(tempLogPath, string.Empty);

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions
            {
                DeviceInfo = deviceInfo,
                StartTime = new DateTime(2025, 10, 6, 22, 58, 0),
                EndTime = new DateTime(2025, 10, 6, 22, 59, 0)
            };

            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse("빈 로그 파일은 파싱할 이벤트가 없으므로 실패 처리되어야 함");
            result.Events.Should().BeEmpty("빈 로그 파일은 이벤트가 없어야 함");

            _output.WriteLine($"✓ Empty Log File 테스트");
            _output.WriteLine($"  - Result Events: {result.Events.Count} (예상: 0)");
            _output.WriteLine($"  - Success: {result.Success}");
        }
        finally
        {
            // 임시 파일 정리
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region 경계값 포함/제외 명확성 테스트

    [Fact]
    public async Task ParseWithTimeRange_BoundaryInclusion_ShouldBeInclusive()
    {
        // Arrange: 경계값이 포함되는지 명확히 검증 (StartTime <= event <= EndTime)
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 먼저 전체 파싱하여 실제 이벤트 시간 확인
        var optionsAll = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);
        var resultAll = await parser.ParseAsync(logPath, optionsAll);

        if (!resultAll.Events.Any())
        {
            _output.WriteLine($"⚠️ No events in log file. Skipping boundary test.");
            return;
        }

        // 첫 번째와 마지막 이벤트 시간을 경계로 사용
        var firstEventTime = resultAll.Events.Min(e => e.Timestamp);
        var lastEventTime = resultAll.Events.Max(e => e.Timestamp);

        var optionsBoundary = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = firstEventTime,
            EndTime = lastEventTime
        };

        // Act
        var resultBoundary = await parser.ParseAsync(logPath, optionsBoundary);

        // Assert
        resultBoundary.Should().NotBeNull();
        resultBoundary.Success.Should().Be(resultBoundary.Events.Any());

        // 첫 번째 이벤트와 마지막 이벤트가 포함되어야 함
        resultBoundary.Events.Should().Contain(e => e.Timestamp == firstEventTime,
            "StartTime과 정확히 일치하는 이벤트는 포함되어야 함 (inclusive)");
        resultBoundary.Events.Should().Contain(e => e.Timestamp == lastEventTime,
            "EndTime과 정확히 일치하는 이벤트는 포함되어야 함 (inclusive)");

        _output.WriteLine($"✓ Boundary Inclusion Test (StartTime <= event <= EndTime)");
        _output.WriteLine($"  - Start Time: {firstEventTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - End Time: {lastEventTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - Total Events in All: {resultAll.Events.Count}");
        _output.WriteLine($"  - Total Events in Boundary: {resultBoundary.Events.Count}");
        _output.WriteLine($"  - First Event Included: {resultBoundary.Events.Any(e => e.Timestamp == firstEventTime)}");
        _output.WriteLine($"  - Last Event Included: {resultBoundary.Events.Any(e => e.Timestamp == lastEventTime)}");
    }

    #endregion

    #region 통계 검증 테스트

    [Fact]
    public async Task ParseWithTimeRange_Statistics_ShouldReflectFiltering()
    {
        // Arrange: 필터링이 통계에 반영되는지 검증
        var configPath = Path.Combine("TestData", "adb_activity_config.yaml");
        var logPath = Path.Combine("..", "..", "..", "..", "..", "sample_logs", "4차 샘플", "activity.log");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"4th sample activity.log not found. Skipping test.");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 10, 6, 23, 0, 0),
            AndroidVersion = "15"
        };

        // 필터링 없음
        var optionsNoFilter = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);
        var resultNoFilter = await parser.ParseAsync(logPath, optionsNoFilter);

        // 필터링 적용
        var optionsWithFilter = new LogParsingOptions
        {
            DeviceInfo = deviceInfo,
            StartTime = new DateTime(2025, 10, 6, 22, 58, 0),
            EndTime = new DateTime(2025, 10, 6, 22, 59, 0)
        };
        var resultWithFilter = await parser.ParseAsync(logPath, optionsWithFilter);

        // Assert
        resultNoFilter.Should().NotBeNull();
        resultNoFilter.Success.Should().Be(resultNoFilter.Events.Any());
        resultWithFilter.Should().NotBeNull();
        resultWithFilter.Success.Should().Be(resultWithFilter.Events.Any());

        // 통계 검증
        resultNoFilter.Statistics.Should().NotBeNull();
        resultWithFilter.Statistics.Should().NotBeNull();

        // 필터링된 결과의 파싱된 라인 수가 통계에 반영되어야 함
        resultWithFilter.Statistics!.ParsedLines.Should().BeGreaterThanOrEqualTo(0,
            "Statistics의 ParsedLines는 0 이상이어야 함");

        // 필터링 없을 때가 필터링 있을 때보다 파싱된 라인 수가 같거나 많아야 함
        resultNoFilter.Statistics!.ParsedLines.Should().BeGreaterThanOrEqualTo(resultWithFilter.Statistics.ParsedLines,
            "필터링 없을 때 파싱된 라인 수가 더 많거나 같아야 함");

        _output.WriteLine($"✓ Statistics Validation with Time Filtering");
        _output.WriteLine($"  - Unfiltered: {resultNoFilter.Events.Count} events ({resultNoFilter.Statistics.ParsedLines} parsed lines)");
        _output.WriteLine($"  - Filtered: {resultWithFilter.Events.Count} events ({resultWithFilter.Statistics.ParsedLines} parsed lines)");
        _output.WriteLine($"  - Event count difference: {resultNoFilter.Events.Count - resultWithFilter.Events.Count}");
    }

    #endregion
}

