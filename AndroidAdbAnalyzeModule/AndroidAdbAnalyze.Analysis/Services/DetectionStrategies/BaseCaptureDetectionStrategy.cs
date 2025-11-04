using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Context;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Parser.Core.Models;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.DetectionStrategies;

/// <summary>
/// 촬영 탐지 전략 추상 클래스 (Template Method Pattern)
/// </summary>
/// <remarks>
/// 모든 촬영 탐지 전략의 공통 로직을 제공합니다:
/// 
/// 공통 로직:
/// - 보조 아티팩트 수집 (시간 윈도우 기반)
/// - 신뢰도 계산 (가중치 합산)
/// - CameraCaptureEvent 생성
/// - 로깅
/// 
/// 전략별 구현:
/// - 핵심 아티팩트 검색 (GetKeyArtifacts)
/// - 보조 아티팩트 타입 정의 (GetSupportingArtifactTypes)
/// - 중복 제거 여부 (DeduplicateCaptures)
/// - 경로 패턴 검증 (ShouldExcludeByPathPattern)
/// </remarks>
public abstract class BaseCaptureDetectionStrategy : ICaptureDetectionStrategy
{
    /// <summary>
    /// 로거 인스턴스
    /// </summary>
    protected readonly ILogger _logger;
    
    /// <summary>
    /// 신뢰도 계산기
    /// </summary>
    protected readonly IConfidenceCalculator _confidenceCalculator;

    /// <summary>
    /// BaseCaptureDetectionStrategy 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="confidenceCalculator">신뢰도 계산기</param>
    protected BaseCaptureDetectionStrategy(
        ILogger logger,
        IConfidenceCalculator confidenceCalculator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
    }

    /// <inheritdoc/>
    public abstract string? PackageNamePattern { get; }

    /// <summary>
    /// 촬영 탐지 (Template Method)
    /// </summary>
    /// <remarks>
    /// 전체 촬영 탐지 프로세스를 정의합니다:
    /// 1. 핵심 아티팩트 검색 (전략별)
    /// 2. 각 핵심 아티팩트에 대해 처리 (공통)
    ///    - 경로 패턴 검증 (전략별)
    ///    - 보조 아티팩트 수집 (공통)
    ///    - 신뢰도 계산 (공통)
    ///    - 임계값 확인 (공통)
    ///    - CameraCaptureEvent 생성 (공통)
    /// 3. 중복 제거 (전략별)
    /// </remarks>
    public IReadOnlyList<CameraCaptureEvent> DetectCaptures(
        SessionContext context,
        AnalysisOptions options)
    {
        var captures = new List<CameraCaptureEvent>();

        // 1단계: 핵심 아티팩트 검색 (전략별 구현)
        var keyArtifacts = GetKeyArtifacts(context, options);
        
        LogKeyArtifactsFound(context, keyArtifacts.Count);

        // 2단계: 각 핵심 아티팩트에 대해 촬영 탐지
        foreach (var keyArtifact in keyArtifacts)
        {
            // 경로 패턴 검증 (전략별 Hook Method)
            if (ShouldExcludeByPathPattern(keyArtifact, options))
            {
                _logger.LogDebug(
                    "[{Strategy}] 경로 패턴 제외: EventId={EventId}",
                    GetType().Name, keyArtifact.EventId);
                continue;
            }

            // 보조 아티팩트 수집 (공통 로직)
            var supportingArtifacts = CollectSupportingArtifacts(
                keyArtifact,
                context,
                options.EventCorrelationWindow);

            _logger.LogInformation("┌─────────────────────────────────────────────────────────────┐");
            _logger.LogInformation("│ [CAPTURE_DETECTION] 촬영 탐지 시작                           │");
            _logger.LogInformation("├─────────────────────────────────────────────────────────────┤");
            _logger.LogInformation("│ [SESSION_INFO] SessionId: {SessionId}", context.Session.SessionId);
            _logger.LogInformation("│ [SESSION_INFO] 패키지명: {PackageName}", context.Session.PackageName);
            _logger.LogInformation("│ [SESSION_INFO] 세션 시간: {Start:HH:mm:ss} ~ {End:HH:mm:ss}", 
                context.Session.StartTime, 
                context.Session.EndTime);
            _logger.LogInformation("├─────────────────────────────────────────────────────────────┤");
            _logger.LogInformation("│ [STRATEGY_INFO] 사용 전략: {Strategy}", GetType().Name);
            _logger.LogInformation("│ [STRATEGY_INFO] 패키지 패턴: {Pattern}", PackageNamePattern ?? "N/A");
            _logger.LogInformation("│ [STRATEGY_INFO] 핵심 아티팩트: {KeyArtifact}", keyArtifact.EventType);
            _logger.LogInformation("│ [STRATEGY_INFO] 촬영 시각: {Time:HH:mm:ss.fff}", keyArtifact.Timestamp);
            _logger.LogInformation("│ [STRATEGY_INFO] 보조 아티팩트: {Count}개", supportingArtifacts.Count);
            if (supportingArtifacts.Count > 0)
            {
                _logger.LogInformation("│ [STRATEGY_INFO] 보조 목록: [{SupportingTypes}]", 
                    string.Join(", ", supportingArtifacts.Select(a => a.EventType)));
            }
            _logger.LogInformation("└─────────────────────────────────────────────────────────────┘");

            // 탐지 점수 계산 (공통 로직)
            var allArtifacts = new List<NormalizedLogEvent> { keyArtifact };
            allArtifacts.AddRange(supportingArtifacts);
            var score = _confidenceCalculator.CalculateConfidence(allArtifacts);

            // CameraCaptureEvent 생성 (공통 로직)
            var capture = CreateCaptureEvent(
                context.Session,
                keyArtifact,
                supportingArtifacts,
                allArtifacts,
                score);

            captures.Add(capture);

            _logger.LogInformation("┌═════════════════════════════════════════════════════════════┐");
            _logger.LogInformation("║ [CAPTURE_SUCCESS] ✅ 촬영 탐지 성공                          ║");
            _logger.LogInformation("╞═════════════════════════════════════════════════════════════╡");
            _logger.LogInformation("║ CaptureId: {CaptureId}", capture.CaptureId);
            _logger.LogInformation("║ 촬영 시각: {Time:HH:mm:ss.fff}", capture.CaptureTime);
            _logger.LogInformation("║ 탐지 점수: {Score:F2}", capture.CaptureDetectionScore);
            _logger.LogInformation("└═════════════════════════════════════════════════════════════┘");
            _logger.LogInformation("");  // 빈 줄로 구분
        }

        // 3단계: 중복 제거 (전략별 Hook Method)
        var deduplicated = DeduplicateCaptures(captures, options);
        
        if (captures.Count != deduplicated.Count)
        {
            _logger.LogInformation(
                "[{Strategy}] Session {SessionId}: 중복 제거 완료 ({Before}개 → {After}개)",
                GetType().Name, context.Session.SessionId, captures.Count, deduplicated.Count);
        }

        return deduplicated;
    }

    /// <summary>
    /// 핵심 아티팩트 검색 (Abstract Method - 각 전략이 반드시 구현)
    /// </summary>
    /// <param name="context">세션 컨텍스트</param>
    /// <param name="options">분석 옵션</param>
    /// <returns>핵심 아티팩트 목록</returns>
    protected abstract List<NormalizedLogEvent> GetKeyArtifacts(
        SessionContext context,
        AnalysisOptions options);

    /// <summary>
    /// 보조 아티팩트 타입 정의 (Abstract Method - 각 전략이 반드시 구현)
    /// </summary>
    /// <returns>보조 아티팩트 타입 집합</returns>
    protected abstract HashSet<string> GetSupportingArtifactTypes();

    /// <summary>
    /// 경로 패턴 검증 (Hook Method - 필요시 오버라이드)
    /// </summary>
    /// <param name="artifact">검증할 아티팩트</param>
    /// <param name="options">분석 옵션</param>
    /// <returns>제외 여부 (true: 제외, false: 포함)</returns>
    protected virtual bool ShouldExcludeByPathPattern(
        NormalizedLogEvent artifact,
        AnalysisOptions options)
    {
        return false; // 기본값: 제외하지 않음
    }

    /// <summary>
    /// 중복 제거 (Hook Method - 필요시 오버라이드)
    /// </summary>
    /// <param name="captures">촬영 이벤트 목록</param>
    /// <param name="options">분석 옵션</param>
    /// <returns>중복 제거된 촬영 이벤트 목록</returns>
    protected virtual IReadOnlyList<CameraCaptureEvent> DeduplicateCaptures(
        List<CameraCaptureEvent> captures,
        AnalysisOptions options)
    {
        return captures; // 기본값: 중복 제거 안 함
    }

    /// <summary>
    /// 핵심 아티팩트 발견 로깅 (Hook Method - 필요시 오버라이드)
    /// </summary>
    /// <param name="context">세션 컨텍스트</param>
    /// <param name="count">핵심 아티팩트 개수</param>
    protected virtual void LogKeyArtifactsFound(SessionContext context, int count)
    {
        _logger.LogDebug(
            "[{Strategy}] Session {SessionId} ({Package}): 핵심 아티팩트 {Count}개",
            GetType().Name, context.Session.SessionId, context.Session.PackageName, count);
    }

    /// <summary>
    /// 보조 아티팩트 수집 (공통 로직, Hook Method)
    /// </summary>
    /// <param name="keyArtifact">핵심 아티팩트</param>
    /// <param name="context">세션 컨텍스트</param>
    /// <param name="correlationWindow">상관관계 윈도우 (±시간)</param>
    /// <returns>보조 아티팩트 목록</returns>
    /// <remarks>
    /// 전략별로 보조 아티팩트 수집 로직을 커스터마이즈할 수 있습니다.
    /// 예: TelegramStrategy는 PLAYER_EVENT를 제외합니다.
    /// </remarks>
    protected virtual List<NormalizedLogEvent> CollectSupportingArtifacts(
        NormalizedLogEvent keyArtifact,
        SessionContext context,
        TimeSpan correlationWindow)
    {
        var windowStart = keyArtifact.Timestamp - correlationWindow;
        var windowEnd = keyArtifact.Timestamp + correlationWindow;

        var supportingArtifactTypes = GetSupportingArtifactTypes();

        var supportingEvents = context.AllEvents
            .Where(e =>
                e.EventId != keyArtifact.EventId &&
                supportingArtifactTypes.Contains(e.EventType) &&
                e.Timestamp >= windowStart &&
                e.Timestamp <= windowEnd)
            .ToList();

        return supportingEvents;
    }

    /// <summary>
    /// CameraCaptureEvent 생성 (공통 로직)
    /// </summary>
    /// <param name="session">세션</param>
    /// <param name="keyArtifact">핵심 아티팩트</param>
    /// <param name="supportingArtifacts">보조 아티팩트 목록</param>
    /// <param name="allArtifacts">전체 아티팩트 목록 (핵심 + 보조)</param>
    /// <param name="score">신뢰도 점수</param>
    /// <returns>촬영 이벤트</returns>
    protected virtual CameraCaptureEvent CreateCaptureEvent(
        CameraSession session,
        NormalizedLogEvent keyArtifact,
        List<NormalizedLogEvent> supportingArtifacts,
        List<NormalizedLogEvent> allArtifacts,
        double score)
    {
        // FilePath 추출
        string? filePath = null;
        if (keyArtifact.Attributes.TryGetValue("file_path", out var fpObj))
            filePath = fpObj?.ToString();

        // FileUri 추출
        string? fileUri = null;
        if (keyArtifact.Attributes.TryGetValue("uri", out var uriObj))
            fileUri = uriObj?.ToString();

        // 증거 타입 목록
        var artifactTypes = allArtifacts
            .Select(e => e.EventType)
            .Distinct()
            .ToList();

        // 메타데이터 수집
        var metadata = new Dictionary<string, string>
        {
            ["detection_strategy"] = GetType().Name,
            ["key_artifact_type"] = keyArtifact.EventType
        };
        
        foreach (var attr in keyArtifact.Attributes)
        {
            if (attr.Value != null)
                metadata[attr.Key] = attr.Value.ToString() ?? string.Empty;
        }

        return new CameraCaptureEvent
        {
            CaptureId = Guid.NewGuid(),
            ParentSessionId = session.SessionId,
            CaptureTime = keyArtifact.Timestamp,
            PackageName = session.PackageName,
            FilePath = filePath,
            FileUri = fileUri,
            decisiveArtifact = keyArtifact.EventId,
            SupportingArtifactIds = supportingArtifacts.Select(e => e.EventId).ToList(),
            IsEstimated = false,
            CaptureDetectionScore = score,
            ArtifactTypes = artifactTypes,
            SourceEventIds = allArtifacts.Select(e => e.EventId).ToList(),
            Metadata = metadata
        };
    }

    /// <summary>
    /// 임시 파일 경로 확인 (공통 유틸리티)
    /// </summary>
    protected static bool IsCapturePath(string uri)
    {
        return uri.Contains("/tmp/", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("/cache/", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("temp_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 앨범 경로 확인 (공통 유틸리티)
    /// </summary>
    /// <remarks>
    /// 다음 패턴들을 갤러리/앨범 접근으로 간주:
    /// 1. MediaStore 경로 (content://media/external/)
    /// 2. 외부 저장소 직접 접근 (/storage/, /sdcard/)
    /// 3. FileProvider 경로 (앱 간 파일 공유용)
    /// 4. 사진 저장 폴더 (/DCIM/, /Pictures/, /Download/)
    /// </remarks>
    protected static bool IsAlbumPath(string uri)
    {
        // 1. MediaStore 경로 (기존)
        if (uri.Contains("content://media/external/images", StringComparison.OrdinalIgnoreCase) ||
            uri.Contains("content://media/external/video", StringComparison.OrdinalIgnoreCase) ||
            uri.Contains("com.google.android.providers.media", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. 외부 저장소 직접 접근
        if (uri.Contains("/storage/emulated/", StringComparison.OrdinalIgnoreCase) ||
            uri.Contains("/sdcard/", StringComparison.OrdinalIgnoreCase) ||
            uri.Contains("/external_storage/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. 앱별 FileProvider (갤러리 공유용)
        if (uri.Contains(".fileprovider", StringComparison.OrdinalIgnoreCase) ||
            uri.Contains(".provider.fileprovider", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 4. DCIM, Pictures 등 사진 저장 폴더
        if (uri.Contains("/DCIM/", StringComparison.OrdinalIgnoreCase) ||
            uri.Contains("/Pictures/", StringComparison.OrdinalIgnoreCase) ||
            uri.Contains("/Download/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

