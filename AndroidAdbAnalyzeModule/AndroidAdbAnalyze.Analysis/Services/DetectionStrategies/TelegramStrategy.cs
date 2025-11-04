using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Configuration;
using AndroidAdbAnalyze.Parser.Core.Constants;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;

/// <summary>
/// Telegram 전용 촬영 탐지 전략
/// </summary>
/// <remarks>
/// Telegram은 다음과 같은 특징이 있습니다:
/// 
/// 1. DATABASE 로그 없음 (기본 MediaStore 사용 안 함)
/// 2. PLAYER_EVENT 없음 (무음 촬영 또는 자체 셔터 음)
/// 3. VIBRATION_EVENT (usage: TOUCH) 발생 ✅ 핵심 아티팩트
/// 
/// 따라서:
/// - VIBRATION_EVENT (usage: TOUCH)를 핵심 아티팩트로 사용
/// - BaseStrategy의 DATABASE/PLAYER_EVENT 의존성 제거
/// 
/// 적용 패키지:
/// - org.telegram.messenger
/// - org.telegram.messenger.web (Telegram X)
/// </remarks>
public sealed class TelegramStrategy : BaseCaptureDetectionStrategy
{
    // ⚠️ static readonly → 인스턴스 필드로 변경
    // 기존 값들은 ConfigurationProvider.GetDefault()에서 제공됩니다.
    
    private readonly HashSet<string> _conditionalKeyArtifactTypes;
    private readonly HashSet<string> _supportingArtifactTypes;
    
    // VIBRATION_EVENT 검증용 문자열 상수 (Telegram 고유값, Configuration 불필요)
    private const string VIBRATION_USAGE_TOUCH = "TOUCH";

    /// <summary>
    /// 기본 생성자 (Backward Compatibility 보장)
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="confidenceCalculator">신뢰도 계산기</param>
    /// <remarks>
    /// 기존 테스트 코드 호환성을 위해 유지됩니다.
    /// 내부적으로 ConfigurationProvider.GetDefault()를 사용하여 기본값을 제공합니다.
    /// </remarks>
    public TelegramStrategy(
        ILogger<TelegramStrategy> logger,
        IConfidenceCalculator confidenceCalculator)
        : this(logger, confidenceCalculator, ConfigurationProvider.GetDefault())
    {
    }

    /// <summary>
    /// Configuration 주입 생성자
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="confidenceCalculator">신뢰도 계산기</param>
    /// <param name="config">아티팩트 탐지 설정</param>
    /// <remarks>
    /// DI 컨테이너에서 Configuration을 주입받아 동적으로 아티팩트 분류를 설정합니다.
    /// YAML 파일 기반 설정 변경이 가능합니다.
    /// </remarks>
    public TelegramStrategy(
        ILogger<TelegramStrategy> logger,
        IConfidenceCalculator confidenceCalculator,
        ArtifactDetectionConfig config)
        : base(logger, confidenceCalculator)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        var strategyConfig = config.Strategies.TryGetValue("telegram", out var cfg)
            ? cfg
            : throw new InvalidOperationException("Configuration에 'telegram' 전략이 정의되지 않았습니다.");
        
        _conditionalKeyArtifactTypes = new HashSet<string>(strategyConfig.ConditionalKeyArtifacts);
        _supportingArtifactTypes = new HashSet<string>(strategyConfig.SupportingArtifacts);
        
        _logger.LogInformation(
            "[TelegramStrategy] 초기화 완료: 조건부 {Conditional}개, 보조 {Supporting}개",
            _conditionalKeyArtifactTypes.Count,
            _supportingArtifactTypes.Count);
    }

    /// <inheritdoc/>
    public override string? PackageNamePattern => "org.telegram.messenger";

    /// <summary>
    /// 핵심 아티팩트 검색 (Template Method 구현)
    /// </summary>
    /// <remarks>
    /// Telegram은 VIBRATION_EVENT (usage: TOUCH)를 핵심 아티팩트로 사용
    /// </remarks>
    protected override List<NormalizedLogEvent> GetKeyArtifacts(
        SessionContext context,
        AnalysisOptions options)
    {
        var keyArtifacts = context.AllEvents
            .Where(e => _conditionalKeyArtifactTypes.Contains(e.EventType))
            .Where(e => ValidateVibrationEvent(e, context.Session.PackageName))
            .ToList();

        _logger.LogDebug(
            "[TelegramStrategy] Session {SessionId} ({Package}): VIBRATION_EVENT {Count}개",
            context.Session.SessionId, context.Session.PackageName, keyArtifacts.Count);

        return keyArtifacts;
    }

    /// <summary>
    /// 보조 아티팩트 타입 정의 (Template Method 구현)
    /// </summary>
    protected override HashSet<string> GetSupportingArtifactTypes()
    {
        return _supportingArtifactTypes;
    }

    /// <summary>
    /// 보조 아티팩트 수집 오버라이드 (PLAYER_EVENT 제외)
    /// </summary>
    protected override List<NormalizedLogEvent> CollectSupportingArtifacts(
        NormalizedLogEvent keyArtifact,
        SessionContext context,
        TimeSpan correlationWindow)
    {
        var supportingArtifacts = base.CollectSupportingArtifacts(keyArtifact, context, correlationWindow);

        // PLAYER_EVENT 제외 (전송 시 발생, 촬영과 무관)
        var filteredArtifacts = supportingArtifacts
            .Where(e => e.EventType != LogEventTypes.PLAYER_EVENT)
            .ToList();

        _logger.LogTrace(
            "[TelegramStrategy] 보조 아티팩트: 전체 {Total}개 → PLAYER_EVENT 제외 후 {Filtered}개",
            supportingArtifacts.Count, filteredArtifacts.Count);

        return filteredArtifacts;
    }

    /// <summary>
    /// VIBRATION_EVENT 검증 (usage: TOUCH + 패키지 확인)
    /// </summary>
    private bool ValidateVibrationEvent(NormalizedLogEvent artifact, string sessionPackageName)
    {
        // 1. usage 확인
        if (!artifact.Attributes.TryGetValue("usage", out var usageObj))
        {
            return false;
        }

        var usage = usageObj?.ToString() ?? string.Empty;
        bool isTouch = usage.Equals(VIBRATION_USAGE_TOUCH, StringComparison.OrdinalIgnoreCase);

        if (!isTouch)
        {
            _logger.LogTrace(
                "[TelegramStrategy] VIBRATION_EVENT 제외: usage={Usage} (TOUCH 아님)",
                usage);
            return false;
        }

        // 2. 패키지 확인 (세션 패키지만 허용)
        if (!artifact.Attributes.TryGetValue("package", out var pkgObj))
        {
            _logger.LogTrace(
                "[TelegramStrategy] VIBRATION_EVENT 제외: 패키지 정보 없음");
            return false;
        }

        var eventPackage = pkgObj?.ToString() ?? string.Empty;
        if (!eventPackage.Equals(sessionPackageName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogTrace(
                "[TelegramStrategy] VIBRATION_EVENT 제외: 패키지 불일치 (세션={Session}, 이벤트={Event})",
                sessionPackageName, eventPackage);
            return false;
        }

        _logger.LogTrace(
            "[TelegramStrategy] VIBRATION_EVENT 승인: usage=TOUCH, package={Package}, Timestamp={Timestamp:HH:mm:ss.fff}",
            eventPackage,             artifact.Timestamp);
        return true;
    }
}

