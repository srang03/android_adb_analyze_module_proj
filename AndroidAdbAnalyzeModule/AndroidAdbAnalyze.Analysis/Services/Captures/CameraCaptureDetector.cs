using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using Microsoft.Extensions.Logging;
using AndroidAdbAnalyze.Parser.Core.Models;

namespace AndroidAdbAnalyze.Analysis.Services.Captures;

/// <summary>
/// 카메라 촬영 이벤트 감지 서비스 구현 (Orchestrator)
/// </summary>
/// <remarks>
/// usagestats 기반 세션 컨텍스트를 구성하고,
/// 앱별 Strategy를 선택하여 촬영 탐지를 위임합니다.
/// 선택적으로 전송 탐지 기능을 통합합니다.
/// </remarks>
public sealed class CameraCaptureDetector : ICaptureDetector
{
    private readonly ILogger<CameraCaptureDetector> _logger;
    private readonly ISessionContextProvider _contextProvider;
    private readonly IReadOnlyList<ICaptureDetectionStrategy> _strategies;
    private readonly ITransmissionDetector? _transmissionDetector;

    /// <summary>
    /// CameraCaptureDetector 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="logger">로거</param>
    /// <param name="contextProvider">세션 컨텍스트 제공자</param>
    /// <param name="strategies">촬영 탐지 전략 목록</param>
    /// <param name="transmissionDetector">전송 탐지 서비스 (선택적)</param>
    public CameraCaptureDetector(
        ILogger<CameraCaptureDetector> logger,
        ISessionContextProvider contextProvider,
        IEnumerable<ICaptureDetectionStrategy> strategies,
        ITransmissionDetector? transmissionDetector = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _strategies = strategies?.ToList() ?? throw new ArgumentNullException(nameof(strategies));
        _transmissionDetector = transmissionDetector;
        
        _logger.LogInformation(
            "[CameraCaptureDetector] 초기화 완료: Strategy {Count}개 등록 ({Strategies}), " +
            "TransmissionDetector={TransmissionEnabled}",
            _strategies.Count,
            string.Join(", ", _strategies.Select(s => s.GetType().Name)),
            _transmissionDetector != null ? "Enabled" : "Disabled");
    }

    /// <inheritdoc/>
    public IReadOnlyList<CameraCaptureEvent> DetectCaptures(
        CameraSession session,
        IReadOnlyList<NormalizedLogEvent> events,
        AnalysisOptions options)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        
        if (events == null || events.Count == 0)
        {
            _logger.LogDebug(
                "[CameraCaptureDetector] Session {SessionId} ({Package}): 이벤트가 없으므로 빈 촬영 목록 반환",
                session.SessionId, session.PackageName);
            return Array.Empty<CameraCaptureEvent>();
        }

        _logger.LogInformation(
            "[CameraCaptureDetector] 촬영 감지 시작: Session={SessionId}, Package={Package}, Events={EventCount}",
            session.SessionId, session.PackageName, events.Count);

        // 1단계: SessionContext 생성 (usagestats 기반)
        var context = _contextProvider.CreateContext(session, events);
        
        _logger.LogDebug(
            "[CameraCaptureDetector] SessionContext 생성 완료: Session={SessionId}, " +
            "AllEvents={AllEvents}, ForegroundServices={Services}",
            session.SessionId, context.AllEvents.Count, context.ForegroundServices.Count);
        
        // 2단계: Strategy 선택
        var selectedStrategy = SelectStrategy(session.PackageName);
        
        _logger.LogInformation(
            "[CameraCaptureDetector] Strategy 선택: Session={SessionId}, Package={Package}, Strategy={Strategy}",
            session.SessionId, session.PackageName, selectedStrategy.GetType().Name);
        
        // 3단계: Strategy로 촬영 탐지 위임
        var captures = selectedStrategy.DetectCaptures(context, options);
        
        _logger.LogInformation(
            "[CameraCaptureDetector] 촬영 감지 완료: Session={SessionId}, Package={Package}, " +
            "Captures={Count}, Strategy={Strategy}",
            session.SessionId, session.PackageName, captures.Count, selectedStrategy.GetType().Name);

        // 4단계: 전송 탐지 (선택적)
        if (_transmissionDetector != null && options.EnableTransmissionDetection && captures.Count > 0)
        {
            _logger.LogDebug(
                "[CameraCaptureDetector] 전송 탐지 시작: Session={SessionId}, Captures={Count}",
                session.SessionId, captures.Count);

            var capturesWithTransmission = new List<CameraCaptureEvent>();
            
            foreach (var capture in captures)
            {
                var transmissionResult = _transmissionDetector.DetectTransmission(
                    capture,
                    context.AllEvents,
                    options);

                // 전송 정보가 있으면 CameraCaptureEvent 업데이트
                if (transmissionResult.IsTransmitted)
                {
                    _logger.LogInformation(
                        "[CameraCaptureDetector] 전송 탐지: CaptureId={CaptureId}, " +
                        "TransmissionTime={Time:HH:mm:ss.fff}, Packets={Packets}",
                        capture.CaptureId, transmissionResult.TransmissionTime, 
                        transmissionResult.TransmittedPackets);

                    // 불변 객체이므로 새 인스턴스 생성 (기존 필드 보존)
                    var updatedCapture = new CameraCaptureEvent
                    {
                        // 기존 필드 복사
                        CaptureId = capture.CaptureId,
                        ParentSessionId = capture.ParentSessionId,
                        CaptureTime = capture.CaptureTime,
                        PackageName = capture.PackageName,
                        FilePath = capture.FilePath,
                        FileUri = capture.FileUri,
                        decisiveArtifact = capture.decisiveArtifact,
                        SupportingArtifactIds = capture.SupportingArtifactIds,
                        IsEstimated = capture.IsEstimated,
                        CaptureDetectionScore = capture.CaptureDetectionScore,
                        ArtifactTypes = capture.ArtifactTypes,
                        SourceEventIds = capture.SourceEventIds,
                        Metadata = capture.Metadata,
                        
                        // 전송 정보 추가
                        IsTransmitted = true,
                        TransmissionTime = transmissionResult.TransmissionTime,
                        TransmittedPackets = transmissionResult.TransmittedPackets
                    };
                    
                    capturesWithTransmission.Add(updatedCapture);
                }
                else
                {
                    // 전송 미탐지 - 기존 capture 그대로 사용
                    capturesWithTransmission.Add(capture);
                }
            }
            
            _logger.LogInformation(
                "[CameraCaptureDetector] 전송 탐지 완료: Session={SessionId}, " +
                "Transmitted={TransmittedCount}/{TotalCount}",
                session.SessionId, 
                capturesWithTransmission.Count(c => c.IsTransmitted),
                capturesWithTransmission.Count);

            return capturesWithTransmission;
        }
        return captures;
    }
    
    /// <summary>
    /// 패키지명 기반 Strategy 선택
    /// </summary>
    /// <param name="packageName">앱 패키지명</param>
    /// <returns>적용할 Strategy</returns>
    /// <remarks>
    /// PackageNamePattern이 매칭되는 첫 번째 Strategy를 선택합니다.
    /// 매칭되지 않으면 기본 Strategy (PackageNamePattern == null)를 반환합니다.
    /// </remarks>
    private ICaptureDetectionStrategy SelectStrategy(string packageName)
    {
        // PackageNamePattern이 매칭되는 첫 번째 Strategy 선택
        foreach (var strategy in _strategies)
        {
            if (strategy.PackageNamePattern == null)
            {
                // 기본 Strategy (PackageNamePattern이 null) - 나중에 처리
                continue;
            }
            
            if (packageName.Contains(strategy.PackageNamePattern, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "[CameraCaptureDetector] Strategy 매칭: Package={Package}, Pattern={Pattern}, Strategy={Strategy}",
                    packageName, strategy.PackageNamePattern, strategy.GetType().Name);
                return strategy;
            }
        }
        
        // 매칭되는 Strategy가 없으면 기본 Strategy (PackageNamePattern == null) 반환
        var defaultStrategy = _strategies.FirstOrDefault(s => s.PackageNamePattern == null);
        if (defaultStrategy == null)
        {
            throw new InvalidOperationException(
                $"기본 Strategy (PackageNamePattern == null)가 등록되지 않았습니다. " +
                $"최소 1개의 기본 Strategy를 등록해야 합니다.");
        }
        
        _logger.LogDebug(
            "[CameraCaptureDetector] 기본 Strategy 사용: Package={Package}, Strategy={Strategy}",
            packageName, defaultStrategy.GetType().Name);
        return defaultStrategy;
    }
}
