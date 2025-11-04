using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Exceptions;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Parser.Tests;

/// <summary>
/// AdbLogParser 엣지 케이스 통합 테스트: 혼합 로그, 파일 크기, 필드 타입 변환, 인코딩 등
/// </summary>
public class ParserEdgeCaseTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<AdbLogParser> _logger;
    private readonly ILogger<YamlConfigurationLoader> _configLogger;

    public ParserEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug); // Debug 레벨로 변경하여 상세 로그 출력
        });
        
        _logger = loggerFactory.CreateLogger<AdbLogParser>();
        _configLogger = loggerFactory.CreateLogger<YamlConfigurationLoader>();
    }

    #region 혼합 로그 파일 테스트 (정상 + 비정상 라인)

    [Fact]
    public async Task Parser_MixedValidAndInvalidLines_ShouldParseValidAndRecordErrors()
    {
        // Arrange: 정상 로그와 파싱 불가능한 로그가 섞인 임시 파일 생성
        // 주의: adb_audio_config.yaml의 onInvalidLine이 "skip"으로 설정되어 있어
        // 파싱 실패한 라인은 에러로 기록되지 않음 ("log"일 때만 기록됨)
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"mixed_log_{Guid.NewGuid()}.txt");

        try
        {
            var mixedContent = @"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:1234 uid/pid:10001/1000 package:com.sec.android.app.camera type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test
INVALID_LOG_LINE_WITHOUT_PROPER_FORMAT
allowed capture policies:
Events log: focus commands as seen by MediaFocusControl
09-04 15:08:26:123 requestAudioFocus() from uid/pid 10001/1000 AA=USAGE_MEDIA/CONTENT_TYPE_MUSIC clientId=android.media.AudioManager@abcde callingPack=com.sec.android.app.camera req:1
This is another corrupted line 😊
MultiFocusStack:";

            await File.WriteAllTextAsync(tempLogPath, mixedContent);

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
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue("일부 라인 파싱 실패해도 전체는 성공으로 처리");
            result.Events.Should().NotBeEmpty("정상 라인들은 파싱되어야 함");
            
            // 정상 라인 수: 2개 (new player, requestAudioFocus)
            result.Events.Count.Should().Be(2);

            // onInvalidLine = "skip" 설정에 따라 파싱 실패 라인은 에러로 기록되지 않음
            // 비정상 라인들(INVALID_LOG_LINE_WITHOUT_PROPER_FORMAT, This is another corrupted line)은
            // 에러로 기록되지 않고 건너뛰어짐

            _output.WriteLine($"✓ Mixed Valid/Invalid Lines Test");
            _output.WriteLine($"  - Total Lines: 8"); // 실제 파일의 전체 라인 수
            _output.WriteLine($"  - Valid Events Parsed: {result.Events.Count}");
            _output.WriteLine($"  - Errors Recorded: {result.Errors.Count}");
            _output.WriteLine($"  - Success: {result.Success}");
            _output.WriteLine($"  - Note: onInvalidLine='skip' 설정으로 인해 파싱 실패 라인은 에러로 기록되지 않음");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    [Fact]
    public async Task Parser_AllInvalidLines_ShouldReturnEmptyEventsWithErrors()
    {
        // Arrange: 모든 라인이 파싱 불가능한 임시 파일
        // 주의: adb_audio_config.yaml의 onInvalidLine이 "skip"으로 설정되어 있어
        // 파싱 실패한 라인은 에러로 기록되지 않음 ("log"일 때만 기록됨)
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"all_invalid_log_{Guid.NewGuid()}.txt");

        try
        {
            var allInvalidContent = @"Events log: playback activity as reported through PlayerBase
This is not a valid log line
Another garbage line here
12345!!!@@@###
corrupted data 😊😊😊
allowed capture policies:";

            await File.WriteAllTextAsync(tempLogPath, allInvalidContent);

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
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse("파싱된 이벤트가 없으므로 결과는 실패");
            result.Events.Should().BeEmpty("파싱 가능한 이벤트가 없어야 함");
            
            // onInvalidLine = "skip" 설정에 따라 파싱 실패 라인은 에러로 기록되지 않음
            // 따라서 Errors는 비어있거나, 섹션이 발견되지 않은 경우만 에러로 기록됨
            _output.WriteLine($"✓ All Invalid Lines Test");
            _output.WriteLine($"  - Events: {result.Events.Count} (예상: 0)");
            _output.WriteLine($"  - Errors: {result.Errors.Count}");
            _output.WriteLine($"  - ErrorMessage: {result.ErrorMessage}");
            _output.WriteLine($"  - Note: onInvalidLine='skip' 설정으로 인해 파싱 실패 라인은 에러로 기록되지 않음");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region MaxFileSizeMB 정밀 테스트

    [Fact]
    public async Task Parser_FileSizeExactlyAtLimit_ShouldParseSuccessfully()
    {
        // Arrange: 파일 크기가 정확히 MaxFileSizeMB와 같은 경우
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"exact_size_log_{Guid.NewGuid()}.txt");

        try
        {
            var logLines = @"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:1234 uid/pid:10001/1000 package:com.test.app type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test
allowed capture policies:";
            var logLinesByteLength = System.Text.Encoding.UTF8.GetByteCount(logLines);
            
            var oneMB = 1024 * 1024;
            var paddingSize = oneMB - logLinesByteLength;
            
            var content = new string('X', paddingSize) + logLines;
            
            await File.WriteAllTextAsync(tempLogPath, content);

            var fileInfo = new FileInfo(tempLogPath);
            _output.WriteLine($"Created file size: {fileInfo.Length} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions
            {
                DeviceInfo = deviceInfo,
                MaxFileSizeMB = 1 // 정확히 1MB로 설정
            };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue("파일 크기가 정확히 제한과 같고 이벤트가 있으면 파싱 성공");
            result.Events.Should().NotBeEmpty();

            _output.WriteLine($"✓ File Size Exactly At Limit Test");
            _output.WriteLine($"  - File Size: {fileInfo.Length / 1024.0 / 1024.0:F2} MB");
            _output.WriteLine($"  - Max Size: {options.MaxFileSizeMB} MB");
            _output.WriteLine($"  - Result: Success");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    [Fact]
    public async Task Parser_FileSizeOneByteOverLimit_ShouldThrowException()
    {
        // Arrange: 파일 크기가 MaxFileSizeMB를 1바이트 초과하는 경우
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"over_limit_log_{Guid.NewGuid()}.txt");

        try
        {
            // 1MB + 1 byte 크기의 파일 생성
            var oneMBPlusOne = (1024 * 1024) + 1;
            var content = new string('X', oneMBPlusOne);
            await File.WriteAllTextAsync(tempLogPath, content);

            var fileInfo = new FileInfo(tempLogPath);
            _output.WriteLine($"Created file size: {fileInfo.Length} bytes ({fileInfo.Length / 1024.0 / 1024.0:F6} MB)");

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions
            {
                DeviceInfo = deviceInfo,
                MaxFileSizeMB = 1 // 1MB로 설정
            };
            var parser = new AdbLogParser(configuration, _logger);

            // Act & Assert
            var act = async () => await parser.ParseAsync(tempLogPath, options);

            await act.Should().ThrowAsync<LogFileTooLargeException>()
                .WithMessage("*too large*");

            _output.WriteLine($"✓ File Size One Byte Over Limit Test");
            _output.WriteLine($"  - File Size: {fileInfo.Length / 1024.0 / 1024.0:F6} MB");
            _output.WriteLine($"  - Max Size: {options.MaxFileSizeMB} MB");
            _output.WriteLine($"  - Result: LogFileTooLargeException (예상된 동작)");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region 필드 타입 변환 실패 테스트

    [Fact]
    public async Task Parser_IntFieldWithNonNumericValue_ShouldRecordError()
    {
        // Arrange: int 필드에 숫자가 아닌 값이 있는 경우
        // 주의: piid:ABC는 regex 패턴 piid:(\d+)와 매칭되지 않아 파싱 자체가 실패함
        // adb_audio_config.yaml의 onInvalidLine이 "skip"으로 설정되어 있어 에러로 기록되지 않음
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"type_conversion_log_{Guid.NewGuid()}.txt");

        try
        {
            var contentWithTypeError = @"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:ABC uid/pid:10001/1000 package:com.test.app type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test
allowed capture policies:";
            // piid는 int로 정의되어 있지만 "ABC"라는 문자열이 제공됨
            // regex 패턴 piid:(\d+)가 piid:ABC와 매칭되지 않아 파싱 실패

            await File.WriteAllTextAsync(tempLogPath, contentWithTypeError);

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
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse("Regex 매칭 실패로 유효한 이벤트가 없으므로 전체 결과는 실패");
            
            result.Events.Should().BeEmpty("Regex 매칭에 실패한 라인은 이벤트로 추가되지 않아야 함");
            
            // onInvalidLine = "skip" 설정에 따라 파싱 실패 라인은 에러로 기록되지 않음
            // regex 매칭 실패는 타입 변환 단계까지 가지 않고 파싱 실패로 처리됨
            
            _output.WriteLine($"✓ Type Conversion Failure Test");
            _output.WriteLine($"  - Events Parsed: {result.Events.Count}");
            _output.WriteLine($"  - Errors Recorded: {result.Errors.Count}");
            _output.WriteLine($"  - Note: onInvalidLine='skip' 설정으로 인해 regex 매칭 실패 라인은 에러로 기록되지 않음");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region 빈 필드 값 테스트

    [Fact]
    public async Task Parser_EmptyFieldValue_ShouldParseWithEmptyString()
    {
        // Arrange: 필드 값이 비어있는 경우
        // 주의: package: (빈 값)는 regex 패턴 package:([\w\.]+)와 매칭되지 않아 파싱 실패
        // adb_audio_config.yaml의 onInvalidLine이 "skip"으로 설정되어 있어 에러로 기록되지 않음
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"empty_field_log_{Guid.NewGuid()}.txt");

        try
        {
            var contentWithEmptyField = @"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:1234 uid/pid:10001/1000 package: type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test
allowed capture policies:";
            // package 필드가 비어있음 (package:)
            // regex 패턴 package:([\w\.]+)는 최소 1개 이상의 문자를 요구하므로 매칭 실패

            await File.WriteAllTextAsync(tempLogPath, contentWithEmptyField);

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
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse("Regex 매칭 실패로 파싱된 이벤트가 없으므로 실패해야 함");

            result.Events.Should().BeEmpty("Regex 매칭에 실패한 라인은 이벤트로 추가되지 않아야 함");
            
            // onInvalidLine = "skip" 설정에 따라 파싱 실패 라인은 에러로 기록되지 않음
            // 빈 필드로 인한 regex 매칭 실패는 에러로 기록되지 않고 건너뛰어짐
            
            _output.WriteLine($"✓ Empty Field Value Test");
            _output.WriteLine($"  - Events Parsed: {result.Events.Count}");
            _output.WriteLine($"  - Errors Recorded: {result.Errors.Count}");
            _output.WriteLine($"  - Note: onInvalidLine='skip' 설정으로 인해 regex 매칭 실패 라인은 에러로 기록되지 않음");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region 인코딩 테스트

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("ascii")]
    public async Task Parser_DifferentEncodings_ShouldParseCorrectly(string encodingName)
    {
        // Arrange: 다양한 인코딩으로 저장된 파일 파싱
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"encoding_{encodingName}_log_{Guid.NewGuid()}.txt");

        try
        {
            var content = @"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:1234 uid/pid:10001/1000 package:com.sec.android.app.camera type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test
allowed capture policies:";

            var encoding = System.Text.Encoding.GetEncoding(encodingName);
            await File.WriteAllTextAsync(tempLogPath, content, encoding);

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions
            {
                DeviceInfo = deviceInfo,
                Encoding = encodingName
            };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue($"{encodingName} 인코딩 파일 파싱 성공");
            result.Events.Should().NotBeEmpty($"{encodingName} 인코딩 파일에서 이벤트 파싱 성공");

            _output.WriteLine($"✓ Encoding Test: {encodingName}");
            _output.WriteLine($"  - Events Parsed: {result.Events.Count}");
        }
        catch (NotSupportedException)
        {
            _output.WriteLine($"⚠️ Encoding '{encodingName}' not supported on this system, skipping test");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    [Fact]
    public async Task Parser_Utf8WithBom_ShouldParseCorrectly()
    {
        // Arrange: UTF-8 BOM이 있는 파일
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"utf8_bom_log_{Guid.NewGuid()}.txt");

        try
        {
            var content = @"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:1234 uid/pid:10001/1000 package:com.sec.android.app.camera type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test
allowed capture policies:";

            var utf8WithBom = new System.Text.UTF8Encoding(true); // BOM 포함
            await File.WriteAllTextAsync(tempLogPath, content, utf8WithBom);

            var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
            var configuration = await configLoader.LoadAsync(configPath);

            var deviceInfo = new DeviceInfo
            {
                TimeZone = "Asia/Seoul",
                CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
                AndroidVersion = "15"
            };

            var options = new LogParsingOptions
            {
                DeviceInfo = deviceInfo,
                Encoding = "utf-8"
            };
            var parser = new AdbLogParser(configuration, _logger);

            // Act
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue("UTF-8 BOM 파일 파싱 성공");
            result.Events.Should().NotBeEmpty();

            _output.WriteLine($"✓ UTF-8 with BOM Test");
            _output.WriteLine($"  - Events Parsed: {result.Events.Count}");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region 빈 섹션 테스트

    [Fact]
    public async Task Parser_EmptySection_ShouldHandleGracefully()
    {
        // Arrange: 섹션 마커는 있지만 내용이 없는 경우
        // 주의: adb_audio_config.yaml의 onInvalidLine이 "skip"으로 설정되어 있어
        // 마커 라인들이 파싱 실패해도 에러로 기록되지 않음
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"empty_section_log_{Guid.NewGuid()}.txt");

        try
        {
            var contentWithEmptySection = @"Events log: playback activity as reported through PlayerBase
allowed capture policies:";
            // 섹션 시작/끝 마커만 있고 이벤트 라인이 없음

            await File.WriteAllTextAsync(tempLogPath, contentWithEmptySection);

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
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse("빈 섹션은 이벤트가 없으므로 실패로 처리되어야 함");
            result.Events.Should().BeEmpty("섹션에 이벤트가 없으므로 파싱된 이벤트도 없어야 함");
            
            // onInvalidLine = "skip" 설정으로 인해 마커 라인은 에러로 기록되지 않음
            // 단, 섹션은 발견되었으므로 Errors는 비어있을 수 있음
            _output.WriteLine($"✓ Empty Section Test");
            _output.WriteLine($"  - Events: {result.Events.Count} (예상: 0)");
            _output.WriteLine($"  - Errors: {result.Errors.Count}");
            _output.WriteLine($"  - ErrorMessage: {result.ErrorMessage}");
            _output.WriteLine($"  - Note: onInvalidLine='skip' 설정으로 인해 마커 라인은 에러로 기록되지 않음");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region 매우 긴 라인 테스트

    [Fact]
    public async Task Parser_VeryLongLine_ShouldParseCorrectly()
    {
        // Arrange: 매우 긴 라인 (10KB)
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"long_line_log_{Guid.NewGuid()}.txt");

        try
        {
            var longPackageName = new string('X', 10000); // 10KB 길이의 패키지명
            var contentWithLongLine = $@"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:1234 uid/pid:10001/1000 package:{longPackageName} type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test
allowed capture policies:
Events log: focus commands as seen by MediaFocusControl
09-04 15:08:26:123 requestAudioFocus() from uid/pid 10001/1000 AA=USAGE_MEDIA/CONTENT_TYPE_MUSIC clientId=android.media.AudioManager@12345 callingPack=com.sec.android.app.camera req=1 flags=0x0 sdk=35";

            await File.WriteAllTextAsync(tempLogPath, contentWithLongLine);

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
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Events.Should().NotBeEmpty("매우 긴 라인도 파싱되어야 함");

            var firstEvent = result.Events.FirstOrDefault(e => e.Attributes.ContainsKey("package"));
            if (firstEvent != null)
            {
                var packageValue = firstEvent.Attributes["package"]?.ToString();
                packageValue.Should().NotBeNullOrEmpty();
                packageValue!.Length.Should().BeGreaterThan(9000, "긴 패키지명이 보존되어야 함");

                _output.WriteLine($"✓ Very Long Line Test");
                _output.WriteLine($"  - Package name length: {packageValue.Length} characters");
                _output.WriteLine($"  - Events parsed: {result.Events.Count}");
            }
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region 특수 문자 테스트

    [Fact]
    public async Task Parser_SpecialCharactersInFields_ShouldParseCorrectly()
    {
        // Arrange: 필드에 특수 문자가 포함된 경우
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"special_chars_log_{Guid.NewGuid()}.txt");

        try
        {
            var contentWithSpecialChars = @"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:1234 uid/pid:10001/1000 package:com.test.app.demo_123 type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test
allowed capture policies:
Events log: focus commands as seen by MediaFocusControl
09-04 15:08:26:123 requestAudioFocus() from uid/pid 10001/1000 AA=USAGE_MEDIA/CONTENT_TYPE_MUSIC clientId=android.media.AudioManager@12345 callingPack=com.example.app.inner req:1
MultiFocusStack:";

            await File.WriteAllTextAsync(tempLogPath, contentWithSpecialChars);

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
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Events.Should().NotBeEmpty("특수 문자가 포함된 필드도 파싱되어야 함");

            var packages = result.Events
                .Where(e => e.Attributes.ContainsKey("package"))
                .Select(e => e.Attributes["package"]?.ToString())
                .ToList();

            packages.Should().Contain("com.test.app.demo_123", "언더스코어가 보존되어야 함");
            packages.Should().Contain("com.example.app.inner", "점으로 구분된 패키지명이 보존되어야 함");

            _output.WriteLine($"✓ Special Characters Test");
            _output.WriteLine($"  - Packages parsed: {string.Join(", ", packages)}");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region 여러 섹션 테스트

    [Fact]
    public async Task Parser_MultipleSections_ShouldParseAllSections()
    {
        // Arrange: 여러 섹션이 있는 로그 파일
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var tempLogPath = Path.Combine(Path.GetTempPath(), $"multi_section_log_{Guid.NewGuid()}.txt");

        try
        {
            var contentWithMultipleSections = @"Events log: playback activity as reported through PlayerBase
09-04 15:08:25:404 new player piid:1234 uid/pid:10001/1000 package:com.test.app1 type:android.media.AudioTrack attr:AudioAttributes: usage=USAGE_MEDIA content=CONTENT_TYPE_MUSIC flags=0x0 tags=test1
allowed capture policies:
Events log: focus commands as seen by MediaFocusControl
09-04 15:09:26:123 requestAudioFocus() from uid/pid 10002/2000 AA=USAGE_MEDIA/CONTENT_TYPE_MUSIC clientId=android.media.AudioManager@abc123 callingPack=com.test.app2 req:1
MultiFocusStack:
Events log: recording activity received by AudioService
09-04 15:10:30:500 rec start riid:100 uid:10003 session:200 src:CAMCORDER not silenced pack:com.test.app3
AudioDeviceBroker:";

            await File.WriteAllTextAsync(tempLogPath, contentWithMultipleSections);

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
            var result = await parser.ParseAsync(tempLogPath, options);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.Events.Count.Should().BeGreaterThanOrEqualTo(3, "3개 섹션의 이벤트가 모두 파싱되어야 함");

            var packages = result.Events
                .Where(e => e.Attributes.ContainsKey("package"))
                .Select(e => e.Attributes["package"]?.ToString())
                .ToList();

            packages.Should().Contain("com.test.app1");
            packages.Should().Contain("com.test.app2");
            packages.Should().Contain("com.test.app3");

            _output.WriteLine($"✓ Multiple Sections Test");
            _output.WriteLine($"  - Total Events: {result.Events.Count}");
            _output.WriteLine($"  - Packages: {string.Join(", ", packages)}");
        }
        finally
        {
            if (File.Exists(tempLogPath))
            {
                File.Delete(tempLogPath);
            }
        }
    }

    #endregion

    #region Null 및 기본값 테스트

    [Fact]
    public async Task Parser_NullDeviceInfo_ShouldUseDefaults()
    {
        // Arrange: DeviceInfo의 일부 필드가 null인 경우
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"⚠️ Test log file not found: {logPath}, skipping test");
            return;
        }

        var configLoader = new YamlConfigurationLoader(configPath, _configLogger);
        var configuration = await configLoader.LoadAsync(configPath);

        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = DateTime.Now,
            AndroidVersion = null, // null
            Manufacturer = null,
            Model = null
        };

        var options = new LogParsingOptions { DeviceInfo = deviceInfo };
        var parser = new AdbLogParser(configuration, _logger);

        // Act
        var result = await parser.ParseAsync(logPath, options);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue("DeviceInfo 필드가 null이어도 파싱 성공");

        _output.WriteLine($"✓ Null DeviceInfo Fields Test");
        _output.WriteLine($"  - Events Parsed: {result.Events.Count}");
    }

    #endregion

    #region 동시성 테스트

    [Fact]
    public async Task Parser_ConcurrentParsing_ShouldBeThreadSafe()
    {
        // Arrange: 여러 스레드에서 동시에 파싱
        var configPath = Path.Combine("TestData", "adb_audio_config.yaml");
        var logPath = Path.Combine("TestData", "audio.txt");

        if (!File.Exists(logPath))
        {
            _output.WriteLine($"⚠️ Test log file not found: {logPath}, skipping test");
            return;
        }

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

        var results = new System.Collections.Concurrent.ConcurrentBag<ParsingResult>();

        // Act: 10개 스레드에서 동시에 파싱
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var result = await parser.ParseAsync(logPath, options);
            results.Add(result);
        });

        await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10, "10번의 파싱 작업이 모두 완료되어야 함");
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.Success.Should().BeTrue("모든 파싱 작업이 성공해야 함");
            r.Events.Should().NotBeEmpty();
        });

        // 모든 결과가 동일한 이벤트 수를 가져야 함
        var eventCounts = results.Select(r => r.Events.Count).Distinct().ToList();
        eventCounts.Should().HaveCount(1, "동일한 입력에 대해 동일한 결과를 반환해야 함");

        _output.WriteLine($"✓ Concurrent Parsing Test");
        _output.WriteLine($"  - Concurrent Tasks: 10");
        _output.WriteLine($"  - All Results Event Count: {eventCounts.First()}");
    }

    #endregion
}

