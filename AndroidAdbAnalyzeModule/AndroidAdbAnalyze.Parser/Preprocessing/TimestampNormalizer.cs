using AndroidAdbAnalyze.Parser.Core.Models;
using System.Globalization;

namespace AndroidAdbAnalyze.Parser.Preprocessing;

/// <summary>
/// 다양한 형식의 타임스탬프 문자열을 표준화된 <see cref="DateTime"/> 객체로 변환합니다.
/// 연도 정보가 없는 타임스탬프를 보완하고, 필요 시 UTC로 변환하는 기능을 제공합니다.
/// </summary>
public sealed class TimestampNormalizer
{
    private readonly DeviceInfo _deviceInfo;
    private readonly bool _convertToUtc;
    private readonly TimeZoneInfo _deviceTimeZone;

    // 지원하는 타임스탬프 포맷들
    private static readonly string[] SupportedFormats = new[]
    {
        "MM-dd HH:mm:ss:fff",           // Audio: 09-04 15:08:25:404
        "MM-dd HH:mm:ss.fff",           // Vibrator: 09-04 15:08:25.404
        "yyyy-MM-dd HH:mm:ss.fff zzz",  // Camera Worker: 2025-09-04 15:08:25.432 +0900
        "yyyy-MM-dd HH:mm:ss",          // UsageStats: 2025-09-06 19:54:46
        "yyyy-MM-dd HH:mm:ss.fff",      // Generic with milliseconds
        "MM-dd HH:mm:ss",               // Without milliseconds
    };

    /// <summary>
    /// <see cref="TimestampNormalizer"/>의 새 인스턴스를 초기화합니다.
    /// </summary>
    /// <param name="deviceInfo">연도 추론 및 시간대 변환에 사용될 디바이스 정보입니다.</param>
    /// <param name="convertToUtc">타임스탬프를 UTC로 변환할지 여부입니다. 기본값은 true입니다.</param>
    public TimestampNormalizer(DeviceInfo deviceInfo, bool convertToUtc = true)
    {
        _deviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
        _convertToUtc = convertToUtc;

        // TimeZone 정보 파싱
        try
        {
            _deviceTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_deviceInfo.TimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback: Asia/Seoul
            _deviceTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        }
    }

    /// <summary>
    /// 제공된 타임스탬프 문자열을 정규화된 <see cref="DateTime"/>으로 파싱합니다.
    /// 여러 내장 포맷과 일반적인 날짜/시간 형식을 순서대로 시도합니다.
    /// </summary>
    /// <param name="timestampString">파싱할 타임스탬프 문자열입니다.</param>
    /// <returns>정규화된 <see cref="DateTime"/> 객체 또는 파싱 실패 시 null을 반환합니다.</returns>
    public DateTime? Normalize(string? timestampString)
    {
        if (string.IsNullOrWhiteSpace(timestampString))
            return null;

        // 1. 지원하는 포맷으로 파싱 시도
        foreach (var format in SupportedFormats)
        {
            if (TryParseWithFormat(timestampString, format, out var parsedTime))
            {
                
                return NormalizeDateTime(parsedTime, format);
            }
        }

        // 2. 일반 DateTime.Parse 시도
        if (DateTime.TryParse(timestampString, CultureInfo.InvariantCulture, 
            DateTimeStyles.None, out var genericTime))
        {
            return NormalizeDateTime(genericTime, null);
        }

        return null;
    }

    /// <summary>
    /// 지정된 형식 문자열을 사용하여 타임스탬프를 파싱합니다.
    /// 연도가 없는 포맷의 경우, 2월 29일(윤년) 파싱을 보장하기 위해 임시 윤년(2000년)을 사용합니다.
    /// 이 임시 연도는 상위 NormalizeDateTime 메서드의 AddYearInformation에서 정확한 연도로 교체됩니다.
    /// </summary>
    private bool TryParseWithFormat(string timestampString, string format, out DateTime result)
    {
        // C#의 DateTime.TryParseExact는 연도가 없는 포맷을 파싱할 때 현재 실행 연도를 기본값으로 사용합니다.
        // 현재 연도가 평년(예: 2025년)일 경우 "02-29" 파싱이 실패하는 문제가 발생합니다.
        // 이를 해결하기 위해 연도가 없는 포맷에는 타임스탬프 문자열에 임시 윤년(2000년)을 명시적으로 추가합니다.
        // 2000년은 400으로 나누어떨어지는 특수 윤년으로, "02-29" 파싱을 항상 보장합니다.
        
        bool hasYear = format.Contains("yyyy");
        
        if (!hasYear)
        {
            // 연도가 없는 포맷: 타임스탬프 문자열에 "2000-"을 앞에 붙여 임시 윤년 사용
            // 예: "02-29 10:00:00" -> "2000-02-29 10:00:00"
            string augmentedTimestamp = "2000-" + timestampString;
            string augmentedFormat = "yyyy-" + format;
            
            if (DateTime.TryParseExact(
                augmentedTimestamp,
                augmentedFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
            {
                return true;
            }
            
            result = default;
            return false;
        }
        
        // 연도가 있는 포맷: 기존 로직 유지
        return DateTime.TryParseExact(
            timestampString,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    /// <summary>
    /// 파싱된 <see cref="DateTime"/>에 연도 정보를 추가하고 UTC로 변환하는 정규화 작업을 수행합니다.
    /// </summary>
    private DateTime NormalizeDateTime(DateTime parsedTime, string? format)
    {
        var normalizedTime = parsedTime;

        // 연도 정보가 없는 포맷인 경우 연도 보완
        if (format != null && !format.Contains("yyyy"))
        {
            normalizedTime = AddYearInformation(parsedTime);
        }

        // UTC 변환
        if (_convertToUtc)
        {
            normalizedTime = ConvertToUtc(normalizedTime, format);
        }

        return normalizedTime;
    }

    /// <summary>
    /// 연도 정보가 없는 <see cref="DateTime"/>에 연도를 추가합니다.
    /// 디바이스의 현재 시간을 기준으로, 타임스탬프가 미래 시점이면 작년으로 간주하여 연도를 보정합니다.
    /// </summary>
    private DateTime AddYearInformation(DateTime parsedTime)
    {
        var deviceTime = _deviceInfo.CurrentTime;
        var year = deviceTime.Year;

        // MM-dd가 디바이스 현재 시간보다 미래인 경우 작년으로 추정
        var candidateTime = new DateTime(
            year, 
            parsedTime.Month, 
            parsedTime.Day,
            parsedTime.Hour,
            parsedTime.Minute,
            parsedTime.Second,
            parsedTime.Millisecond);

        if (candidateTime > deviceTime)
        {
            // 미래 시간이면 작년으로 설정
            candidateTime = candidateTime.AddYears(-1);
        }

        return candidateTime;
    }

    /// <summary>
    /// 디바이스의 시간대를 기준으로 현지 시간을 UTC로 변환합니다.
    /// </summary>
    private DateTime ConvertToUtc(DateTime localTime, string? format)
    {
        // 이미 TimeZone 정보가 포함된 포맷인 경우
        if (format != null && format.Contains("zzz"))
        {
            // DateTime.Parse가 이미 TimeZone을 고려했으므로 그대로 UTC로 변환
            if (localTime.Kind == DateTimeKind.Local)
                return localTime.ToUniversalTime();
            if (localTime.Kind == DateTimeKind.Utc)
                return localTime;
        }

        // Unspecified인 경우 디바이스 TimeZone으로 간주
        if (localTime.Kind == DateTimeKind.Unspecified)
        {
            return TimeZoneInfo.ConvertTimeToUtc(localTime, _deviceTimeZone);
        }

        return localTime.ToUniversalTime();
    }

    /// <summary>
    /// <see cref="ParsedLogEntry"/> 객체에서 타임스탬프를 추출하고 정규화합니다.
    /// entry의 `Timestamp` 속성 또는 "timestamp" 필드에서 값을 찾아 처리합니다.
    /// </summary>
    /// <param name="entry">타임스탬프를 추출할 <see cref="ParsedLogEntry"/> 객체입니다.</param>
    /// <returns>정규화된 <see cref="DateTime"/> 객체 또는 타임스탬프를 찾지 못하거나 파싱 실패 시 null을 반환합니다.</returns>
    public DateTime? NormalizeLogEntry(ParsedLogEntry entry)
    {
        if (entry == null)
            return null;

        // 이미 Timestamp가 있으면 정규화
        if (entry.Timestamp.HasValue)
        {
            return NormalizeDateTime(entry.Timestamp.Value, null);
        }

        // Fields에서 timestamp 추출 시도
        if (entry.Fields.TryGetValue("timestamp", out var timestampObj))
        {
            if (timestampObj is DateTime dt)
                return NormalizeDateTime(dt, null);

            if (timestampObj is string str)
                return Normalize(str);
        }

        return null;
    }
}

