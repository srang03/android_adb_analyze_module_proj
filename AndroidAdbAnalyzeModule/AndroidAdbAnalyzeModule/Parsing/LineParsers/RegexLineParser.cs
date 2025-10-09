using AndroidAdbAnalyzeModule.Configuration.Models;
using AndroidAdbAnalyzeModule.Core.Interfaces;
using AndroidAdbAnalyzeModule.Core.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AndroidAdbAnalyzeModule.Parsing.LineParsers;

/// <summary>
/// Regex 기반 라인 파서
/// </summary>
public sealed class RegexLineParser : ILineParser
{
    private readonly LinePatternConfig _patternConfig;
    private readonly Regex _compiledRegex;

    /// <summary>
    /// RegexLineParser 생성자
    /// </summary>
    /// <param name="patternConfig">라인 패턴 설정</param>
    public RegexLineParser(LinePatternConfig patternConfig)
    {
        _patternConfig = patternConfig ?? throw new ArgumentNullException(nameof(patternConfig));
        
        // Regex 미리 컴파일
        _compiledRegex = new Regex(
            _patternConfig.Regex,
            RegexOptions.Compiled,
            TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// 해당 라인을 파싱할 수 있는지 확인
    /// </summary>
    public bool CanParse(string line, ParsingContext context)
    {
        if (string.IsNullOrEmpty(line))
            return false;

        try
        {
            return _compiledRegex.IsMatch(line);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 라인 파싱
    /// </summary>
    public ParsedLogEntry? Parse(string line, ParsingContext context)
    {
        if (string.IsNullOrEmpty(line))
            return null;

        try
        {
            var match = _compiledRegex.Match(line);
            if (!match.Success)
                return null;

            // 필드 추출 및 타입 변환
            var fields = new Dictionary<string, object>();
            
            foreach (var fieldDef in _patternConfig.Fields)
            {
                var fieldName = fieldDef.Key;
                var definition = fieldDef.Value;

                if (definition.Group >= match.Groups.Count)
                    continue;

                var groupValue = match.Groups[definition.Group].Value;
                if (string.IsNullOrEmpty(groupValue))
                    continue;

                var convertedValue = ConvertValue(groupValue, definition.Type, definition.Format);
                if (convertedValue != null)
                {
                    fields[fieldName] = convertedValue;
                }
            }

            // 타임스탬프 추출 (있는 경우)
            DateTime? timestamp = null;
            if (fields.TryGetValue("timestamp", out var tsValue) && tsValue is DateTime dt)
            {
                timestamp = dt;
            }

            // EventType 동적 치환 지원: {fieldName} 패턴을 필드 값으로 치환
            var eventType = _patternConfig.EventType;
            if (eventType.Contains("{") && eventType.Contains("}"))
            {
                foreach (var field in fields)
                {
                    var placeholder = $"{{{field.Key}}}";
                    if (eventType.Contains(placeholder))
                    {
                        eventType = eventType.Replace(placeholder, field.Value?.ToString() ?? string.Empty);
                    }
                }
            }

            return new ParsedLogEntry
            {
                EventType = eventType,
                Timestamp = timestamp,
                Fields = fields,
                RawLine = line,
                LineNumber = context.LineNumber,
                SectionId = context.SectionId
            };
        }
        catch
        {
            // 파싱 실패 시 null 반환
            return null;
        }
    }

    /// <summary>
    /// 값 타입 변환
    /// </summary>
    private object? ConvertValue(string value, string type, string? format)
    {
        try
        {
            return type.ToLower() switch
            {
                "string" => value,
                "int" => int.Parse(value, CultureInfo.InvariantCulture),
                "long" => long.Parse(value, CultureInfo.InvariantCulture),
                "double" => double.Parse(value, CultureInfo.InvariantCulture),
                "bool" => bool.Parse(value),
                "hex" => ConvertHex(value),
                "datetime" => ParseDateTime(value, format),
                _ => value
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 16진수 문자열을 int로 변환
    /// </summary>
    private int ConvertHex(string hexValue)
    {
        // "0x" 접두사 제거
        if (hexValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hexValue = hexValue.Substring(2);
        }

        return Convert.ToInt32(hexValue, 16);
    }

    /// <summary>
    /// 날짜/시간 파싱
    /// </summary>
    private DateTime? ParseDateTime(string value, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            // 포맷이 없으면 일반 DateTime.Parse 시도
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            return null;
        }

        // 지정된 포맷으로 파싱
        if (DateTime.TryParseExact(
            value,
            format,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result))
        {
            return result;
        }

        return null;
    }
}

