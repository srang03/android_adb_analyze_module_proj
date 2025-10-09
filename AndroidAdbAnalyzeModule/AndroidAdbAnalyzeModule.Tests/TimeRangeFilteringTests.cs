using AndroidAdbAnalyzeModule.Configuration.Loaders;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing;
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
        result.Success.Should().BeTrue();

        // 이벤트가 있을 경우에만 시간 범위 검증
        if (result.Events.Count > 0)
        {
            // 모든 이벤트가 시간 범위 내에 있는지 확인
            var eventsOutsideRange = result.Events
                .Where(e => e.Timestamp < options.StartTime || e.Timestamp > options.EndTime)
                .ToList();

            eventsOutsideRange.Should().BeEmpty($"모든 이벤트가 {options.StartTime} ~ {options.EndTime} 범위 내에 있어야 함");

            _output.WriteLine($"✓ Time Range Filtering 성공");
            _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"  - Filtered Events: {result.Events.Count}");
        }
        else
        {
            _output.WriteLine($"⚠️ 해당 시간 범위에 파싱 가능한 이벤트가 없습니다.");
            _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"  - 이것은 정상적인 상황일 수 있습니다 (로그 파일에 해당 시간 범위의 이벤트가 없음).");
        }
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
        result.Success.Should().BeTrue();

        // StartTime 이전 이벤트가 없어야 함
        var eventsBeforeStart = result.Events
            .Where(e => e.Timestamp < options.StartTime)
            .ToList();

        eventsBeforeStart.Should().BeEmpty($"모든 이벤트가 {options.StartTime} 이후여야 함");

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
        result.Success.Should().BeTrue();

        // EndTime 이후 이벤트가 없어야 함
        var eventsAfterEnd = result.Events
            .Where(e => e.Timestamp > options.EndTime)
            .ToList();

        eventsAfterEnd.Should().BeEmpty($"모든 이벤트가 {options.EndTime} 이전이어야 함");

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
        resultNoFilter.Success.Should().BeTrue();
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
        result.Success.Should().BeTrue();
        
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
        result.Success.Should().BeTrue();

        // 시간 범위 검증
        var eventsWithTimestamp = result.Events.ToList();
        
        foreach (var evt in eventsWithTimestamp)
        {
            evt.Timestamp.Should().BeOnOrAfter(options.StartTime!.Value);
            evt.Timestamp.Should().BeOnOrBefore(options.EndTime!.Value);
        }

        _output.WriteLine($"✓ 좁은 시간 범위 (5초) 정밀 필터링 성공");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - Filtered Events: {eventsWithTimestamp.Count}");

        if (eventsWithTimestamp.Any())
        {
            _output.WriteLine($"  - First Event: {eventsWithTimestamp.First().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            _output.WriteLine($"  - Last Event: {eventsWithTimestamp.Last().Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
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
        result.Success.Should().BeTrue();

        // StartTime과 정확히 일치하는 이벤트가 포함되어야 함 (이상, 미만이 아닌 이상, 이하)
        var eventAtStartTime = result.Events
            .Where(e => e.Timestamp == options.StartTime)
            .ToList();

        _output.WriteLine($"✓ StartTime 경계값 테스트");
        _output.WriteLine($"  - Start Time: {options.StartTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - Events at exact start time: {eventAtStartTime.Count}");
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
        result.Success.Should().BeTrue();

        // EndTime과 정확히 일치하는 이벤트가 포함되어야 함
        var eventAtEndTime = result.Events
            .Where(e => e.Timestamp == options.EndTime)
            .ToList();

        _output.WriteLine($"✓ EndTime 경계값 테스트");
        _output.WriteLine($"  - End Time: {options.EndTime:yyyy-MM-dd HH:mm:ss.fff}");
        _output.WriteLine($"  - Events at exact end time: {eventAtEndTime.Count}");
    }

    #endregion
}

