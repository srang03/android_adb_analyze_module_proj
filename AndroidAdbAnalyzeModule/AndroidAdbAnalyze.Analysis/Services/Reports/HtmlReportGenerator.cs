using System.Text;
using System.Web;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Events;
using AndroidAdbAnalyze.Analysis.Models.Results;
using AndroidAdbAnalyze.Analysis.Models.Sessions;
using AndroidAdbAnalyze.Analysis.Models.Visualization;
using Microsoft.Extensions.Logging;

namespace AndroidAdbAnalyze.Analysis.Services.Reports;

/// <summary>
/// HTML 포렌식 분석 보고서 생성기
/// </summary>
public sealed class HtmlReportGenerator : IReportGenerator
{
    private const int DEFAULT_STRING_BUILDER_CAPACITY = 50000; // 약 50KB의 HTML 예상

    private readonly ITimelineBuilder _timelineBuilder;
    private readonly ILogger<HtmlReportGenerator> _logger;

    /// <summary>
    /// HtmlReportGenerator 인스턴스를 생성합니다.
    /// </summary>
    /// <param name="timelineBuilder">타임라인 구성 서비스</param>
    /// <param name="logger">로거</param>
    public HtmlReportGenerator(
        ITimelineBuilder timelineBuilder,
        ILogger<HtmlReportGenerator> logger)
    {
        _timelineBuilder = timelineBuilder ?? throw new ArgumentNullException(nameof(timelineBuilder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Format => "HTML";

    /// <inheritdoc/>
    public string GenerateReport(AnalysisResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        _logger.LogInformation("HTML 보고서 생성 시작");

        var html = new StringBuilder(DEFAULT_STRING_BUILDER_CAPACITY);

        AppendHtmlHeader(html);
        AppendStyles(html);
        html.AppendLine("<body>");
        html.AppendLine("<div class=\"container\">");

        AppendReportHeader(html);
        AppendMetadataSection(html, result);
        AppendExecutiveSummary(html, result);
        
        if (result.Sessions.Any())
            AppendSessionTable(html, result.Sessions);
        
        if (result.CaptureEvents.Any())
            AppendCaptureTable(html, result.CaptureEvents);
        
        var timelineItems = _timelineBuilder.BuildTimeline(result);
        if (timelineItems.Any())
            AppendTimelineChart(html, timelineItems);
        
        AppendStatistics(html, result.Statistics);
        
        if (result.Errors.Any() || result.Warnings.Any())
            AppendErrorsAndWarnings(html, result);
        
        AppendAppendix(html);
        AppendFooter(html, result);

        html.AppendLine("</div>"); // container
        AppendChartScript(html, timelineItems);
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        _logger.LogInformation("HTML 보고서 생성 완료 (크기: {Size} bytes)", html.Length);

        return html.ToString();
    }

    private void AppendHtmlHeader(StringBuilder html)
    {
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"ko\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("    <title>모바일 로그 분석 보고서 - Android ADB 로그 분석</title>");
    }

    private void AppendStyles(StringBuilder html)
    {
        html.AppendLine("    <style>");
        html.AppendLine(HtmlStyles.CSS);
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
    }

    private void AppendReportHeader(StringBuilder html)
    {
        html.AppendLine("        <div class=\"report-header\">");
        html.AppendLine("            <h1>📱 모바일 로그 분석 보고서</h1>");
        html.AppendLine("            <p class=\"subtitle\">Android ADB System Log Analysis</p>");
        html.AppendLine("        </div>");
    }

    private void AppendMetadataSection(StringBuilder html, AnalysisResult result)
    {
        html.AppendLine("        <div class=\"metadata-section\">");
        html.AppendLine("            <h2 style=\"color: #2c3e50; margin-bottom: 15px;\">보고서 정보</h2>");
        html.AppendLine("            <div class=\"metadata-grid\">");

        AppendMetadataItem(html, "보고서 번호", $"ADB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}");
        AppendMetadataItem(html, "분석 일시", result.Statistics.AnalysisStartTime.ToString("yyyy-MM-dd HH:mm:ss") + " (로컬 시간)");

        if (result.DeviceInfo != null)
        {
            if (!string.IsNullOrEmpty(result.DeviceInfo.Manufacturer))
                AppendMetadataItem(html, "디바이스 제조사", Escape(result.DeviceInfo.Manufacturer));
            
            if (!string.IsNullOrEmpty(result.DeviceInfo.Model))
                AppendMetadataItem(html, "디바이스 모델", Escape(result.DeviceInfo.Model));
            
            if (!string.IsNullOrEmpty(result.DeviceInfo.AndroidVersion))
                AppendMetadataItem(html, "Android 버전", $"Android {Escape(result.DeviceInfo.AndroidVersion)}");
        }

        AppendMetadataItem(html, "처리 시간", $"{result.Statistics.ProcessingTime.TotalSeconds:F3}초");

        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
    }

    private void AppendMetadataItem(StringBuilder html, string label, string value)
    {
        html.AppendLine("                <div class=\"metadata-item\">");
        html.AppendLine($"                    <span class=\"metadata-label\">{Escape(label)}</span>");
        html.AppendLine($"                    <span class=\"metadata-value\">{value}</span>");
        html.AppendLine("                </div>");
    }

    private void AppendExecutiveSummary(StringBuilder html, AnalysisResult result)
    {
        html.AppendLine("        <div class=\"content-section\">");
        html.AppendLine("            <h2 class=\"section-title\">📊 Executive Summary</h2>");
        html.AppendLine("            <div class=\"executive-summary\">");
        
        var avgConfidence = result.CaptureEvents.Any() 
            ? result.CaptureEvents.Average(c => c.CaptureDetectionScore) * 100
            : 0;

        html.AppendLine($"                <p><strong>분석 개요:</strong> 본 보고서는 Android ADB 시스템 로그를 분석하여 카메라 사용 이력 및 촬영 활동을 식별한 결과를 포함합니다. " +
                       $"총 <strong>{result.Statistics.TotalSourceEvents:N0}개</strong>의 로그 이벤트를 처리하여 " +
                       $"<strong>{result.Statistics.TotalSessions}개</strong>의 카메라 세션과 " +
                       $"<strong>{result.Statistics.TotalCaptureEvents}개</strong>의 촬영 이벤트를 감지하였습니다.</p>");

        html.AppendLine("                <div class=\"summary-stats\">");
        AppendStatCard(html, result.Statistics.TotalSourceEvents.ToString("N0"), "처리된 이벤트");
        AppendStatCard(html, result.Statistics.TotalSessions.ToString(), "카메라 세션");
        AppendStatCard(html, result.Statistics.TotalCaptureEvents.ToString(), "촬영 이벤트");
        AppendStatCard(html, $"{avgConfidence:F0}%", "평균 신뢰도");
        html.AppendLine("                </div>");

        html.AppendLine("            </div>");
        html.AppendLine("            <div class=\"alert alert-info\">");
        html.AppendLine("                <strong>ℹ️ 정보:</strong> 모든 타임스탬프는 로그가 생성된 디바이스의 로컬 시간 기준으로 표시됩니다.");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
    }

    private void AppendStatCard(StringBuilder html, string number, string label)
    {
        html.AppendLine("                    <div class=\"stat-card\">");
        html.AppendLine($"                        <div class=\"stat-number\">{Escape(number)}</div>");
        html.AppendLine($"                        <div class=\"stat-label\">{Escape(label)}</div>");
        html.AppendLine("                    </div>");
    }

    private void AppendSessionTable(StringBuilder html, IReadOnlyList<CameraSession> sessions)
    {
        html.AppendLine("        <div class=\"content-section\">");
        html.AppendLine("            <h2 class=\"section-title\">📹 카메라 세션 분석</h2>");
        html.AppendLine("            <p>감지된 카메라 세션 목록입니다. 각 세션은 카메라 앱의 시작부터 종료까지의 기간을 나타냅니다.</p>");
        html.AppendLine("            <table class=\"data-table\">");
        html.AppendLine("                <thead>");
        html.AppendLine("                    <tr>");
        html.AppendLine("                        <th>#</th>");
        html.AppendLine("                        <th>패키지명</th>");
        html.AppendLine("                        <th>시작 시간</th>");
        html.AppendLine("                        <th>종료 시간</th>");
        html.AppendLine("                        <th>지속 시간</th>");
        html.AppendLine("                        <th>상태</th>");
        html.AppendLine("                        <th>신뢰도</th>");
        html.AppendLine("                    </tr>");
        html.AppendLine("                </thead>");
        html.AppendLine("                <tbody>");

        for (int i = 0; i < sessions.Count; i++)
        {
            var session = sessions[i];
            html.AppendLine("                    <tr>");
            html.AppendLine($"                        <td>{i + 1}</td>");
            html.AppendLine($"                        <td><code>{Escape(session.PackageName)}</code></td>");
            html.AppendLine($"                        <td>{FormatDateTime(session.StartTime)}</td>");
            html.AppendLine($"                        <td>{(session.EndTime.HasValue ? FormatDateTime(session.EndTime.Value) : "-")}</td>");
            html.AppendLine($"                        <td>{FormatDuration(session.Duration)}</td>");
            html.AppendLine($"                        <td>{GetStatusBadge(session.IsIncomplete)}</td>");
            html.AppendLine($"                        <td>{GetConfidenceBar(session.SessionCompletenessScore)}</td>");
            html.AppendLine("                    </tr>");
        }

        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");
        html.AppendLine("        </div>");
    }

    private void AppendCaptureTable(StringBuilder html, IReadOnlyList<CameraCaptureEvent> captures)
    {
        html.AppendLine("        <div class=\"content-section\">");
        html.AppendLine("            <h2 class=\"section-title\">📸 촬영 이벤트 분석</h2>");
        html.AppendLine("            <p>감지된 카메라 촬영 이벤트 목록입니다. 각 이벤트는 실제 사진 또는 비디오 촬영을 나타냅니다.</p>");
        html.AppendLine("            <table class=\"data-table\">");
        html.AppendLine("                <thead>");
        html.AppendLine("                    <tr>");
        html.AppendLine("                        <th>#</th>");
        html.AppendLine("                        <th>촬영 시간</th>");
        html.AppendLine("                        <th>패키지명</th>");
        html.AppendLine("                        <th>파일 경로</th>");
        html.AppendLine("                        <th>유형</th>");
        html.AppendLine("                        <th>신뢰도</th>");
        html.AppendLine("                    </tr>");
        html.AppendLine("                </thead>");
        html.AppendLine("                <tbody>");

        for (int i = 0; i < captures.Count; i++)
        {
            var capture = captures[i];
            html.AppendLine("                    <tr>");
            html.AppendLine($"                        <td>{i + 1}</td>");
            html.AppendLine($"                        <td>{FormatDateTime(capture.CaptureTime)}</td>");
            html.AppendLine($"                        <td><code>{Escape(capture.PackageName)}</code></td>");
            
            var filePath = !string.IsNullOrEmpty(capture.FilePath) 
                ? $"<code>{Escape(capture.FilePath)}</code>" 
                : "-";
            html.AppendLine($"                        <td>{filePath}</td>");
            
            html.AppendLine($"                        <td>{GetCaptureTypeBadge(capture.IsEstimated)}</td>");
            html.AppendLine($"                        <td>{GetConfidenceBar(capture.CaptureDetectionScore)}</td>");
            html.AppendLine("                    </tr>");
        }

        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");
        html.AppendLine("        </div>");
    }

    private void AppendTimelineChart(StringBuilder html, IReadOnlyList<TimelineItem> items)
    {
        // 날짜 범위 미리 계산 (HTML에서 표시하기 위해)
        var allTimes = items.SelectMany(i => new[] { i.StartTime, i.EndTime ?? i.StartTime }).ToList();
        var minTime = allTimes.Any() ? allTimes.Min() : DateTime.Now;
        var maxTime = allTimes.Any() ? allTimes.Max() : DateTime.Now;
        var dateRangeText = minTime.Date == maxTime.Date
            ? $"{minTime:yyyy년 M월 d일}"
            : $"{minTime:yyyy년 M월 d일} ~ {maxTime:M월 d일}";
        
        // Transmission 있는지 확인
        var hasTransmission = items.Any(i => i.EventType == Constants.TimelineEventTypes.TRANSMISSION);
        
        html.AppendLine("        <div class=\"content-section\">");
        html.AppendLine("            <h2 class=\"section-title\">⏱️ 타임라인 분석</h2>");
        html.AppendLine("            <p>시간순으로 정렬된 카메라 세션 및 촬영 이벤트를 시각화합니다.</p>");
        
        // 타이틀 및 날짜 (스크롤 영역 밖)
        html.AppendLine("            <div class=\"timeline-header\">");
        html.AppendLine("                <h3 class=\"timeline-title\">시간순 이벤트 타임라인 (세션 기간 + 촬영 시점)</h3>");
        html.AppendLine($"                <p class=\"timeline-date\">📅 {dateRangeText}</p>");
        html.AppendLine("            </div>");
        
        // 조작 안내
        html.AppendLine("            <div class=\"chart-controls\">");
        html.AppendLine("                <span class=\"scroll-hint\">💡 좌우 스크롤 | Ctrl+휠로 줌 | 드래그로 이동</span>");
        html.AppendLine("                <button class=\"btn-reset-zoom\" onclick=\"resetTimelineZoom()\">🔄 줌 초기화</button>");
        html.AppendLine("            </div>");
        
        // 메인 래퍼 (Flexbox: 왼쪽 고정 + 오른쪽 스크롤)
        html.AppendLine("            <div class=\"chart-main-wrapper\">");
        
        // 왼쪽 고정 영역 (Y축 + 범례)
        html.AppendLine("                <div class=\"timeline-y-axis-fixed\">");
        html.AppendLine("                    <div class=\"y-axis-title\">이벤트 타입</div>");
        html.AppendLine("                    <div class=\"y-axis-labels\">");
        html.AppendLine("                        <div class=\"y-label-item\">Session</div>");
        html.AppendLine("                        <div class=\"y-label-item\">Capture</div>");
        if (hasTransmission)
        {
            html.AppendLine("                        <div class=\"y-label-item\">Transmission</div>");
        }
        html.AppendLine("                    </div>");
        html.AppendLine("                    <div class=\"timeline-legend-left\" id=\"timelineLegendLeft\">");
        html.AppendLine("                        <!-- JavaScript로 범례 생성 -->");
        html.AppendLine("                    </div>");
        html.AppendLine("                </div>");
        
        // 오른쪽 스크롤 영역 (차트)
        html.AppendLine("                <div class=\"chart-scroll-area\">");
        html.AppendLine("                    <div class=\"chart-container-fixed\">");
        html.AppendLine("                        <canvas id=\"timelineChart\"></canvas>");
        html.AppendLine("                    </div>");
        html.AppendLine("                </div>");
        
        html.AppendLine("            </div>"); // chart-main-wrapper
        html.AppendLine("        </div>"); // content-section
    }

    private void AppendStatistics(StringBuilder html, AnalysisStatistics stats)
    {
        html.AppendLine("        <div class=\"content-section\">");
        html.AppendLine("            <h2 class=\"section-title\">📈 상세 통계</h2>");

        // 처리 통계
        html.AppendLine("            <div class=\"subsection-title\">처리 통계</div>");
        html.AppendLine("            <table class=\"data-table\">");
        html.AppendLine("                <tbody>");
        AppendStatRow(html, "총 처리 이벤트 수", $"{stats.TotalSourceEvents:N0} 개");
        AppendStatRow(html, "중복 제거된 이벤트 수", $"{stats.DeduplicatedEvents:N0} 개 ({(stats.TotalSourceEvents > 0 ? (double)stats.DeduplicatedEvents / stats.TotalSourceEvents * 100 : 0):F1}%)");
        AppendStatRow(html, "고유 이벤트 수", $"{stats.TotalSourceEvents - stats.DeduplicatedEvents:N0} 개");
        
        // 시간 통계 - 단계별로 구분하여 표시
        if (stats.TotalPipelineTime.HasValue)
        {
            // 전체 파이프라인 시간이 있는 경우 (Console.Executor에서 실행)
            AppendStatRow(html, "▶ 전체 파이프라인 소요 시간", $"<strong>{stats.TotalPipelineTime.Value.TotalSeconds:F3} 초</strong>");
            
            if (stats.ParsingTime.HasValue)
            {
                AppendStatRow(html, "　├ 로그 파싱 시간", $"{stats.ParsingTime.Value.TotalSeconds:F3} 초");
            }
            
            AppendStatRow(html, "　└ 로그 분석 시간", $"{stats.ProcessingTime.TotalSeconds:F3} 초");
            
            if (stats.ParsingTime.HasValue && stats.ParsingTime.Value.TotalSeconds > 0)
            {
                var parsingEventsPerSecond = stats.TotalSourceEvents / stats.ParsingTime.Value.TotalSeconds;
                AppendStatRow(html, "평균 파싱 속도", $"{parsingEventsPerSecond:N0} 이벤트/초");
            }
        }
        else
        {
            // Analysis 모듈 단독 사용 시
            AppendStatRow(html, "분석 소요 시간", $"{stats.ProcessingTime.TotalSeconds:F3} 초");
            
            if (stats.ProcessingTime.TotalSeconds > 0)
            {
                var eventsPerSecond = stats.TotalSourceEvents / stats.ProcessingTime.TotalSeconds;
                AppendStatRow(html, "평균 분석 속도", $"{eventsPerSecond:N0} 이벤트/초");
            }
        }
        
        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");

        // 세션 통계
        html.AppendLine("            <div class=\"subsection-title\">세션 통계</div>");
        html.AppendLine("            <table class=\"data-table\">");
        html.AppendLine("                <tbody>");
        AppendStatRow(html, "총 카메라 세션 수", $"{stats.TotalSessions} 개");
        AppendStatRow(html, "완전한 세션", $"{stats.CompleteSessions} 개 ({(stats.TotalSessions > 0 ? (double)stats.CompleteSessions / stats.TotalSessions * 100 : 0):F0}%)");
        AppendStatRow(html, "불완전한 세션", $"{stats.IncompleteSessions} 개 ({(stats.TotalSessions > 0 ? (double)stats.IncompleteSessions / stats.TotalSessions * 100 : 0):F0}%)");
        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");

        // 촬영 통계
        html.AppendLine("            <div class=\"subsection-title\">촬영 통계</div>");
        html.AppendLine("            <table class=\"data-table\">");
        html.AppendLine("                <tbody>");
        AppendStatRow(html, "총 촬영 이벤트 수", $"{stats.TotalCaptureEvents} 개");
        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");

        html.AppendLine("        </div>");
    }

    private void AppendStatRow(StringBuilder html, string label, string value)
    {
        html.AppendLine("                    <tr>");
        html.AppendLine($"                        <td style=\"font-weight: 600; width: 40%;\">{Escape(label)}</td>");
        html.AppendLine($"                        <td>{Escape(value)}</td>");
        html.AppendLine("                    </tr>");
    }

    private void AppendErrorsAndWarnings(StringBuilder html, AnalysisResult result)
    {
        html.AppendLine("        <div class=\"content-section\">");
        
        if (result.Errors.Any())
        {
            html.AppendLine("            <h3 class=\"subsection-title\">⚠️ 에러</h3>");
            foreach (var error in result.Errors)
            {
                html.AppendLine("            <div class=\"alert alert-error\">");
                html.AppendLine($"                <strong>오류:</strong> {Escape(error)}");
                html.AppendLine("            </div>");
            }
        }

        if (result.Warnings.Any())
        {
            html.AppendLine("            <h3 class=\"subsection-title\">⚠️ 경고</h3>");
            foreach (var warning in result.Warnings)
            {
                html.AppendLine("            <div class=\"alert alert-warning\">");
                html.AppendLine($"                <strong>경고:</strong> {Escape(warning)}");
                html.AppendLine("            </div>");
            }
        }

        html.AppendLine("        </div>");
    }

    private void AppendAppendix(StringBuilder html)
    {
        html.AppendLine("        <div class=\"content-section\">");
        html.AppendLine("            <h2 class=\"section-title\">📎 부록</h2>");
        html.AppendLine("            <div class=\"subsection-title\">분석 방법론</div>");
        html.AppendLine("            <p>본 분석은 Android ADB 시스템 로그 파일을 기반으로 다음의 방법론을 사용하여 수행되었습니다:</p>");
        html.AppendLine("            <ul style=\"margin-left: 20px; margin-top: 10px;\">");
        html.AppendLine("                <li>이벤트 중복 제거: Jaccard 유사도 알고리즘 (임계값: 0.8)</li>");
        html.AppendLine("                <li>세션 감지: CAMERA_CONNECT/CAMERA_DISCONNECT 이벤트 쌍 매칭</li>");
        html.AppendLine("                <li>촬영 감지: DATABASE_INSERT, VIBRATION_EVENT, SHUTTER_SOUND 등의 아티팩트 기반 탐지</li>");
        html.AppendLine("                <li>탐지 점수 계산: 아티팩트 타입별 가중치 합산 방식</li>");
        html.AppendLine("            </ul>");
        html.AppendLine("            <div class=\"subsection-title\" style=\"margin-top: 30px;\">면책 조항</div>");
        html.AppendLine("            <div class=\"alert alert-info\">");
        html.AppendLine("                <p><strong>주의:</strong> 본 보고서는 자동화된 시스템을 통해 생성되었으며, 분석 결과는 제공된 로그 데이터의 품질과 완전성에 따라 달라질 수 있습니다. 법적 증거로 사용하기 전에 전문가의 검증이 필요합니다.</p>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
    }

    private void AppendFooter(StringBuilder html, AnalysisResult result)
    {
        html.AppendLine("        <div class=\"report-footer\">");
        html.AppendLine("            <p><strong>AndroidAdbAnalyze - Digital Forensics Analysis Tool</strong></p>");
        html.AppendLine($"            <p>Report Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} (로컬 시간)</p>");
        html.AppendLine("            <p>Version 1.0.0 | © 2025 All Rights Reserved</p>");
        html.AppendLine("        </div>");
    }

    private void AppendChartScript(StringBuilder html, IReadOnlyList<TimelineItem> items)
    {
        // 시간 범위 계산 (분석 시작 ~ 종료)
        var allTimes = items.SelectMany(i => new[] { i.StartTime, i.EndTime ?? i.StartTime }).ToList();
        var minTime = allTimes.Any() ? allTimes.Min() : DateTime.Now;
        var maxTime = allTimes.Any() ? allTimes.Max() : DateTime.Now;
        var timeRange = maxTime - minTime;
        
        // x축 1시간 단위로 고정 + 날짜 포함
        string timeUnit = "hour";  // 1시간 단위 고정
        string displayFormat = "MM/dd HH:mm";  // 날짜 포함 (월/일 시:분)
        int stepSize = 1;  // 1시간 간격
        
        // 날짜 범위 계산 (차트 설명에 사용)
        var dateRangeText = minTime.Date == maxTime.Date
            ? $"{minTime:yyyy년 M월 d일}"  // 같은 날
            : $"{minTime:yyyy년 M월 d일} ~ {maxTime:M월 d일}";  // 다른 날
        
        html.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js\"></script>");
        html.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chartjs-adapter-date-fns@3.0.0/dist/chartjs-adapter-date-fns.bundle.min.js\"></script>");
        html.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chartjs-plugin-zoom@2.0.1/dist/chartjs-plugin-zoom.min.js\"></script>");
        html.AppendLine("    <script>");
        html.AppendLine("        let timelineChart = null;");
        html.AppendLine("        const ctx = document.getElementById('timelineChart')?.getContext('2d');");
        html.AppendLine("        if (ctx) {");
        html.AppendLine("            const timelineData = {");
        html.AppendLine("                datasets: [");

        // 전송 데이터 (먼저 확인하여 크기 동적 조정)
        var transmissions = items.Where(i => i.EventType == Constants.TimelineEventTypes.TRANSMISSION).ToList();
        var hasTransmission = transmissions.Any();
        
        // UX 최적화: 적절한 크기 설정
        var barThickness = hasTransmission ? 60 : 80;  // 세션 막대 두께
        var highConfidenceRadius = 5;    // 높은 확신 촬영 점 크기 (8 → 5)
        var mediumConfidenceRadius = 4;  // 중간 확신 촬영 점 크기 (6 → 4)
        // 낮은 확신 촬영 제거 (요구사항 3)
        
        // 세션 데이터 - Session 레이어에 배치 (범례에서 제외)
        var sessions = items.Where(i => i.EventType == Constants.TimelineEventTypes.CAMERA_SESSION).ToList();
        if (sessions.Any())
        {
            html.AppendLine("                    {");
            html.AppendLine("                        type: 'bar',");
            html.AppendLine("                        label: '',"); // 범례에서 제외
            html.AppendLine("                        data: [");
            
            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var startTime = session.StartTime;
                var endTime = session.EndTime ?? session.StartTime.AddMinutes(5); // 종료 시간 없으면 5분 추정
                var isIncomplete = session.Metadata.TryGetValue("IsIncomplete", out var incomplete) && incomplete == "True";
                
                // UX 최적화: 밝고 선명한 색상 (완전 불투명)
                var opacity = "0.85";  // 약간의 투명도로 겹침 시각화
                // 파란색(완전) + 주황색(불완전) - 높은 대비
                var color = isIncomplete 
                    ? "230, 126, 34"   // 불완전: 밝은 주황색 #e67e22
                    : "52, 152, 219";  // 완전: 밝은 파란색 #3498db
                
                html.AppendLine($"                            {{");
                html.AppendLine($"                                x: [new Date('{startTime:yyyy-MM-ddTHH:mm:ss}'), new Date('{endTime:yyyy-MM-ddTHH:mm:ss}')],");
                html.AppendLine($"                                y: 'Session',");  // Timeline → Session 분리
                html.AppendLine($"                                backgroundColor: 'rgba({color}, {opacity})',");
                html.AppendLine($"                                borderColor: 'rgba({color}, 1)',");
                html.AppendLine($"                                borderWidth: 2,");
                html.AppendLine($"                                label: '{session.Label} (점수: {session.Score:F2})'");
                html.Append($"                            }}");
                if (i < sessions.Count - 1)
                    html.AppendLine(",");
                else
                    html.AppendLine();
            }
            
            html.AppendLine("                        ],");
            html.AppendLine("                        borderSkipped: false,");
            html.AppendLine($"                        barThickness: {barThickness},");
            html.AppendLine("                        barPercentage: 0.95,");  // 여백 더 축소 (0.9 → 0.95)
            html.AppendLine("                        categoryPercentage: 0.9");  // 카테고리 간 여백 더 축소 (0.8 → 0.9)
            html.AppendLine("                    },");
        }

        // 촬영 데이터 - Capture 레이어에 배치
        var captures = items.Where(i => i.EventType == Constants.TimelineEventTypes.CAMERA_CAPTURE).ToList();
        if (captures.Any())
        {
            // 점수별로 그룹화하여 다른 색상 및 크기 적용 (낮은 확신 제외)
            var highConfidence = captures.Where(c => c.Score >= 0.7).ToList();
            var mediumConfidence = captures.Where(c => c.Score >= 0.4 && c.Score < 0.7).ToList();
            // 낮은 확신 촬영 제거 (요구사항 3)

            if (highConfidence.Any())
            {
                html.AppendLine("                    {");
                html.AppendLine("                        type: 'scatter',");
                html.AppendLine("                        label: '',"); // 범례에서 제외
                html.Append("                        data: [");
                html.Append(string.Join(", ", highConfidence.Select(c => 
                    $"{{ x: new Date('{c.StartTime:yyyy-MM-ddTHH:mm:ss}'), y: 'Capture', label: '{c.Label}', score: {c.Score:F2} }}")));  // Capture 레이어 분리
                html.AppendLine("],");
                html.AppendLine("                        backgroundColor: 'rgba(231, 76, 60, 0.9)',");
                html.AppendLine("                        borderColor: 'rgba(192, 57, 43, 1)',");
                html.AppendLine("                        borderWidth: 2,");
                html.AppendLine($"                        pointRadius: {highConfidenceRadius},");  // 5px (축소)
                html.AppendLine($"                        pointHoverRadius: {highConfidenceRadius + 2}");
                html.AppendLine("                    },");
            }

            if (mediumConfidence.Any())
            {
                html.AppendLine("                    {");
                html.AppendLine("                        type: 'scatter',");
                html.AppendLine("                        label: '촬영 (중간 확신: 0.4~0.7)',");
                html.Append("                        data: [");
                html.Append(string.Join(", ", mediumConfidence.Select(c => 
                    $"{{ x: new Date('{c.StartTime:yyyy-MM-ddTHH:mm:ss}'), y: 'Capture', label: '{c.Label}', score: {c.Score:F2} }}")));  // Capture 레이어 분리
                html.AppendLine("],");
                html.AppendLine("                        backgroundColor: 'rgba(241, 196, 15, 0.85)',");
                html.AppendLine("                        borderColor: 'rgba(243, 156, 18, 1)',");
                html.AppendLine("                        borderWidth: 2,");
                html.AppendLine($"                        pointRadius: {mediumConfidenceRadius},");  // 4px (축소)
                html.AppendLine($"                        pointHoverRadius: {mediumConfidenceRadius + 2}");
                html.AppendLine("                    },");
            }

            // 낮은 확신 촬영 제거 (요구사항 3)
        }
        if (transmissions.Any())
        {
            html.AppendLine("                    {");
            html.AppendLine("                        type: 'scatter',");
            html.AppendLine("                        label: '네트워크 전송',");
            html.Append("                        data: [");
            html.Append(string.Join(", ", transmissions.Select(t => 
                $"{{ x: new Date('{t.StartTime:yyyy-MM-ddTHH:mm:ss}'), y: 'Transmission', label: '{t.Label}' }}")));
            html.AppendLine("],");
            html.AppendLine("                        backgroundColor: 'rgba(255, 159, 64, 0.8)',");
            html.AppendLine("                        borderColor: 'rgba(255, 159, 64, 1)',");
            html.AppendLine("                        borderWidth: 2,");
            html.AppendLine("                        pointRadius: 6,");  // UX 최적화: 7 → 6
            html.AppendLine("                        pointHoverRadius: 8,");
            html.AppendLine("                        pointStyle: 'triangle'");
            html.AppendLine("                    }");
        }

        html.AppendLine("                ]");
        html.AppendLine("            };");
        html.AppendLine("            timelineChart = new Chart(ctx, {");
        html.AppendLine("                type: 'bar',");
        html.AppendLine("                data: timelineData,");
        html.AppendLine("                options: {");
        html.AppendLine("                    responsive: true,");
        html.AppendLine("                    maintainAspectRatio: false,");
        html.AppendLine("                    indexAxis: 'y',");
        html.AppendLine("                    plugins: {");
        html.AppendLine("                        title: { display: false },");  // HTML에서 표시
        html.AppendLine("                        legend: { display: false },");  // HTML에서 표시
        html.AppendLine("                        tooltip: {");
        html.AppendLine("                            callbacks: {");
        html.AppendLine("                                title: function(context) {");
        html.AppendLine("                                    const item = context[0];");
        html.AppendLine("                                    if (item.dataset.type === 'bar') {");
        html.AppendLine("                                        const data = item.dataset.data[item.dataIndex];");
        html.AppendLine("                                        return data.label || item.dataset.label;");
        html.AppendLine("                                    }");
        html.AppendLine("                                    return item.dataset.label;");
        html.AppendLine("                                },");
        html.AppendLine("                                label: function(context) {");
        html.AppendLine("                                    if (context.dataset.type === 'bar') {");
        html.AppendLine("                                        const data = context.dataset.data[context.dataIndex];");
        html.AppendLine("                                        const start = new Date(data.x[0]).toLocaleString('ko-KR');");
        html.AppendLine("                                        const end = new Date(data.x[1]).toLocaleString('ko-KR');");
        html.AppendLine("                                        const duration = (new Date(data.x[1]) - new Date(data.x[0])) / 1000;");
        html.AppendLine("                                        return [`시작: ${start}`, `종료: ${end}`, `지속: ${duration.toFixed(1)}초`];");
        html.AppendLine("                                    } else {");
        html.AppendLine("                                        const data = context.dataset.data[context.dataIndex];");
        html.AppendLine("                                        const time = new Date(data.x).toLocaleString('ko-KR');");
        html.AppendLine("                                        const label = data.label || '';");
        html.AppendLine("                                        const score = data.score ? ` (점수: ${data.score})` : '';");
        html.AppendLine("                                        return `${label}${score} - ${time}`;");
        html.AppendLine("                                    }");
        html.AppendLine("                                }");
        html.AppendLine("                            }");
        html.AppendLine("                        }");
        html.AppendLine("                    },");
        html.AppendLine("                    scales: {");
        html.AppendLine("                        x: {");
        html.AppendLine("                            type: 'time',");
        html.AppendLine("                            time: {");
        html.AppendLine($"                                unit: '{timeUnit}',");
        html.AppendLine($"                                stepSize: {stepSize},");
        html.AppendLine($"                                displayFormats: {{ hour: '{displayFormat}' }},");
        html.AppendLine("                                tooltipFormat: 'yyyy년 M월 d일 HH:mm:ss'");
        html.AppendLine("                            },");
        html.AppendLine("                            grid: {");
        html.AppendLine("                                color: 'rgba(0, 0, 0, 0.2)',");   // 더 진한 그리드
        html.AppendLine("                                lineWidth: 1.5,");                 // 더 두꺼운 선
        html.AppendLine("                                drawOnChartArea: true,");
        html.AppendLine("                                drawBorder: true,");
        html.AppendLine("                                borderWidth: 2,");
        html.AppendLine("                                borderColor: '#333'");
        html.AppendLine("                            },");
        html.AppendLine("                            ticks: {");
        html.AppendLine("                                maxRotation: 45,");
        html.AppendLine("                                minRotation: 45,");  // 45도 고정 회전 (날짜 포함으로 길어짐)
        html.AppendLine("                                autoSkipPadding: 20,");
        html.AppendLine("                                font: { size: 11 },");
        html.AppendLine("                                color: '#555'");
        html.AppendLine("                            }");
        html.AppendLine("                        },");
        html.AppendLine("                        y: {");
        html.AppendLine("                            type: 'category',");
        
        // y축 레이블을 동적으로 생성 (Session, Capture, Transmission 분리)
        html.Append("                            labels: [");
        var yLabels = new List<string> { "'Session'", "'Capture'" };  // 세션과 촬영 분리
        if (transmissions.Any())
        {
            yLabels.Add("'Transmission'");
        }
        html.Append(string.Join(", ", yLabels));
        html.AppendLine("],");
        
        html.AppendLine("                            title: {");
        html.AppendLine("                                display: false");  // HTML에서 표시
        html.AppendLine("                            },");
        html.AppendLine("                            offset: true,");
        html.AppendLine("                            grid: {");
        html.AppendLine("                                color: 'rgba(0, 0, 0, 0.15)',");  // 더 진한 그리드
        html.AppendLine("                                lineWidth: 1.5,");  // 더 두꺼운 선
        html.AppendLine("                                drawBorder: true,");
        html.AppendLine("                                borderWidth: 2,");
        html.AppendLine("                                borderColor: '#333'");
        html.AppendLine("                            },");
        html.AppendLine("                            ticks: {");
        html.AppendLine("                                display: false");  // HTML에서 표시
        html.AppendLine("                            }");
        html.AppendLine("                        }");
        html.AppendLine("                    },");
        html.AppendLine("                    plugins: {");
        html.AppendLine("                        zoom: {");
        html.AppendLine("                            zoom: {");
        html.AppendLine("                                wheel: {");
        html.AppendLine("                                    enabled: true,");
        html.AppendLine("                                    modifierKey: 'ctrl'");  // Ctrl+휠로 줌
        html.AppendLine("                                },");
        html.AppendLine("                                pinch: {");
        html.AppendLine("                                    enabled: true");  // 터치 핀치 줌
        html.AppendLine("                                },");
        html.AppendLine("                                mode: 'x',");  // X축만 줌 (시간 범위)
        html.AppendLine("                                scaleMode: 'x'");  // X축 스케일만 변경
        html.AppendLine("                            },");
        html.AppendLine("                            pan: {");
        html.AppendLine("                                enabled: true,");
        html.AppendLine("                                mode: 'x',");  // X축만 팬
        html.AppendLine("                                scaleMode: 'x',");  // X축 스케일만 변경
        html.AppendLine("                                modifierKey: 'shift'");  // Shift+드래그로 팬
        html.AppendLine("                            },");
        html.AppendLine("                            limits: {");
        html.AppendLine("                                x: { minRange: 60 * 60 * 1000 },");  // 최소 1시간 범위
        html.AppendLine("                                y: { min: 0, max: 10 }");  // Y축 고정 (확대 방지)
        html.AppendLine("                            }");
        html.AppendLine("                        }");
        html.AppendLine("                    }");
        html.AppendLine("                }");
        html.AppendLine("            });");
        html.AppendLine("            ");
        html.AppendLine("            // HTML 범례 생성");
        html.AppendLine("            createHtmlLegend();");
        html.AppendLine("        }");
        html.AppendLine("        ");
        html.AppendLine("        function createHtmlLegend() {");
        html.AppendLine("            const legendContainer = document.getElementById('timelineLegendLeft');");
        html.AppendLine("            if (!legendContainer || !timelineChart) return;");
        html.AppendLine("            ");
        html.AppendLine("            let legendHtml = '';");
        html.AppendLine("            const datasets = timelineChart.data.datasets;");
        html.AppendLine("            ");
        html.AppendLine("            datasets.forEach((dataset) => {");
        html.AppendLine("                // 범례에서 제외: 빈 label인 경우 (카메라 세션, 높은 확신 촬영)");
        html.AppendLine("                if (!dataset.label || dataset.label.trim() === '') {");
        html.AppendLine("                    return;");
        html.AppendLine("                }");
        html.AppendLine("                ");
        html.AppendLine("                const isBar = dataset.type === 'bar';");
        html.AppendLine("                const bgColor = Array.isArray(dataset.backgroundColor) ");
        html.AppendLine("                    ? dataset.backgroundColor[0] ");
        html.AppendLine("                    : dataset.backgroundColor;");
        html.AppendLine("                const borderColor = Array.isArray(dataset.borderColor) ");
        html.AppendLine("                    ? dataset.borderColor[0] ");
        html.AppendLine("                    : dataset.borderColor;");
        html.AppendLine("                ");
        html.AppendLine("                const shapeClass = isBar ? 'legend-box' : 'legend-dot';");
        html.AppendLine("                const shapeStyle = `background-color: ${bgColor}; border-color: ${borderColor};`;");
        html.AppendLine("                ");
        html.AppendLine("                legendHtml += `");
        html.AppendLine("                    <div class=\"legend-item\">");
        html.AppendLine("                        <div class=\"${shapeClass}\" style=\"${shapeStyle}\"></div>");
        html.AppendLine("                        <span class=\"legend-label\">${dataset.label}</span>");
        html.AppendLine("                    </div>");
        html.AppendLine("                `;");
        html.AppendLine("            });");
        html.AppendLine("            ");
        html.AppendLine("            legendContainer.innerHTML = legendHtml;");
        html.AppendLine("        }");
        html.AppendLine("        ");
        html.AppendLine("        // 줌 초기화 함수");
        html.AppendLine("        function resetTimelineZoom() {");
        html.AppendLine("            if (timelineChart) {");
        html.AppendLine("                timelineChart.resetZoom();");
        html.AppendLine("            }");
        html.AppendLine("        }");
        html.AppendLine("    </script>");
    }

    // Helper Methods
    private static string Escape(string text) => HttpUtility.HtmlEncode(text);

    private static string FormatDateTime(DateTime dt) => dt.ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
            return "-";

        var d = duration.Value;
        if (d.TotalMinutes < 1)
            return $"{d.Seconds}초";
        if (d.TotalHours < 1)
            return $"{d.Minutes}분 {d.Seconds}초";
        return $"{(int)d.TotalHours}시간 {d.Minutes}분";
    }

    private static string GetStatusBadge(bool isIncomplete)
    {
        return isIncomplete
            ? "<span class=\"badge badge-warning\">불완전</span>"
            : "<span class=\"badge badge-success\">완료</span>";
    }

    private static string GetCaptureTypeBadge(bool isEstimated)
    {
        return isEstimated
            ? "<span class=\"badge badge-warning\">추정</span>"
            : "<span class=\"badge badge-success\">확정</span>";
    }

    private static string GetTransmissionBadge(CameraCaptureEvent capture)
    {
        if (capture.IsTransmitted)
        {
            var transmissionTime = capture.TransmissionTime?.ToString("HH:mm:ss") ?? "N/A";
            var packets = capture.TransmittedPackets ?? 0;
            return $"<span class=\"badge badge-danger\" title=\"전송 시간: {transmissionTime}, 패킷: {packets}개\">📤 전송됨</span>";
        }
        else
        {
            return "<span class=\"badge badge-secondary\">미전송</span>";
        }
    }

    private static string GetConfidenceBar(double score)
    {
        var percent = Math.Min((int)(score * 100), 100); // 최대 100%로 제한
        var cssClass = score >= 0.8 ? "confidence-high" : score >= 0.5 ? "confidence-medium" : "confidence-low";
        
        return $@"<div class=""confidence-bar-container"">
                                <div class=""confidence-bar {cssClass}"" style=""width: {percent}%;""></div>
                            </div>
                            {percent}%";
    }
}
