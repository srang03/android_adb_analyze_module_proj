using AndroidAdbAnalyzeModule.Core.Models;
using System.Globalization;

namespace AndroidAdbAnalyzeModule.Preprocessing;

/// <summary>
/// 타임스탬프 정규화기
/// 다양한 포맷의 타임스탬프를 UTC로 변환
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
    /// TimestampNormalizer 생성자
    /// </summary>
    /// <param name="deviceInfo">디바이스 정보</param>
    /// <param name="convertToUtc">UTC 변환 여부</param>
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
    /// 타임스탬프 문자열을 DateTime으로 변환
    /// </summary>
    /// <param name="timestampString">타임스탬프 문자열</param>
    /// <returns>정규화된 DateTime (UTC 또는 디바이스 로컬 시간)</returns>
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
    /// 특정 포맷으로 파싱 시도
    /// </summary>
    private bool TryParseWithFormat(string timestampString, string format, out DateTime result)
    {
        return DateTime.TryParseExact(
            timestampString,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    /// <summary>
    /// DateTime 정규화 (연도 보완, UTC 변환)
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
    /// 연도 정보 보완
    /// MM-dd 포맷의 경우 디바이스 현재 시간 기준으로 연도 추정
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
    /// UTC로 변환
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
    /// ParsedLogEntry의 타임스탬프를 정규화
    /// </summary>
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

