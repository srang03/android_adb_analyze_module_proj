using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Core.Constants;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Options;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Captures;

/// <summary>
/// 카메라 촬영 이벤트 감지 서비스 구현 (Orchestrator)
/// </summary>
/// <remarks>
/// usagestats 기반 세션 컨텍스트를 구성하고,
/// 앱별 Strategy를 선택하여 촬영 탐지를 위임합니다.
/// </remarks>
public sealed class CameraCaptureDetector : ICaptureDetector
{
    private readonly ILogger<CameraCaptureDetector> _logger;
    private readonly ISessionContextProvider _contextProvider;
    private readonly IReadOnlyList<ICaptureDetectionStrategy> _strategies;

    public CameraCaptureDetector(
        ILogger<CameraCaptureDetector> logger,
        ISessionContextProvider contextProvider,
        IEnumerable<ICaptureDetectionStrategy> strategies)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _strategies = strategies?.OrderByDescending(s => s.Priority).ToList() 
                     ?? throw new ArgumentNullException(nameof(strategies));
        
        _logger.LogInformation(
            "[CameraCaptureDetector] 초기화 완료: Strategy {Count}개 등록 ({Strategies})",
            _strategies.Count,
            string.Join(", ", _strategies.Select(s => $"{s.GetType().Name}(Priority={s.Priority})")));
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
            "AllEvents={AllEvents}, ActivityResumed={Resumed}, ActivityPaused={Paused}, ForegroundServices={Services}",
            session.SessionId, context.AllEvents.Count, 
            context.ActivityResumedTime?.ToString("HH:mm:ss.fff") ?? "N/A",
            context.ActivityPausedTime?.ToString("HH:mm:ss.fff") ?? "N/A",
            context.ForegroundServices.Count);
        
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

        return captures;
    }
    
    /// <summary>
    /// 패키지명 기반 Strategy 선택
    /// </summary>
    /// <param name="packageName">앱 패키지명</param>
    /// <returns>적용할 Strategy (우선순위순)</returns>
    private ICaptureDetectionStrategy SelectStrategy(string packageName)
    {
        // Priority 순으로 정렬된 Strategy 목록에서
        // PackageNamePattern이 매칭되는 첫 번째 Strategy 선택
        foreach (var strategy in _strategies)
        {
            if (strategy.PackageNamePattern == null)
            {
                // 기본 Strategy (PackageNamePattern이 null)
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
