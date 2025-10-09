using AndroidAdbAnalyzeModule.Configuration.Loaders;
using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
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

            eventTypes.Should().Contain("ACTIVITY_LIFECYCLE", "Should parse activity lifecycle events");
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

            // Verify we have camera lifecycle events
            var cameraOpened = cameraEvents.Where(e =>
                e.EventType == LogEventTypes.ACTIVITY_LIFECYCLE &&
                e.Attributes.ContainsKey("activityState") &&
                e.Attributes["activityState"].ToString() == "ACTIVITY_RESUMED").ToList();

            var cameraClosed = cameraEvents.Where(e =>
                e.EventType == LogEventTypes.ACTIVITY_LIFECYCLE &&
                e.Attributes.ContainsKey("activityState") &&
                e.Attributes["activityState"].ToString() == "ACTIVITY_STOPPED").ToList();

            var cameraService = cameraEvents.Where(e =>
                e.EventType == LogEventTypes.FOREGROUND_SERVICE).ToList();

            cameraOpened.Should().NotBeEmpty("Should have camera opened events");
            cameraClosed.Should().NotBeEmpty("Should have camera closed events");

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
            var sampleEvent = result.Events
                .FirstOrDefault(e => e.EventType == LogEventTypes.ACTIVITY_LIFECYCLE &&
                                   e.Attributes.ContainsKey("package") &&
                                   e.Attributes["package"].ToString()!.Contains("camera"));

            sampleEvent.Should().NotBeNull("Should have at least one camera event");
            sampleEvent!.Timestamp.Should().NotBe(default(DateTime), "Should have valid timestamp");
            sampleEvent.Attributes.Should().ContainKey("package");
            sampleEvent.Attributes.Should().ContainKey("activityState");
            sampleEvent.EventType.Should().Be("ACTIVITY_LIFECYCLE");

            _output.WriteLine($"✓ Parsed data suitable for correlation analysis");
            _output.WriteLine($"  Sample Event:");
            _output.WriteLine($"    Timestamp: {sampleEvent.Timestamp:yyyy-MM-dd HH:mm:ss}");
            _output.WriteLine($"    Type: {sampleEvent.EventType}");
            _output.WriteLine($"    Package: {sampleEvent.Attributes["package"]}");
            _output.WriteLine($"    State: {sampleEvent.Attributes["activityState"]}");

            // Demonstrate that upper application can perform correlation
            _output.WriteLine($"\n  ⚠️ Note: Correlation analysis (combining events) is done by the consuming application, not the DLL");
        }

        #endregion
    }

}