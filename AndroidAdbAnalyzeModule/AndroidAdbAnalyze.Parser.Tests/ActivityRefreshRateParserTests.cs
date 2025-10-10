using AndroidAdbAnalyze.Parser.Core.Interfaces;
using AndroidAdbAnalyze.Parser.Parsing.Interfaces;
using AndroidAdbAnalyze.Parser.Parsing.MultilinePatterns;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace AndroidAdbAnalyzeModule.Tests;

/// <summary>
/// ActivityRefreshRateParser 단위 테스트
/// Multiline 패턴 파서의 정확성 및 엣지 케이스 검증
/// </summary>
public class ActivityRefreshRateParserTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger? _logger;
    private readonly IMultilinePatternParser _parser;

    public ActivityRefreshRateParserTests(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Trace);
        });
        
        _logger = loggerFactory.CreateLogger("TestLogger");
        _parser = new ActivityRefreshRateParser(_logger);
    }

    #region 기본 파싱 테스트

    [Fact]
    public void TryParse_ValidMinPattern_ShouldSucceed()
    {
        // Arrange
        var lines = new List<string>
        {
            "#15 << 10-06 22:58:30.717 >>",
            " [Min] Requested ( refreshRate=60.0 w=Window{df80827 u0 com.peace.SilentCamera/com.peace.SilentCamera.CameraActivity})"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var canParse = _parser.CanParse(section, 0);
        var success = _parser.TryParse(section, 0, out var entry, out var linesToSkip);

        // Assert
        canParse.Should().BeTrue("타임스탬프 패턴이 매칭되어야 함");
        success.Should().BeTrue("유효한 2줄 패턴이므로 파싱 성공해야 함");
        entry.Should().NotBeNull();
        entry!.EventType.Should().Be("CAMERA_ACTIVITY_REFRESH");
        entry.Fields["mode"].Should().Be("Min");
        entry.Fields["refreshRate"].Should().Be(60.0);
        entry.Fields["package"].Should().Be("com.peace.SilentCamera");
        entry.Fields["activity"].Should().Be("com.peace.SilentCamera.CameraActivity");
        linesToSkip.Should().Be(1, "다음 1줄을 스킵해야 함");

        _output.WriteLine($"✓ Min 패턴 파싱 성공: {entry.EventType}");
    }

    [Fact]
    public void TryParse_ValidMaxPattern_ShouldSucceed()
    {
        // Arrange
        var lines = new List<string>
        {
            "#2 << 10-06 22:50:55.212 >>",
            " [Max] Requested ( refreshRate=60.0 w=Window{9f97dda u0 com.sec.android.app.camera/com.sec.android.app.camera.Camera})"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var success = _parser.TryParse(section, 0, out var entry, out var linesToSkip);

        // Assert
        success.Should().BeTrue();
        entry.Should().NotBeNull();
        entry!.Fields["mode"].Should().Be("Max");
        entry.Fields["package"].Should().Be("com.sec.android.app.camera");

        _output.WriteLine($"✓ Max 패턴 파싱 성공");
    }

    [Fact]
    public void TryParse_TelegramPattern_ShouldSucceed()
    {
        // Arrange
        var lines = new List<string>
        {
            "#10 << 10-06 22:56:56.859 >>",
            " [Max] Requested ( refreshRate=60.0 w=Window{c09123c u0 org.telegram.messenger/org.telegram.messenger.DefaultIcon})"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var success = _parser.TryParse(section, 0, out var entry, out _);

        // Assert
        success.Should().BeTrue();
        entry.Should().NotBeNull();
        entry!.Fields["package"].Should().Be("org.telegram.messenger");
        entry.Fields["activity"].Should().Be("org.telegram.messenger.DefaultIcon");

        _output.WriteLine($"✓ Telegram 패턴 파싱 성공");
    }

    #endregion

    #region 필터링 테스트 (비카메라 패턴)

    [Fact]
    public void TryParse_ToastPattern_ShouldFail()
    {
        // Arrange - Toast는 package/activity 형식이 아님
        var lines = new List<string>
        {
            "#14 << 10-06 22:58:29.261 >>",
            " [Min] Requested ( refreshRate=60.0 w=Window{5fc9b9e u0 Toast})"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var success = _parser.TryParse(section, 0, out var entry, out _);

        // Assert
        success.Should().BeFalse("Toast는 package/activity 형식이 아니므로 필터링되어야 함");
        entry.Should().BeNull();

        _output.WriteLine($"✓ Toast 패턴 정상 필터링됨");
    }

    [Fact]
    public void TryParse_SplashScreenPattern_ShouldFail()
    {
        // Arrange - Splash Screen도 package/activity 형식이 아님
        var lines = new List<string>
        {
            "#11 << 10-06 22:57:38.114 >>",
            " [Max] Requested ( refreshRate=60.0 w=Window{8ab897b u0 Splash Screen com.peace.SilentCamera})"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var success = _parser.TryParse(section, 0, out var entry, out _);

        // Assert
        success.Should().BeFalse("Splash Screen은 package/activity 형식이 아니므로 필터링되어야 함");
        entry.Should().BeNull();

        _output.WriteLine($"✓ Splash Screen 패턴 정상 필터링됨");
    }

    #endregion

    #region 엣지 케이스 테스트

    [Fact]
    public void CanParse_WithWrongSectionId_ShouldReturnFalse()
    {
        // Arrange
        var lines = new List<string>
        {
            "#15 << 10-06 22:58:30.717 >>",
            " [Min] Requested ( refreshRate=60.0 w=Window{...})"
        };
        
        var section = CreateLogSection("wrong_section", lines);

        // Act
        var canParse = _parser.CanParse(section, 0);

        // Assert
        canParse.Should().BeFalse("섹션 ID가 'activities'가 아니므로 false 반환");

        _output.WriteLine($"✓ 잘못된 섹션 ID 정상 거부");
    }

    [Fact]
    public void TryParse_WithInsufficientLines_ShouldFail()
    {
        // Arrange - 마지막 라인 (2번째 줄 없음)
        var lines = new List<string>
        {
            "#15 << 10-06 22:58:30.717 >>"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var success = _parser.TryParse(section, 0, out var entry, out _);

        // Assert
        success.Should().BeFalse("2줄 패턴인데 1줄만 있으므로 파싱 실패해야 함");
        entry.Should().BeNull();

        _output.WriteLine($"✓ 불충분한 라인 수 정상 처리");
    }

    [Fact]
    public void TryParse_WithInvalidTimestampFormat_ShouldFail()
    {
        // Arrange
        var lines = new List<string>
        {
            "Invalid timestamp line",
            " [Min] Requested ( refreshRate=60.0 w=Window{df80827 u0 com.peace.SilentCamera/com.peace.SilentCamera.CameraActivity})"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var canParse = _parser.CanParse(section, 0);
        var success = _parser.TryParse(section, 0, out var entry, out _);

        // Assert
        canParse.Should().BeFalse("타임스탬프 패턴이 매칭되지 않아야 함");
        success.Should().BeFalse("Line1 타임스탬프 형식 오류로 파싱 실패해야 함");
        entry.Should().BeNull();

        _output.WriteLine($"✓ 잘못된 타임스탬프 형식 정상 거부");
    }

    [Fact]
    public void TryParse_WithInvalidRefreshRateFormat_ShouldFail()
    {
        // Arrange
        var lines = new List<string>
        {
            "#15 << 10-06 22:58:30.717 >>",
            "Invalid refresh rate line"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var success = _parser.TryParse(section, 0, out var entry, out _);

        // Assert
        success.Should().BeFalse("Line2 refreshRate 형식 오류로 파싱 실패해야 함");
        entry.Should().BeNull();

        _output.WriteLine($"✓ 잘못된 refreshRate 형식 정상 거부");
    }

    [Fact]
    public void TryParse_WithMissingSlashInPackage_ShouldFail()
    {
        // Arrange - package/activity 형식이 아닌 경우
        var lines = new List<string>
        {
            "#15 << 10-06 22:58:30.717 >>",
            " [Min] Requested ( refreshRate=60.0 w=Window{df80827 u0 com.example.SingleName})"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act
        var success = _parser.TryParse(section, 0, out var entry, out _);

        // Assert
        success.Should().BeFalse("슬래시(/)가 없으므로 package/activity 형식이 아니라 필터링되어야 함");
        entry.Should().BeNull();

        _output.WriteLine($"✓ 슬래시 없는 패턴 정상 필터링");
    }

    #endregion

    #region 경계 조건 테스트

    [Fact]
    public void TryParse_AtSectionEnd_WithOneLine_ShouldFail()
    {
        // Arrange - 섹션 끝에서 2번째 줄이 없는 경우
        var lines = new List<string>
        {
            "Some previous line",
            "#15 << 10-06 22:58:30.717 >>"
            // 다음 줄 없음 (섹션 끝)
        };
        
        var section = CreateLogSection("activities", lines);

        // Act - 마지막 인덱스에서 파싱 시도
        var success = _parser.TryParse(section, 1, out var entry, out _);

        // Assert
        success.Should().BeFalse("섹션 끝에서 2번째 줄이 없으므로 파싱 실패해야 함");
        entry.Should().BeNull();

        _output.WriteLine($"✓ 섹션 끝 경계 조건 정상 처리");
    }

    [Fact]
    public void TryParse_MultipleConsecutivePatterns_ShouldParseAll()
    {
        // Arrange - 연속된 여러 패턴
        var lines = new List<string>
        {
            "#15 << 10-06 22:58:30.717 >>",
            " [Min] Requested ( refreshRate=60.0 w=Window{df80827 u0 com.peace.SilentCamera/com.peace.SilentCamera.CameraActivity})",
            "#14 << 10-06 22:58:29.261 >>",
            " [Max] Requested ( refreshRate=120.0 w=Window{abc u0 com.example.app/com.example.app.MainActivity})"
        };
        
        var section = CreateLogSection("activities", lines);

        // Act - 첫 번째 패턴 파싱
        var success1 = _parser.TryParse(section, 0, out var entry1, out var skip1);
        
        // Act - 두 번째 패턴 파싱 (skip1 적용)
        var success2 = _parser.TryParse(section, 0 + 1 + skip1, out var entry2, out var skip2);

        // Assert
        success1.Should().BeTrue();
        entry1.Should().NotBeNull();
        entry1!.Fields["package"].Should().Be("com.peace.SilentCamera");
        skip1.Should().Be(1);

        success2.Should().BeTrue();
        entry2.Should().NotBeNull();
        entry2!.Fields["package"].Should().Be("com.example.app");
        skip2.Should().Be(1);

        _output.WriteLine($"✓ 연속된 패턴 모두 파싱 성공");
    }

    #endregion

    #region 파서 속성 테스트

    [Fact]
    public void Parser_ShouldHaveCorrectProperties()
    {
        // Assert
        _parser.ParserId.Should().Be("activity_refresh_rate_multiline");
        _parser.TargetSectionId.Should().Be("activities");
        _parser.Priority.Should().Be(1);

        _output.WriteLine($"✓ 파서 속성 확인 완료");
    }

    #endregion

    #region Helper Methods

    private LogSection CreateLogSection(string sectionId, List<string> lines)
    {
        return new LogSection
        {
            Id = sectionId,
            Name = sectionId,
            StartLine = 1,
            EndLine = 1 + lines.Count,
            Lines = lines
        };
    }

    #endregion
}

