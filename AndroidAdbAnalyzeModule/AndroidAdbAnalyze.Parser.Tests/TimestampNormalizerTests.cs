using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Preprocessing;
using FluentAssertions;
using Xunit.Abstractions;

namespace AndroidAdbAnalyze.Parser.Tests;

/// <summary>
/// TimestampNormalizer 단위 테스트: 타임스탬프 정규화, 연도 추론, UTC 변환 검증
/// </summary>
public class TimestampNormalizerTests
{
    private readonly ITestOutputHelper _output;

    public TimestampNormalizerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region 기본 타임스탬프 파싱 테스트

    [Theory]
    [InlineData("09-04 15:08:25:404", 2025, 9, 4, 6, 8, 25, 404)] // Audio format (15:08 KST -> 06:08 UTC)
    [InlineData("09-04 15:08:25.404", 2025, 9, 4, 6, 8, 25, 404)] // Vibrator format (15:08 KST -> 06:08 UTC)
    [InlineData("2025-09-04 15:08:25.432 +0900", 2025, 9, 4, 6, 8, 25, 432)] // Camera Worker with timezone (UTC result)
    [InlineData("2025-09-06 19:54:46", 2025, 9, 6, 10, 54, 46, 0)] // UsageStats format (19:54 KST -> 10:54 UTC)
    [InlineData("2025-09-04 15:08:25.432", 2025, 9, 4, 6, 8, 25, 432)] // Generic with milliseconds (15:08 KST -> 06:08 UTC)
    [InlineData("09-04 15:08:25", 2025, 9, 4, 6, 8, 25, 0)] // Without milliseconds (15:08 KST -> 06:08 UTC)
    public void Normalize_ValidFormats_ShouldParseCorrectly(
        string timestampString, int expectedYear, int expectedMonth, int expectedDay,
        int expectedHour, int expectedMinute, int expectedSecond, int expectedMillisecond)
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull($"타임스탬프 '{timestampString}' 파싱 실패");
        result!.Value.Year.Should().Be(expectedYear);
        result.Value.Month.Should().Be(expectedMonth);
        result.Value.Day.Should().Be(expectedDay);
        result.Value.Hour.Should().Be(expectedHour);
        result.Value.Minute.Should().Be(expectedMinute);
        result.Value.Second.Should().Be(expectedSecond);
        result.Value.Millisecond.Should().Be(expectedMillisecond);
        result.Value.Kind.Should().Be(DateTimeKind.Utc);

        _output.WriteLine($"✓ Parsed '{timestampString}' to '{result.Value:yyyy-MM-dd HH:mm:ss.fff K}'");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-timestamp")]
    [InlineData("99-99 99:99:99")]
    public void Normalize_InvalidFormats_ShouldReturnNull(string? invalidTimestamp)
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        // Act
        var result = normalizer.Normalize(invalidTimestamp);

        // Assert
        result.Should().BeNull($"잘못된 타임스탬프 '{invalidTimestamp ?? "null"}'는 null을 반환해야 함");
        _output.WriteLine($"✓ Invalid timestamp '{invalidTimestamp ?? "null"}' returned null as expected.");
    }

    #endregion

    #region 연도 추론 테스트

    [Fact]
    public void Normalize_NoYear_FutureMonth_ShouldInferPreviousYear()
    {
        // Arrange: 디바이스 현재 시간이 1월 5일이고, 타임스탬프가 12월이면 작년으로 추정
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 1, 5, 10, 0, 0), // 2025년 1월 5일
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false); // UTC 변환 없이 테스트

        var timestampString = "12-31 23:59:59"; // 12월 31일

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2024, "미래 월(12월)은 작년(2024)으로 추정되어야 함");
        result.Value.Month.Should().Be(12);
        result.Value.Day.Should().Be(31);

        _output.WriteLine(
            $"✓ Year Inference (Future Month): Device at {deviceInfo.CurrentTime:yyyy-MM-dd}, Timestamp '{timestampString}' => Year {result.Value.Year}");
    }

    [Fact]
    public void Normalize_NoYear_PastMonth_ShouldInferCurrentYear()
    {
        // Arrange: 디바이스 현재 시간이 12월이고, 타임스탬프가 11월이면 올해로 추정
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 12, 15, 10, 0, 0), // 2025년 12월 15일
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = "11-01 10:00:00"; // 11월 1일

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2025, "과거 월(11월)은 올해(2025)로 추정되어야 함");
        result.Value.Month.Should().Be(11);
        result.Value.Day.Should().Be(1);

        _output.WriteLine(
            $"✓ Year Inference (Past Month): Device at {deviceInfo.CurrentTime:yyyy-MM-dd}, Timestamp '{timestampString}' => Year {result.Value.Year}");
    }

    [Fact]
    public void Normalize_NoYear_ExactCurrentMonthAndDay_ShouldInferCurrentYear()
    {
        // Arrange: 디바이스 현재 시간과 같은 월/일이면 올해로 추정
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29), // 2025년 9월 7일 18:31:29
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = "09-07 10:00:00"; // 9월 7일 10:00 (현재보다 이전 시각)

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2025, "같은 월/일이고 시각이 이전이면 올해로 추정");
        result.Value.Month.Should().Be(9);
        result.Value.Day.Should().Be(7);

        _output.WriteLine(
            $"✓ Year Inference (Same Day, Past Time): Device at {deviceInfo.CurrentTime:yyyy-MM-dd HH:mm:ss}, Timestamp '{timestampString}' => Year {result.Value.Year}");
    }

    [Fact]
    public void Normalize_NoYear_ExactCurrentMonthAndDayButFutureTime_ShouldInferPreviousYear()
    {
        // Arrange: 디바이스 현재 시간과 같은 월/일이지만 미래 시각이면 작년으로 추정
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 10, 0, 0), // 2025년 9월 7일 10:00:00
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = "09-07 20:00:00"; // 9월 7일 20:00 (현재보다 미래 시각)

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2024, "같은 월/일이지만 미래 시각이면 작년으로 추정");
        result.Value.Month.Should().Be(9);
        result.Value.Day.Should().Be(7);

        _output.WriteLine(
            $"✓ Year Inference (Same Day, Future Time): Device at {deviceInfo.CurrentTime:yyyy-MM-dd HH:mm:ss}, Timestamp '{timestampString}' => Year {result.Value.Year}");
    }

    #endregion

    #region 윤년 테스트

    [Theory]
    [InlineData(2024, 2, 29, true)]  // 2024는 윤년
    [InlineData(2020, 2, 29, true)]  // 2020은 윤년
    [InlineData(2000, 2, 29, true)]  // 2000은 윤년
    [InlineData(1900, 2, 29, false)] // 1900은 평년
    [InlineData(2100, 2, 29, false)] // 2100은 평년
    [InlineData(2023, 2, 29, false)] // 2023은 평년
    public void Normalize_LeapYear_ShouldHandleFebruary29Correctly(int year, int month, int day, bool isLeapYear)
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(year, month, day > 28 && !isLeapYear ? 28 : day, 12, 0, 0),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = $"{year:0000}-{month:00}-{day:00} 10:00:00";

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        if (isLeapYear)
        {
            result.Should().NotBeNull($"{year}년은 윤년이므로 2월 29일이 유효함");
            result!.Value.Year.Should().Be(year);
            result.Value.Month.Should().Be(month);
            result.Value.Day.Should().Be(day);
            _output.WriteLine($"✓ Leap Year Test: Parsed '{timestampString}' successfully for year {year}.");
        }
        else
        {
            result.Should().BeNull($"{year}년은 평년이므로 2월 29일이 무효함");
            _output.WriteLine($"✓ Leap Year Test: Correctly failed to parse '{timestampString}' for non-leap year {year}.");
        }
    }

    [Fact]
    public void Normalize_LeapYear_NoYear_February29_ShouldInferCorrectLeapYear()
    {
        // Arrange: 연도 없이 "02-29" 타임스탬프 파싱
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2024, 3, 1, 12, 0, 0), // 2024년 3월 1일 (2024는 윤년)
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = "02-29 10:00:00"; // 2월 29일

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull("2024년은 윤년이므로 2월 29일이 유효함");
        result!.Value.Year.Should().Be(2024);
        result.Value.Month.Should().Be(2);
        result.Value.Day.Should().Be(29);

        _output.WriteLine(
            $"✓ Leap Year Inference: Device at {deviceInfo.CurrentTime:yyyy-MM-dd}, Timestamp '{timestampString}' => Year {result.Value.Year}");
    }

    #endregion

    #region 타임존 변환 테스트

    [Theory]
    [InlineData("Asia/Seoul", 9)]      // UTC+9
    [InlineData("America/New_York", -4)] // UTC-4 (EDT, 여름)
    [InlineData("America/Los_Angeles", -7)] // UTC-7 (PDT, 여름)
    [InlineData("Europe/London", 1)]    // UTC+1 (BST, 여름)
    [InlineData("UTC", 0)]              // UTC+0
    public void Normalize_DifferentTimeZones_ShouldConvertToUtcCorrectly(string timeZone, int expectedUtcOffset)
    {
        // Arrange
        var localTime = new DateTime(2025, 7, 1, 12, 0, 0); // 여름 시간대 (DST 고려)
        var deviceInfo = new DeviceInfo
        {
            TimeZone = timeZone,
            CurrentTime = localTime,
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        var timestampString = $"{localTime:yyyy-MM-dd HH:mm:ss}";

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull($"타임존 '{timeZone}' 파싱 실패");
        result!.Value.Kind.Should().Be(DateTimeKind.Utc, "ConvertToUtc=true일 때 Kind는 Utc여야 함");

        // UTC 오프셋 검증 (대략적으로, DST 고려)
        var expectedUtcTime = localTime.AddHours(-expectedUtcOffset);
        var timeDifference = Math.Abs((result.Value - expectedUtcTime).TotalMinutes);
        timeDifference.Should().BeLessThan(60, $"타임존 '{timeZone}' UTC 변환 오차가 1시간 미만이어야 함");

        _output.WriteLine(
            $"✓ TimeZone '{timeZone}': Local {localTime:HH:mm} => UTC {result.Value:HH:mm} (Expected Offset: {expectedUtcOffset}h)");
    }

    [Fact]
    public void Normalize_ConvertToUtcFalse_ShouldKeepLocalTime()
    {
        // Arrange
        var localTime = new DateTime(2025, 9, 7, 15, 30, 45);
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = localTime,
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false); // UTC 변환 없음

        var timestampString = $"{localTime:yyyy-MM-dd HH:mm:ss}";

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(localTime.Year);
        result.Value.Month.Should().Be(localTime.Month);
        result.Value.Day.Should().Be(localTime.Day);
        result.Value.Hour.Should().Be(localTime.Hour);
        result.Value.Minute.Should().Be(localTime.Minute);
        result.Value.Second.Should().Be(localTime.Second);

        _output.WriteLine(
            $"✓ ConvertToUtc=false: Input '{timestampString}' correctly kept as local time '{result.Value:yyyy-MM-dd HH:mm:ss}'.");
    }

    [Fact]
    public void Normalize_TimestampWithTimeZoneOffset_ShouldParseCorrectly()
    {
        // Arrange: "+0900" 오프셋이 포함된 타임스탬프
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        var timestampString = "2025-09-04 15:08:25.432 +0900"; // Camera Worker format

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Kind.Should().Be(DateTimeKind.Utc);
        
        // +0900 오프셋이므로 UTC는 9시간 전
        result.Value.Hour.Should().Be(6, "15:08 KST = 06:08 UTC");
        result.Value.Minute.Should().Be(8);
        result.Value.Second.Should().Be(25);
        result.Value.Millisecond.Should().Be(432);

        _output.WriteLine(
            $"✓ Timestamp with explicit offset '{timestampString}' was correctly converted to '{result.Value:yyyy-MM-dd HH:mm:ss.fff K}'.");
    }

    #endregion

    #region DST (Daylight Saving Time) 경계 테스트

    [Fact]
    public void Normalize_DstTransition_SpringForward_ShouldHandleCorrectly()
    {
        // Arrange: 미국 동부 시간대에서 DST 시작 (2025년 3월 9일 2:00 AM -> 3:00 AM)
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "America/New_York",
            CurrentTime = new DateTime(2025, 3, 9, 3, 30, 0), // DST 시작 이후
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        // DST 전환 전 시각 (EST, UTC-5)
        var timestampBefore = "2025-03-09 01:30:00";
        // DST 전환 후 시각 (EDT, UTC-4)
        var timestampAfter = "2025-03-09 03:30:00";

        // Act
        var resultBefore = normalizer.Normalize(timestampBefore);
        var resultAfter = normalizer.Normalize(timestampAfter);

        // Assert
        resultBefore.Should().NotBeNull("DST 전환 전 타임스탬프 파싱 성공");
        resultAfter.Should().NotBeNull("DST 전환 후 타임스탬프 파싱 성공");

        _output.WriteLine($"✓ DST Spring Forward (America/New_York):");
        _output.WriteLine($"  - Before DST (UTC-5): {timestampBefore} => {resultBefore.Value:HH:mm:ss} UTC");
        _output.WriteLine($"  - After DST (UTC-4):  {timestampAfter} => {resultAfter.Value:HH:mm:ss} UTC");
    }

    [Fact]
    public void Normalize_DstTransition_FallBack_ShouldHandleCorrectly()
    {
        // Arrange: 미국 동부 시간대에서 DST 종료 (2025년 11월 2일 2:00 AM -> 1:00 AM)
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "America/New_York",
            CurrentTime = new DateTime(2025, 11, 2, 3, 30, 0), // DST 종료 이후
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        // DST 전환 전 시각 (EDT, UTC-4)
        var timestampBefore = "2025-11-02 01:30:00";
        // DST 전환 후 시각 (EST, UTC-5)
        var timestampAfter = "2025-11-02 03:30:00";

        // Act
        var resultBefore = normalizer.Normalize(timestampBefore);
        var resultAfter = normalizer.Normalize(timestampAfter);

        // Assert
        resultBefore.Should().NotBeNull("DST 전환 전 타임스탬프 파싱 성공");
        resultAfter.Should().NotBeNull("DST 전환 후 타임스탬프 파싱 성공");

        _output.WriteLine($"✓ DST Fall Back (America/New_York):");
        _output.WriteLine($"  - Before DST (UTC-4): {timestampBefore} => {resultBefore.Value:HH:mm:ss} UTC");
        _output.WriteLine($"  - After DST (UTC-5):  {timestampAfter} => {resultAfter.Value:HH:mm:ss} UTC");
    }

    #endregion

    #region 엣지 케이스 테스트

    [Fact]
    public void Normalize_Midnight_ShouldParseCorrectly()
    {
        // Arrange: 자정 시각
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = "2025-09-07 00:00:00";

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Hour.Should().Be(0);
        result.Value.Minute.Should().Be(0);
        result.Value.Second.Should().Be(0);

        _output.WriteLine($"✓ Midnight '{timestampString}' parsed correctly.");
    }

    [Fact]
    public void Normalize_EndOfDay_ShouldParseCorrectly()
    {
        // Arrange: 하루의 끝 시각
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = "2025-09-07 23:59:59.999";

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Hour.Should().Be(23);
        result.Value.Minute.Should().Be(59);
        result.Value.Second.Should().Be(59);
        result.Value.Millisecond.Should().Be(999);

        _output.WriteLine($"✓ End of Day '{timestampString}' parsed correctly.");
    }

    [Fact]
    public void Normalize_VeryFarPast_ShouldParseCorrectly()
    {
        // Arrange: 아주 과거 시각 (2000년)
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = "2000-01-01 00:00:00";

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2000);
        result.Value.Month.Should().Be(1);
        result.Value.Day.Should().Be(1);

        _output.WriteLine($"✓ Far Past date '{timestampString}' parsed correctly.");
    }

    [Fact]
    public void Normalize_VeryFarFuture_ShouldParseCorrectly()
    {
        // Arrange: 아주 미래 시각 (2099년)
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var timestampString = "2099-12-31 23:59:59";

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2099);
        result.Value.Month.Should().Be(12);
        result.Value.Day.Should().Be(31);

        _output.WriteLine($"✓ Far Future date '{timestampString}' parsed correctly.");
    }

    [Theory]
    [InlineData("2025-01-01 10:00:00")] // January 1st
    [InlineData("2025-12-31 23:59:59")] // December 31st
    [InlineData("2025-02-28 12:00:00")] // Non-leap year Feb 28
    [InlineData("2024-02-29 12:00:00")] // Leap year Feb 29
    public void Normalize_YearBoundaryDates_ShouldParseCorrectly(string timestampString)
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 6, 15, 12, 0, 0),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull($"연도 경계 날짜 '{timestampString}' 파싱 성공해야 함");
        _output.WriteLine($"✓ Year Boundary Date '{timestampString}' parsed correctly.");
    }

    [Fact]
    public void Normalize_MillisecondPrecision_ShouldPreservePrecision()
    {
        // Arrange: 밀리초 정밀도 테스트
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: false);

        var testCases = new[]
        {
            ("2025-09-04 15:08:25.001", 1),
            ("2025-09-04 15:08:25.123", 123),
            ("2025-09-04 15:08:25.999", 999),
            ("09-04 15:08:25:000", 0),
            ("09-04 15:08:25.500", 500)
        };

        foreach (var (timestampString, expectedMillisecond) in testCases)
        {
            // Act
            var result = normalizer.Normalize(timestampString);

            // Assert
            result.Should().NotBeNull($"타임스탬프 '{timestampString}' 파싱 실패");
            result!.Value.Millisecond.Should().Be(expectedMillisecond,
                $"밀리초 정밀도가 보존되어야 함 (input: {timestampString})");

            _output.WriteLine($"✓ Millisecond Precision: '{timestampString}' preserved as {result.Value.Millisecond}ms.");
        }
    }

    #endregion

    #region NormalizeLogEntry 테스트

    [Fact]
    public void NormalizeLogEntry_WithValidTimestamp_ShouldNormalize()
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        var entry = new ParsedLogEntry
        {
            Timestamp = new DateTime(2025, 9, 4, 15, 8, 25),
            Fields = new Dictionary<string, object>()
        };

        // Act
        var result = normalizer.NormalizeLogEntry(entry);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Kind.Should().Be(DateTimeKind.Utc);

        _output.WriteLine(
            $"✓ NormalizeLogEntry from Timestamp property: {entry.Timestamp} => {result.Value:yyyy-MM-dd HH:mm:ss K}");
    }

    [Fact]
    public void NormalizeLogEntry_WithTimestampInFields_ShouldNormalize()
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        var entry = new ParsedLogEntry
        {
            Timestamp = null,
            Fields = new Dictionary<string, object>
            {
                { "timestamp", "2025-09-04 15:08:25" }
            }
        };

        // Act
        var result = normalizer.NormalizeLogEntry(entry);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Kind.Should().Be(DateTimeKind.Utc);

        _output.WriteLine(
            $"✓ NormalizeLogEntry from 'timestamp' field: '{entry.Fields["timestamp"]}' => {result.Value:yyyy-MM-dd HH:mm:ss K}");
    }

    [Fact]
    public void NormalizeLogEntry_WithNullEntry_ShouldReturnNull()
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        // Act
        var result = normalizer.NormalizeLogEntry(null!);

        // Assert
        result.Should().BeNull("null entry는 null을 반환해야 함");
        _output.WriteLine($"✓ NormalizeLogEntry with null entry returned null as expected.");
    }

    [Fact]
    public void NormalizeLogEntry_WithoutTimestamp_ShouldReturnNull()
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        var entry = new ParsedLogEntry
        {
            Timestamp = null,
            Fields = new Dictionary<string, object>() // timestamp 필드 없음
        };

        // Act
        var result = normalizer.NormalizeLogEntry(entry);

        // Assert
        result.Should().BeNull("타임스탬프 정보가 없으면 null을 반환해야 함");
        _output.WriteLine($"✓ NormalizeLogEntry without timestamp information returned null as expected.");
    }

    #endregion

    #region ISO 8601 포맷 fallback 테스트

    [Theory]
    [InlineData("2025-09-04T15:08:25Z")]           // ISO 8601 with Z
    [InlineData("2025-09-04T15:08:25+09:00")]      // ISO 8601 with offset
    [InlineData("2025-09-04T15:08:25.432Z")]       // ISO 8601 with milliseconds
    [InlineData("2025-09-04T15:08:25.432+09:00")] // ISO 8601 with milliseconds and offset
    public void Normalize_Iso8601Formats_ShouldParseThroughFallback(string timestampString)
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        // Act
        var result = normalizer.Normalize(timestampString);

        // Assert
        result.Should().NotBeNull($"ISO 8601 포맷 '{timestampString}'는 DateTime.Parse fallback으로 파싱되어야 함");
        result!.Value.Year.Should().Be(2025);
        result.Value.Month.Should().Be(9);
        result.Value.Day.Should().Be(4);

        _output.WriteLine(
            $"✓ ISO 8601 Fallback: '{timestampString}' parsed successfully to '{result.Value:yyyy-MM-dd HH:mm:ss.fff K}'.");
    }

    #endregion

    #region 동시성 및 스레드 안전성 테스트

    [Fact]
    public void Normalize_ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var deviceInfo = new DeviceInfo
        {
            TimeZone = "Asia/Seoul",
            CurrentTime = new DateTime(2025, 9, 7, 18, 31, 29),
            AndroidVersion = "15"
        };
        var normalizer = new TimestampNormalizer(deviceInfo, convertToUtc: true);

        var timestamps = new[]
        {
            "2025-09-04 15:08:25",
            "09-04 15:08:25:404",
            "2025-09-06 19:54:46",
            "09-04 15:08:25.404"
        };

        var results = new System.Collections.Concurrent.ConcurrentBag<DateTime?>();

        // Act: 병렬로 Normalize 호출
        Parallel.ForEach(timestamps, timestamp =>
        {
            for (int i = 0; i < 100; i++)
            {
                var result = normalizer.Normalize(timestamp);
                results.Add(result);
            }
        });

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r => r.Should().NotBeNull("모든 타임스탬프가 정상적으로 파싱되어야 함"));

        _output.WriteLine($"✓ Thread Safety: {results.Count} timestamps parsed concurrently without errors.");
    }

    #endregion
}

