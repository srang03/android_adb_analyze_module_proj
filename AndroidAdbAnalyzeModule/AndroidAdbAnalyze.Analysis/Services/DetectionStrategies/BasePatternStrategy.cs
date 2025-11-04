using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Configuration;
using AndroidAdbAnalyze.Analysis.Configuration;
using Microsoft.Extensions.Logging;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Core.Constants;

namespace AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;

/// <summary>
/// 기본 패턴 기반 촬영 탐지 전략
/// </summary>
/// <remarks>
/// 대부분의 앱에 적용 가능한 범용 전략입니다:
/// 
/// 핵심 아티팩트 (Key Artifact):
/// - DATABASE_INSERT, DATABASE_EVENT (미디어 저장 확정)
/// 
/// 조건부 핵심 아티팩트 (Conditional Key Artifact):
/// - VIBRATION_EVENT (hapticType=50061)
/// - PLAYER_EVENT (tags=CAMERA + PostProcessService)
/// - URI_PERMISSION_GRANT (임시 파일 경로만)
/// - SILENT_CAMERA_CAPTURE
/// 
/// 보조 아티팩트 (Supporting Artifact):
/// - PLAYER_CREATED, PLAYER_RELEASED
/// - MEDIA_EXTRACTOR
/// - SHUTTER_SOUND
/// - CAMERA_ACTIVITY_REFRESH
/// 
/// 적용 대상:
/// - 기본 카메라 (com.sec.android.app.camera)
/// - 무음 카메라 (com.peace.SilentCamera)
/// - 기타 표준 카메라 API 사용 앱
/// </remarks>
public sealed class BasePatternStrategy : BaseCaptureDetectionStrategy
{
    // ⚠️ static readonly → 인스턴스 필드로 변경
    // 기존 값들은 ConfigurationProvider.GetDefault()에서 제공됩니다.
    
    private readonly HashSet<string> _keyArtifactTypes;
    private readonly HashSet<string> _conditionalKeyArtifactTypes;
    private readonly HashSet<string> _supportingArtifactTypes;
    private readonly ValidationConstantsConfig _validation;

    /// <summary>
    /// 기본 생성자 (Backward Compatibility 보장)
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="confidenceCalculator">신뢰도 계산기</param>
    /// <remarks>
    /// 기존 테스트 코드 호환성을 위해 유지됩니다.
    /// 내부적으로 ConfigurationProvider.GetDefault()를 사용하여 기본값을 제공합니다.
    /// </remarks>
    public BasePatternStrategy(
        ILogger<BasePatternStrategy> logger,
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
    public BasePatternStrategy(
        ILogger<BasePatternStrategy> logger,
        IConfidenceCalculator confidenceCalculator,
        ArtifactDetectionConfig config)
        : base(logger, confidenceCalculator)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        var strategyConfig = config.Strategies.TryGetValue("base_pattern", out var cfg)
            ? cfg
            : throw new InvalidOperationException("Configuration에 'base_pattern' 전략이 정의되지 않았습니다.");
        
        _keyArtifactTypes = new HashSet<string>(strategyConfig.KeyArtifacts);
        _conditionalKeyArtifactTypes = new HashSet<string>(strategyConfig.ConditionalKeyArtifacts);
        _supportingArtifactTypes = new HashSet<string>(strategyConfig.SupportingArtifacts);
        _validation = config.Validation;
        
        _logger.LogInformation(
            "[BaseStrategy] 초기화 완료: 핵심 {Key}개, 조건부 {Conditional}개, 보조 {Supporting}개",
            _keyArtifactTypes.Count,
            _conditionalKeyArtifactTypes.Count,
            _supportingArtifactTypes.Count);
    }

    /// <inheritdoc/>
    public override string? PackageNamePattern => null; // 모든 앱에 기본 적용 (fallback)

    /// <summary>
    /// 핵심 아티팩트 검색 (Template Method 구현)
    /// </summary>
    /// <remarks>
    /// 1. 확정 핵심 아티팩트 검색 (DATABASE_INSERT, DATABASE_EVENT)
    /// 2. 확정이 없을 경우 조건부 핵심 아티팩트 검색 (VIBRATION_EVENT, PLAYER_EVENT, URI_PERMISSION_GRANT, SILENT_CAMERA_CAPTURE)
    /// </remarks>
    protected override List<NormalizedLogEvent> GetKeyArtifacts(
        SessionContext context,
        AnalysisOptions options)
    {
        // 1단계: 확정 핵심 아티팩트 검색
        var keyArtifacts = context.AllEvents
            .Where(e => _keyArtifactTypes.Contains(e.EventType))
            .ToList();

        _logger.LogDebug(
            "[BaseStrategy] Session {SessionId} ({Package}): 확정 핵심 아티팩트 {Count}개",
            context.Session.SessionId, context.Session.PackageName, keyArtifacts.Count);

        // 2단계: 조건부 핵심 아티팩트 검색 (항상 수행)
        // - 확정이 없으면: 조건부를 "핵심"으로 승격
        // - 확정이 있으면: 조건부를 "보조"로 강등 (supportingArtifacts에서 자동 수집)
        var conditionalKeyArtifacts = context.AllEvents
            .Where(e => _conditionalKeyArtifactTypes.Contains(e.EventType))
            .Where(e => ValidateConditionalKeyArtifact(e, context))
            .ToList();
        
        _logger.LogDebug(
            "[BaseStrategy] Session {SessionId}: 조건부 아티팩트 검증 완료 {Count}개",
            context.Session.SessionId, conditionalKeyArtifacts.Count);

        // 3단계: 반환 로직 (배타적 우선순위 유지)
        if (keyArtifacts.Count == 0)
        {
            // 확정 없음 → 조건부를 "핵심"으로 승격
            _logger.LogInformation(
                "[BaseStrategy] Session {SessionId}: 확정 핵심 없음 → 조건부 {Count}개를 핵심으로 승격",
                context.Session.SessionId, conditionalKeyArtifacts.Count);
            return conditionalKeyArtifacts;
        }
        else
        {
            // 확정 있음 → 확정만 반환 (조건부는 supportingArtifacts로 자동 수집됨)
            _logger.LogDebug(
                "[BaseStrategy] Session {SessionId}: 확정 핵심 있음 → 조건부 {Count}개는 보조로 처리 (supportingArtifacts에서 수집)",
                context.Session.SessionId, conditionalKeyArtifacts.Count);
            
            _logger.LogInformation(
                "[BaseStrategy] Session {SessionId} ({Package}): 핵심 아티팩트 {Count}개 반환 (확정 우선)",
                context.Session.SessionId, context.Session.PackageName, keyArtifacts.Count);
            return keyArtifacts;
        }
    }

    /// <summary>
    /// 보조 아티팩트 타입 정의 (Template Method 구현)
    /// </summary>
    protected override HashSet<string> GetSupportingArtifactTypes()
    {
        return _supportingArtifactTypes;
    }

    /// <summary>
    /// 경로 패턴 검증 (Template Method 구현)
    /// </summary>
    protected override bool ShouldExcludeByPathPattern(
        NormalizedLogEvent artifact,
        AnalysisOptions options)
    {
        return IsExcludedByPathPattern(artifact, options);
    }

    /// <summary>
    /// 중복 제거 (Template Method 구현)
    /// </summary>
    protected override IReadOnlyList<CameraCaptureEvent> DeduplicateCaptures(
        List<CameraCaptureEvent> captures,
        AnalysisOptions options)
    {
        return DeduplicateCapturesByTimeWindow(captures, options.CaptureDeduplicationWindow);
    }

    /// <summary>
    /// 보조 아티팩트 검증
    /// </summary>
    private bool ValidateConditionalKeyArtifact(
        NormalizedLogEvent Artifact,
        SessionContext context)
    {
        switch (Artifact.EventType)
        {
            case var type when type == LogEventTypes.VIBRATION_EVENT:
                return ValidateVibrationEventAsShutter(Artifact, context);
                
            case var type when type == LogEventTypes.PLAYER_EVENT:
                return ValidatePlayerEvent(Artifact, context);
                
            case var type when type == LogEventTypes.URI_PERMISSION_GRANT:
                return ValidateUriPermission(Artifact);
                
            case var type when type == LogEventTypes.SILENT_CAMERA_CAPTURE:
                return true; // SilentCameraCaptureParser에서 이미 검증됨
                
            case var type when type == LogEventTypes.FOREGROUND_SERVICE:
                return ValidateForegroundServiceAsCapture(Artifact, context);
                
            default:
                return false;
        }
    }
    
    /// <summary>
    /// VIBRATION_EVENT를 촬영 버튼 진동으로 검증 (hapticType=50061)
    /// </summary>
    /// <remarks>
    /// 기본 카메라의 촬영 버튼을 누를 때 발생하는 진동 이벤트를 감지합니다.
    /// 
    /// Pattern 1 (기본):
    ///   hapticType=50061, status=finished
    /// 
    /// Pattern 2 (보완):
    ///   hapticType=50061, status=cancelled_superseded (0.2초 이내)
    ///   → 바로 이어서 hapticType=50072, status=finished
    ///   
    /// Pattern 2는 Android 시스템이 촬영 버튼 진동을 취소하고
    /// 즉시 일반 UI 진동으로 교체하는 경우를 처리합니다.
    /// </remarks>
    private bool ValidateVibrationEventAsShutter(NormalizedLogEvent Artifact, SessionContext context)
    {
        if (!Artifact.Attributes.TryGetValue("hapticType", out var hapticTypeObj))
        {
            _logger.LogTrace(
                "[BaseStrategy] VIBRATION_EVENT 제외: hapticType 정보 없음");
            return false;
        }

        // hapticType 값 추출 (int 또는 string일 수 있음)
        int hapticType;
        if (hapticTypeObj is int hapticTypeInt)
        {
            hapticType = hapticTypeInt;
        }
        else if (int.TryParse(hapticTypeObj?.ToString(), out var parsed))
        {
            hapticType = parsed;
        }
        else
        {
            _logger.LogTrace(
                "[BaseStrategy] VIBRATION_EVENT 제외: hapticType 파싱 실패 (value={Value})",
                hapticTypeObj);
            return false;
        }

        if (hapticType != _validation.HapticTypeCameraShutter)
        {
            _logger.LogTrace(
                "[BaseStrategy] VIBRATION_EVENT 제외: hapticType={HapticType} (설정값 아님)",
                hapticType);
            return false;
        }

        // Pattern 1: status=finished (기존 로직)
        if (Artifact.Attributes.TryGetValue("status", out var statusObj))
        {
            var status = statusObj?.ToString() ?? string.Empty;
            if (status.Equals("finished", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "[BaseStrategy] ✅ VIBRATION_EVENT 승인 (Pattern 1): hapticType=50061, status=finished, Time={Time:HH:mm:ss.fff}",
                    Artifact.Timestamp);
                return true;
            }

            // Pattern 2: status=cancelled_superseded + 바로 뒤 50072 finished
            if (status.Equals("cancelled_superseded", StringComparison.OrdinalIgnoreCase))
            {
                // 0.2초 이내에 hapticType=50072, status=finished가 있는지 확인
                var followUpVibration = context.AllEvents
                    .Where(e => e.EventType == LogEventTypes.VIBRATION_EVENT)
                    .Where(e => e.Timestamp > Artifact.Timestamp)
                    .Where(e => (e.Timestamp - Artifact.Timestamp).TotalSeconds <= 0.2)
                    .Where(e => e.Attributes.TryGetValue("hapticType", out var ht) && 
                               (ht is int htInt && htInt == 50072 || 
                                int.TryParse(ht?.ToString(), out var htParsed) && htParsed == 50072))
                    .Where(e => e.Attributes.TryGetValue("status", out var s) && 
                               s?.ToString()?.Equals("finished", StringComparison.OrdinalIgnoreCase) == true)
                    .FirstOrDefault();

                if (followUpVibration != null)
                {
                    _logger.LogDebug(
                        "[BaseStrategy] ✅ VIBRATION_EVENT 승인 (Pattern 2): hapticType=50061 cancelled → 50072 finished, Time={Time:HH:mm:ss.fff}",
                        Artifact.Timestamp);
                    return true;
                }

                _logger.LogTrace(
                    "[BaseStrategy] VIBRATION_EVENT 제외: status=cancelled_superseded이지만 후속 50072 없음, Time={Time:HH:mm:ss.fff}",
                    Artifact.Timestamp);
                return false;
            }

            // 기타 status는 제외
            _logger.LogTrace(
                "[BaseStrategy] VIBRATION_EVENT 제외: status={Status} (미지원), Time={Time:HH:mm:ss.fff}",
                status, Artifact.Timestamp);
            return false;
        }

        // status 정보 없음 (기본적으로 제외)
        _logger.LogTrace(
            "[BaseStrategy] VIBRATION_EVENT 제외: status 정보 없음, Time={Time:HH:mm:ss.fff}",
            Artifact.Timestamp);
        return false;
    }

    /// <summary>
    /// PLAYER_EVENT 검증 (셔터 음 + PostProcessService)
    /// </summary>
    /// <remarks>
    /// 기본 카메라 PLAYER_EVENT는 PostProcessService와 함께 사용되어야 함
    /// </remarks>
    private bool ValidatePlayerEvent(
        NormalizedLogEvent playerEvent,
        SessionContext context)
    {
            // 1. event: started 확인
            if (!playerEvent.Attributes.TryGetValue("event", out var eventObj) ||
                eventObj?.ToString() != _validation.PlayerEventStateStarted)
            {
                return false;
            }

        // 2. piid 추출
        if (!playerEvent.Attributes.TryGetValue("piid", out var piidObj))
        {
            return false;
        }

        // 3. PLAYER_CREATED에서 tags: CAMERA 확인
        var relatedPlayerCreated = context.AllEvents
            .Where(e => e.EventType == LogEventTypes.PLAYER_CREATED)
            .Where(e => e.Attributes.TryGetValue("piid", out var otherPiid) && 
                       otherPiid?.ToString() == piidObj.ToString())
            .Where(e => e.Timestamp <= playerEvent.Timestamp)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();

        if (relatedPlayerCreated == null)
        {
            return false;
        }

        if (!relatedPlayerCreated.Attributes.TryGetValue("tags", out var tagsObj))
        {
            return false;
        }

            var tags = tagsObj?.ToString() ?? string.Empty;
            if (!tags.Contains(_validation.PlayerTagCamera, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

        // 4. PostProcessService 확인 (기본 카메라만)
        bool hasPostProcessService = context.ForegroundServices.Any(fs =>
            fs.ServiceClass.Contains(_validation.ServiceClassPostProcess, StringComparison.OrdinalIgnoreCase) &&
            playerEvent.Timestamp >= fs.StartTime &&
            playerEvent.Timestamp <= (fs.StopTime ?? DateTime.MaxValue));

        if (!hasPostProcessService)
        {
            _logger.LogTrace(
                "[BaseStrategy] PLAYER_EVENT 제외: PostProcessService 없음 (piid={Piid})",
                piidObj);
            return false;
        }

        _logger.LogTrace(
            "[BaseStrategy] PLAYER_EVENT 승인: piid={Piid}, PostProcessService 존재",
            piidObj);
        return true;
    }

    /// <summary>
    /// URI_PERMISSION_GRANT 검증 (임시 파일만)
    /// </summary>
    private bool ValidateUriPermission(NormalizedLogEvent Artifact)
    {
        if (!Artifact.Attributes.TryGetValue("uri", out var uriObj))
        {
            return false;
        }

        var uri = uriObj?.ToString() ?? string.Empty;
        
        // 앨범 경로 제외
        if (IsAlbumPath(uri))
        {
            _logger.LogTrace(
                "[BaseStrategy] URI_PERMISSION_GRANT 제외: 앨범 경로 (uri={Uri})",
                uri);
            return false;
        }

        // 임시 파일 경로만 허용
        return IsCapturePath(uri);
    }

    /// <summary>
    /// FOREGROUND_SERVICE를 촬영 아티팩트로 검증
    /// </summary>
    /// <remarks>
    /// <para>
    /// usagestats 기반 촬영 탐지 로직:
    /// </para>
    /// <para>
    /// <b>검증 조건:</b>
    /// 1. serviceState가 "FOREGROUND_SERVICE_START"여야 함
    /// 2. className이 설정된 촬영 서비스 클래스 목록에 포함되어야 함
    ///    - PostProcessService → 촬영 확정 (기본 카메라)
    ///    - NotificationService → 촬영 가능성 (기본 카메라, 카카오톡)
    /// </para>
    /// <para>
    /// <b>활용 시나리오:</b>
    /// - 재부팅 후 24시간 이내 로그
    /// - 다른 휘발성 로그 (vibrator, audio, media_camera_worker)는 사라짐
    /// - usagestats 로그만 남아있는 상황
    /// </para>
    /// <para>
    /// <b>장점:</b>
    /// - 기존 0% 탐지율 → 50~100% 탐지율로 개선
    /// - 기존 로직 영향 없음 (조건부 핵심 아티팩트 추가만)
    /// - YAML 설정으로 서비스 클래스 추가/제거 가능
    /// </para>
    /// <para>
    /// <b>한계:</b>
    /// - Telegram, Silent Camera는 적용 불가 (서비스 없음)
    /// - 카카오톡은 기본 카메라 호출 → com.sec.android.app.camera 패키지로 기록
    /// - 정확한 촬영 장수 확인 불가 (촬영 여부만 확인)
    /// </para>
    /// <para>
    /// <b>추가 (2025-10-26):</b> 재부팅 휘발성 대응
    /// </para>
    /// </remarks>
    /// <param name="artifact">FOREGROUND_SERVICE 이벤트</param>
    /// <param name="context">세션 컨텍스트 (미사용, 인터페이스 일관성 유지)</param>
    /// <returns>촬영 서비스면 true, 아니면 false</returns>
    private bool ValidateForegroundServiceAsCapture(
        NormalizedLogEvent artifact, 
        SessionContext context)
    {
        // 1단계: serviceState 확인 (START만 허용) - 헬퍼 사용
        if (!Services.Validation.ForegroundServiceValidator.IsValidStartEvent(artifact))
        {
            var serviceState = artifact.Attributes.GetValueOrDefault("serviceState")?.ToString();
            _logger.LogDebug(
                "[ValidateForegroundService] Session {SessionId}: serviceState={State} (SKIP, START만 허용)",
                context.Session.SessionId, serviceState);
            return false;
        }
        
        // 2단계: className 추출 - 헬퍼 사용
        var className = Services.Validation.ForegroundServiceValidator.ExtractClassName(artifact);
        if (string.IsNullOrEmpty(className))
        {
            _logger.LogDebug(
                "[ValidateForegroundService] Session {SessionId}: className 없음 (SKIP)",
                context.Session.SessionId);
            return false;
        }
        
        // 3단계: 촬영 확정 서비스 확인 (PostProcessService) - 헬퍼 사용
        var isConfirmedCapture = Services.Validation.ForegroundServiceValidator.MatchesAnyPattern(
            className, 
            _validation.ServiceClassCaptureConfirmed);
        
        if (isConfirmedCapture)
        {
            _logger.LogInformation(
                "[ValidateForegroundService] ✅ Session {SessionId}: 촬영 확정 서비스 발견: {ClassName}",
                context.Session.SessionId, className);
            return true;
        }
        
        // 4단계: 촬영 가능성 서비스 확인 (NotificationService) - 헬퍼 사용
        var isPossibleCapture = Services.Validation.ForegroundServiceValidator.MatchesAnyPattern(
            className, 
            _validation.ServiceClassCapturePossible);
        
        if (isPossibleCapture)
        {
            _logger.LogInformation(
                "[ValidateForegroundService] ⚠️ Session {SessionId}: 촬영 가능성 서비스 발견: {ClassName}",
                context.Session.SessionId, className);
            return true;
        }
        
        // 5단계: 해당 없음 → 촬영 아님
        _logger.LogDebug(
            "[ValidateForegroundService] Session {SessionId}: 촬영 서비스 아님: {ClassName}",
            context.Session.SessionId, className);
        return false;
    }


    /// <summary>
    /// 경로 패턴 검증
    /// </summary>
    private bool IsExcludedByPathPattern(
        NormalizedLogEvent Artifact,
        AnalysisOptions options)
    {
        // FilePath 확인
        if (Artifact.Attributes.TryGetValue("file_path", out var fpObj))
        {
            var filePath = fpObj?.ToString() ?? string.Empty;
            
            if (options.ScreenshotPathPatterns.Any(pattern => 
                filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            
            if (options.DownloadPathPatterns.Any(pattern => 
                filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // FileUri 확인
        if (Artifact.Attributes.TryGetValue("uri", out var uriObj))
        {
            var fileUri = uriObj?.ToString() ?? string.Empty;
            
            // DATABASE 이벤트는 MediaStore URI여도 신규 촬영으로 간주
            bool isDatabaseEvent = Artifact.EventType == LogEventTypes.DATABASE_INSERT ||
                                   Artifact.EventType == LogEventTypes.DATABASE_EVENT;
            
            if (!isDatabaseEvent && IsAlbumPath(fileUri))
            {
                return true;
            }
            
            if (options.ScreenshotPathPatterns.Any(pattern => 
                fileUri.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            
            if (options.DownloadPathPatterns.Any(pattern => 
                fileUri.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 시간 윈도우 기반 중복 제거
    /// </summary>
    /// <remarks>
    /// 동일한 촬영에 대해 여러 증거(VIBRATION_EVENT, PLAYER_EVENT 등)가 시간대별로 발생할 수 있음.
    /// 시간 윈도우(기본 1초) 내의 여러 캡처 이벤트를 하나로 통합.
    /// 우선순위: VIBRATION_EVENT > PLAYER_EVENT > URI_PERMISSION_GRANT > SILENT_CAMERA_CAPTURE
    /// </remarks>
    private List<CameraCaptureEvent> DeduplicateCapturesByTimeWindow(
        List<CameraCaptureEvent> captures,
        TimeSpan timeWindow)
    {
        if (captures.Count <= 1)
            return captures;

        var deduplicated = new List<CameraCaptureEvent>();
        var sorted = captures.OrderBy(c => c.CaptureTime).ToList();

        var processed = new HashSet<Guid>();

        for (int i = 0; i < sorted.Count; i++)
        {
            if (processed.Contains(sorted[i].CaptureId))
                continue;

            var current = sorted[i];
            var group = new List<CameraCaptureEvent> { current };
            processed.Add(current.CaptureId);

            // 시간 윈도우 내의 다른 캡처들을 그룹에 추가
            for (int j = i + 1; j < sorted.Count; j++)
            {
                var next = sorted[j];
                if ((next.CaptureTime - current.CaptureTime) <= timeWindow)
                {
                    group.Add(next);
                    processed.Add(next.CaptureId);
                }
                else
                {
                    break; // 시간 차이가 윈도우를 벗어나면 중단
                }
            }

            // 그룹에서 우선순위가 가장 높은 캡처 선택
            var best = SelectBestCaptureFromGroup(group);
            deduplicated.Add(best);

            if (group.Count > 1)
            {
                var ArtifactTypesSummary = string.Join(", ", best.ArtifactTypes);
                _logger.LogDebug(
                    "[BaseStrategy] 중복 그룹 통합: {Count}개 → 1개 (Time={Time:HH:mm:ss.fff}, ArtifactTypes=[{ArtifactTypes}])",
                    group.Count, best.CaptureTime, ArtifactTypesSummary);
            }
        }

        return deduplicated;
    }

    /// <summary>
    /// 그룹에서 가장 신뢰도 높은 캡처 선택
    /// </summary>
    private CameraCaptureEvent SelectBestCaptureFromGroup(List<CameraCaptureEvent> group)
    {
        // 우선순위:
        // 1. VIBRATION_EVENT (mType=50061, status=finished) - 가장 신뢰도 높음
        // 2. PLAYER_EVENT (PostProcessService 검증 완료)
        // 3. URI_PERMISSION_GRANT
        // 4. SILENT_CAMERA_CAPTURE
        // 5. FOREGROUND_SERVICE (usagestats 기반, 추가 2025-10-26)

        var priorities = new Dictionary<string, int>
        {
            { LogEventTypes.VIBRATION_EVENT, 100 },
            { LogEventTypes.PLAYER_EVENT, 80 },
            { LogEventTypes.URI_PERMISSION_GRANT, 60 },
            { LogEventTypes.SILENT_CAMERA_CAPTURE, 50 },
            { LogEventTypes.FOREGROUND_SERVICE, 40 }
        };

        // ArtifactTypes에서 가장 우선순위가 높은 타입을 기준으로 정렬
        return group
            .OrderByDescending(c => c.ArtifactTypes.Max(et => priorities.GetValueOrDefault(et, 0)))
            .ThenByDescending(c => c.CaptureDetectionScore)
            .First();
    }
}
