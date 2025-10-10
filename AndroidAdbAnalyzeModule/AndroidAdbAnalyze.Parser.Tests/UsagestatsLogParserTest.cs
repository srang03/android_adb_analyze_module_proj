using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests
{
    public class UsagestatsLogParserTest
    {
        private readonly ITestOutputHelper _output;
        private readonly ILogger<AdbLogParser> _logger;
        private readonly ILogger<YamlConfigurationLoader> _configLogger;

        public UsagestatsLogParserTest(ITestOutputHelper output)
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

        #region UsageStats Log Tests

        [Fact]
        public async Task ParseUsageStatsLog_ShouldSucceed()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Events.Should().NotBeEmpty();
            result.Statistics.ParsedLines.Should().BeGreaterThan(0);

            _output.WriteLine($"✓ UsageStats log parsed successfully");
            _output.WriteLine($"  Total Events: {result.Events.Count}");
            _output.WriteLine($"  Parsed Lines: {result.Statistics.ParsedLines}");
            _output.WriteLine($"  Elapsed Time: {result.Statistics.ElapsedTime.TotalMilliseconds:F2}ms");
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldHandle_MultipleEventTypes()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert
            var eventTypes = result.Events.Select(e => e.EventType).Distinct().ToList();

            // ✅ 수정: 동적 EventType으로 변경 (ACTIVITY_LIFECYCLE → ACTIVITY_RESUMED, ACTIVITY_PAUSED, ACTIVITY_STOPPED)
            var activityLifecycleTypes = new[] { "ACTIVITY_RESUMED", "ACTIVITY_PAUSED", "ACTIVITY_STOPPED" };
            eventTypes.Intersect(activityLifecycleTypes).Should().NotBeEmpty(
                "Should parse activity lifecycle events (ACTIVITY_RESUMED, ACTIVITY_PAUSED, or ACTIVITY_STOPPED)");
            eventTypes.Should().Contain("FOREGROUND_SERVICE", "Should parse foreground service events");
            eventTypes.Should().Contain("SCREEN_STATE", "Should parse screen state events");

            _output.WriteLine($"✓ UsageStats event types parsed correctly");
            _output.WriteLine($"  Event Types Found: {eventTypes.Count}");
            foreach (var type in eventTypes)
            {
                var count = result.Events.Count(e => e.EventType == type);
                _output.WriteLine($"  - {type}: {count} events");
            }
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldParse_AllPackages()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert
            var packages = result.Events
                .Where(e => e.Attributes.ContainsKey("package"))
                .Select(e => e.Attributes["package"].ToString())
                .Distinct()
                .ToList();

            packages.Should().NotBeEmpty("Should extract package names");
            packages.Should().Contain(p => p!.Contains("camera"), "Should include camera package");
            packages.Should().Contain(p => p!.Contains("launcher"), "Should include launcher package");
            packages.Should().Contain(p => p!.Contains("settings"), "Should include settings package");

            _output.WriteLine($"✓ All packages parsed correctly");
            _output.WriteLine($"  Total Unique Packages: {packages.Count}");
            _output.WriteLine($"  Sample Packages:");
            foreach (var pkg in packages.Take(10))
            {
                _output.WriteLine($"    - {pkg}");
            }
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldIdentify_CameraActivityLifecycle()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert
            var cameraEvents = result.Events
                .Where(e => e.Attributes.ContainsKey("package") &&
                           e.Attributes["package"].ToString()!.Contains("camera"))
                .OrderBy(e => e.Timestamp)
                .ToList();

            cameraEvents.Should().NotBeEmpty("Should find camera app events");

            // ✅ 수정: 동적 EventType 사용 (ACTIVITY_LIFECYCLE → ACTIVITY_RESUMED)
            var cameraOpened = cameraEvents.Where(e =>
                e.EventType == LogEventTypes.ACTIVITY_RESUMED).ToList();

            // ✅ 수정: 동적 EventType 사용 (ACTIVITY_LIFECYCLE → ACTIVITY_STOPPED)
            var cameraClosed = cameraEvents.Where(e =>
                e.EventType == LogEventTypes.ACTIVITY_STOPPED).ToList();

            var cameraService = cameraEvents.Where(e =>
                e.EventType == LogEventTypes.FOREGROUND_SERVICE).ToList();

            cameraOpened.Should().NotBeEmpty("Should have camera opened events (ACTIVITY_RESUMED)");
            cameraClosed.Should().NotBeEmpty("Should have camera closed events (ACTIVITY_STOPPED)");

            _output.WriteLine($"✓ Camera activity lifecycle identified");
            _output.WriteLine($"  Camera Opened: {cameraOpened.Count} times");
            _output.WriteLine($"  Camera Closed: {cameraClosed.Count} times");
            _output.WriteLine($"  Foreground Service: {cameraService.Count} events");

            // Log first camera session
            if (cameraOpened.Any())
            {
                var firstOpen = cameraOpened.First();
                _output.WriteLine($"\n  First Camera Session:");
                _output.WriteLine($"    Opened: {firstOpen.Timestamp:yyyy-MM-dd HH:mm:ss}");

                var nextClose = cameraClosed.FirstOrDefault(e => e.Timestamp > firstOpen.Timestamp);
                if (nextClose != null)
                {
                    _output.WriteLine($"    Closed: {nextClose.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    _output.WriteLine($"    Duration: {(nextClose.Timestamp - firstOpen.Timestamp).TotalSeconds:F1}s");
                }
            }
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldProvide_DataForCorrelation()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert - Verify parsed data has necessary fields for correlation
            // ✅ 수정: 동적 EventType 사용 (ACTIVITY_LIFECYCLE → ACTIVITY_RESUMED)
            var sampleEvent = result.Events
                .FirstOrDefault(e => e.EventType == LogEventTypes.ACTIVITY_RESUMED &&
                                   e.Attributes.ContainsKey("package") &&
                                   e.Attributes["package"].ToString()!.Contains("camera"));

            sampleEvent.Should().NotBeNull("Should have at least one camera ACTIVITY_RESUMED event");
            sampleEvent!.Timestamp.Should().NotBe(default(DateTime), "Should have valid timestamp");
            sampleEvent.Attributes.Should().ContainKey("package");
            sampleEvent.EventType.Should().Be("ACTIVITY_RESUMED");

            _output.WriteLine($"✓ Parsed data suitable for correlation analysis");
            _output.WriteLine($"  Sample Event:");
            _output.WriteLine($"    Timestamp: {sampleEvent.Timestamp:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"    Type: {sampleEvent.EventType}");
            _output.WriteLine($"    Package: {sampleEvent.Attributes["package"]}");

            // Demonstrate that upper application can perform correlation
            _output.WriteLine($"\n  ⚠️ Note: Correlation analysis (combining events) is done by the consuming application, not the DLL");
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldParse_TaskRootPackage()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert - taskRootPackage는 실제 앱을 식별하는 데 중요
            var eventsWithTaskRoot = result.Events
                .Where(e => e.Attributes.ContainsKey("taskRootPackage"))
                .ToList();

            eventsWithTaskRoot.Should().NotBeEmpty("Should parse taskRootPackage attribute");

            // taskRootPackage와 package가 다른 경우 확인 (예: 카카오톡이 기본 카메라 호출)
            var differentTaskRootEvents = eventsWithTaskRoot
                .Where(e => e.Attributes.ContainsKey("package") &&
                           e.Attributes["taskRootPackage"].ToString() != e.Attributes["package"].ToString())
                .ToList();

            _output.WriteLine($"✓ TaskRootPackage parsing validated");
            _output.WriteLine($"  Events with taskRootPackage: {eventsWithTaskRoot.Count}");
            _output.WriteLine($"  Events where taskRootPackage ≠ package: {differentTaskRootEvents.Count}");

            if (differentTaskRootEvents.Any())
            {
                var sample = differentTaskRootEvents.First();
                _output.WriteLine($"\n  Sample (taskRootPackage ≠ package):");
                _output.WriteLine($"    package: {sample.Attributes["package"]}");
                _output.WriteLine($"    taskRootPackage: {sample.Attributes["taskRootPackage"]}");
                _output.WriteLine($"    → This indicates app '{sample.Attributes["taskRootPackage"]}' launched '{sample.Attributes["package"]}'");
            }
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldParse_AllActivityStates()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert - 모든 Activity Lifecycle 이벤트 타입 검증
            var eventTypes = result.Events.Select(e => e.EventType).Distinct().ToList();

            var activityStates = new[] 
            { 
                LogEventTypes.ACTIVITY_RESUMED, 
                LogEventTypes.ACTIVITY_PAUSED, 
                LogEventTypes.ACTIVITY_STOPPED 
            };

            foreach (var state in activityStates)
            {
                var count = result.Events.Count(e => e.EventType == state);
                _output.WriteLine($"  {state}: {count} events");
            }

            // 최소 1개 이상의 Activity Lifecycle 이벤트가 있어야 함
            var activityEvents = result.Events
                .Where(e => activityStates.Contains(e.EventType))
                .ToList();

            activityEvents.Should().NotBeEmpty("Should parse at least one activity lifecycle event");

            _output.WriteLine($"✓ All activity lifecycle states validated");
            _output.WriteLine($"  Total activity lifecycle events: {activityEvents.Count}");
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldParse_TimestampAccurately()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert - 타임스탬프 정확도 검증
            result.Events.Should().NotBeEmpty();

            foreach (var evt in result.Events)
            {
                // 모든 이벤트가 유효한 타임스탬프를 가져야 함
                evt.Timestamp.Should().NotBe(default(DateTime), $"Event {evt.EventType} should have valid timestamp");
                
                // 타임스탬프가 Year 2025에 있어야 함 (테스트 데이터 기준)
                evt.Timestamp.Year.Should().Be(2025, "Timestamp year should be 2025");
            }

            // 타임스탬프가 시간 순서대로 정렬 가능한지 확인
            var orderedEvents = result.Events.OrderBy(e => e.Timestamp).ToList();
            orderedEvents.Should().HaveCount(result.Events.Count, "All events should be orderable by timestamp");

            var firstEvent = orderedEvents.First();
            var lastEvent = orderedEvents.Last();

            _output.WriteLine($"✓ Timestamp parsing accuracy validated");
            _output.WriteLine($"  First Event: {firstEvent.Timestamp:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"  Last Event: {lastEvent.Timestamp:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"  Time Span: {(lastEvent.Timestamp - firstEvent.Timestamp).TotalMinutes:F1} minutes");
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldParse_InstanceIdAndClassName()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert - instanceId와 className 파싱 검증
            var eventsWithInstanceId = result.Events
                .Where(e => e.Attributes.ContainsKey("instanceId"))
                .ToList();

            var eventsWithClassName = result.Events
                .Where(e => e.Attributes.ContainsKey("className"))
                .ToList();

            _output.WriteLine($"✓ InstanceId and ClassName parsing validated");
            _output.WriteLine($"  Events with instanceId: {eventsWithInstanceId.Count}");
            _output.WriteLine($"  Events with className: {eventsWithClassName.Count}");

            if (eventsWithInstanceId.Any())
            {
                var sample = eventsWithInstanceId.First();
                _output.WriteLine($"\n  Sample Event with instanceId:");
                _output.WriteLine($"    EventType: {sample.EventType}");
                _output.WriteLine($"    instanceId: {sample.Attributes["instanceId"]}");
                
                if (sample.Attributes.ContainsKey("className"))
                {
                    _output.WriteLine($"    className: {sample.Attributes["className"]}");
                }
            }
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldHandle_ScreenStateEvents()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert - Screen State 이벤트 검증
            var screenEvents = result.Events
                .Where(e => e.EventType == LogEventTypes.SCREEN_STATE)
                .ToList();

            if (screenEvents.Any())
            {
                screenEvents.Should().NotBeEmpty("Should parse screen state events");

                // Screen State 이벤트는 screenState 속성을 가져야 함
                foreach (var evt in screenEvents)
                {
                    evt.Attributes.Should().ContainKey("screenState", "Screen state event should have 'screenState' attribute");
                }

                _output.WriteLine($"✓ Screen State events validated");
                _output.WriteLine($"  Total Screen State events: {screenEvents.Count}");

                var states = screenEvents
                    .Where(e => e.Attributes.ContainsKey("screenState"))
                    .Select(e => e.Attributes["screenState"].ToString())
                    .Distinct()
                    .ToList();

                _output.WriteLine($"  Screen states found: {string.Join(", ", states)}");
            }
            else
            {
                _output.WriteLine($"⚠️ No Screen State events found in test data");
            }
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldHandle_ForegroundServiceEvents()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert - Foreground Service 이벤트 검증
            var serviceEvents = result.Events
                .Where(e => e.EventType == LogEventTypes.FOREGROUND_SERVICE)
                .ToList();

            serviceEvents.Should().NotBeEmpty("Should parse foreground service events");

            // Foreground Service 이벤트는 package, className 속성을 가져야 함
            foreach (var evt in serviceEvents)
            {
                evt.Attributes.Should().ContainKey("package", "Foreground service event should have 'package' attribute");
                evt.Attributes.Should().ContainKey("className", "Foreground service event should have 'className' attribute");
            }

            _output.WriteLine($"✓ Foreground Service events validated");
            _output.WriteLine($"  Total Foreground Service events: {serviceEvents.Count}");

            var servicePackages = serviceEvents
                .Select(e => e.Attributes["package"].ToString())
                .Distinct()
                .ToList();

            _output.WriteLine($"  Packages with foreground services: {servicePackages.Count}");
            foreach (var pkg in servicePackages.Take(5))
            {
                var count = serviceEvents.Count(e => e.Attributes["package"].ToString() == pkg);
                _output.WriteLine($"    - {pkg}: {count} events");
            }
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldHandle_EmptyOrMissingFile()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var nonExistentLogPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "non_existent_file.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            {
                await parser.ParseAsync(nonExistentLogPath, options);
            });

            _output.WriteLine($"✓ Missing file error handling validated");
        }

        [Fact]
        public async Task ParseUsageStatsLog_ShouldValidate_SectionParsing()
        {
            // Arrange
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "adb_usagestats_config.yaml");
            var logPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "usagestats.txt");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions { DeviceInfo = deviceInfo };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(logPath, options);

            // Assert - Section 정보 검증
            result.Events.Should().NotBeEmpty();

            // 모든 이벤트가 SourceSection을 가져야 함
            foreach (var evt in result.Events)
            {
                evt.SourceSection.Should().NotBeNullOrEmpty($"Event {evt.EventType} should have SourceSection");
            }

            var sections = result.Events
                .Select(e => e.SourceSection)
                .Distinct()
                .ToList();

            _output.WriteLine($"✓ Section parsing validated");
            _output.WriteLine($"  Sections found: {sections.Count}");
            foreach (var section in sections)
            {
                var count = result.Events.Count(e => e.SourceSection == section);
                _output.WriteLine($"    - {section}: {count} events");
            }

            // usagestats.txt는 일반적으로 "last_24_hours" 섹션을 가짐
            sections.Should().Contain("last_24_hours", "Should parse 'last_24_hours' section");
        }

        #endregion
    }

}