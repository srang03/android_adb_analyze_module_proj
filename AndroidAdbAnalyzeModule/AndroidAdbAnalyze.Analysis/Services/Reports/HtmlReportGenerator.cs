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
            ? result.CaptureEvents.Average(c => c.ConfidenceScore) * 100
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
            html.AppendLine($"                        <td>{GetConfidenceBar(session.ConfidenceScore)}</td>");
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
            html.AppendLine($"                        <td>{GetConfidenceBar(capture.ConfidenceScore)}</td>");
            html.AppendLine("                    </tr>");
        }

        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");
        html.AppendLine("        </div>");
    }

    private void AppendTimelineChart(StringBuilder html, IReadOnlyList<TimelineItem> items)
    {
        html.AppendLine("        <div class=\"content-section\">");
        html.AppendLine("            <h2 class=\"section-title\">⏱️ 타임라인 분석</h2>");
        html.AppendLine("            <p>시간순으로 정렬된 카메라 세션 및 촬영 이벤트를 시각화합니다.</p>");
        html.AppendLine("            <div class=\"chart-container\">");
        html.AppendLine("                <canvas id=\"timelineChart\"></canvas>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
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
        AppendStatRow(html, "처리 소요 시간", $"{stats.ProcessingTime.TotalSeconds:F3} 초");
        
        if (stats.ProcessingTime.TotalSeconds > 0)
        {
            var eventsPerSecond = stats.TotalSourceEvents / stats.ProcessingTime.TotalSeconds;
            AppendStatRow(html, "평균 처리 속도", $"{eventsPerSecond:N0} 이벤트/초");
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
        html.AppendLine("                <li>이벤트 중복 제거: Jaccard 유사도 알고리즘 (임계값: 0.85)</li>");
        html.AppendLine("                <li>세션 감지: CAMERA_CONNECT/CAMERA_DISCONNECT 이벤트 쌍 매칭</li>");
        html.AppendLine("                <li>촬영 감지: DATABASE_INSERT, MEDIA_INSERT_END, SHUTTER_SOUND 등의 증거 기반 탐지</li>");
        html.AppendLine("                <li>신뢰도 계산: 증거 이벤트 타입별 가중치 합산 방식</li>");
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
        html.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js\"></script>");
        html.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chartjs-adapter-date-fns@3.0.0/dist/chartjs-adapter-date-fns.bundle.min.js\"></script>");
        html.AppendLine("    <script>");
        html.AppendLine("        const ctx = document.getElementById('timelineChart')?.getContext('2d');");
        html.AppendLine("        if (ctx) {");
        html.AppendLine("            const timelineData = {");
        html.AppendLine("                datasets: [");

        // 세션 데이터
        var sessions = items.Where(i => i.EventType == Constants.TimelineEventTypes.CAMERA_SESSION).ToList();
        if (sessions.Any())
        {
            html.AppendLine("                    {");
            html.AppendLine("                        label: '카메라 세션',");
            html.Append("                        data: [");
            html.Append(string.Join(", ", sessions.Select(s => 
                $"{{ x: new Date('{s.StartTime:yyyy-MM-ddTHH:mm:ss}'), y: 1 }}")));
            html.AppendLine("],");
            html.AppendLine("                        backgroundColor: 'rgba(52, 152, 219, 0.7)',");
            html.AppendLine("                        borderColor: 'rgba(52, 152, 219, 1)',");
            html.AppendLine("                        borderWidth: 2,");
            html.AppendLine("                        pointRadius: 8,");
            html.AppendLine("                        pointHoverRadius: 10");
            html.AppendLine("                    },");
        }

        // 촬영 데이터
        var captures = items.Where(i => i.EventType == Constants.TimelineEventTypes.CAMERA_CAPTURE).ToList();
        if (captures.Any())
        {
            html.AppendLine("                    {");
            html.AppendLine("                        label: '촬영 이벤트',");
            html.Append("                        data: [");
            html.Append(string.Join(", ", captures.Select(c => 
                $"{{ x: new Date('{c.StartTime:yyyy-MM-ddTHH:mm:ss}'), y: 0 }}")));
            html.AppendLine("],");
            html.AppendLine("                        backgroundColor: 'rgba(231, 76, 60, 0.7)',");
            html.AppendLine("                        borderColor: 'rgba(231, 76, 60, 1)',");
            html.AppendLine("                        borderWidth: 2,");
            html.AppendLine("                        pointRadius: 6,");
            html.AppendLine("                        pointHoverRadius: 8");
            html.AppendLine("                    }");
        }

        html.AppendLine("                ]");
        html.AppendLine("            };");
        html.AppendLine("            new Chart(ctx, {");
        html.AppendLine("                type: 'scatter',");
        html.AppendLine("                data: timelineData,");
        html.AppendLine("                options: {");
        html.AppendLine("                    responsive: true,");
        html.AppendLine("                    maintainAspectRatio: true,");
        html.AppendLine("                    plugins: {");
        html.AppendLine("                        title: {");
        html.AppendLine("                            display: true,");
        html.AppendLine("                            text: '시간순 이벤트 타임라인',");
        html.AppendLine("                            font: { size: 16, weight: 'bold' },");
        html.AppendLine("                            color: '#2c3e50'");
        html.AppendLine("                        },");
        html.AppendLine("                        legend: { display: true, position: 'top' },");
        html.AppendLine("                        tooltip: {");
        html.AppendLine("                            callbacks: {");
        html.AppendLine("                                label: function(context) {");
        html.AppendLine("                                    let label = context.dataset.label || '';");
        html.AppendLine("                                    if (label) label += ': ';");
        html.AppendLine("                                    label += new Date(context.parsed.x).toLocaleString('ko-KR');");
        html.AppendLine("                                    return label;");
        html.AppendLine("                                }");
        html.AppendLine("                            }");
        html.AppendLine("                        }");
        html.AppendLine("                    },");
        html.AppendLine("                    scales: {");
        html.AppendLine("                        x: {");
        html.AppendLine("                            type: 'time',");
        html.AppendLine("                            time: { unit: 'minute', displayFormats: { minute: 'HH:mm' } },");
        html.AppendLine("                            title: { display: true, text: '시간 (로컬 시간)', font: { size: 14, weight: 'bold' }, color: '#2c3e50' },");
        html.AppendLine("                            grid: { color: 'rgba(0, 0, 0, 0.05)' }");
        html.AppendLine("                        },");
        html.AppendLine("                        y: {");
        html.AppendLine("                            title: { display: true, text: '이벤트 타입', font: { size: 14, weight: 'bold' }, color: '#2c3e50' },");
        html.AppendLine("                            min: -0.5,");
        html.AppendLine("                            max: 1.5,");
        html.AppendLine("                            ticks: { ");
        html.AppendLine("                                stepSize: 1,");
        html.AppendLine("                                callback: function(value) { ");
        html.AppendLine("                                    if (value === 0) return '촬영';");
        html.AppendLine("                                    if (value === 1) return '세션';");
        html.AppendLine("                                    return '';");
        html.AppendLine("                                }");
        html.AppendLine("                            },");
        html.AppendLine("                            grid: { color: 'rgba(0, 0, 0, 0.05)' }");
        html.AppendLine("                        }");
        html.AppendLine("                    }");
        html.AppendLine("                }");
        html.AppendLine("            });");
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

    private static string GetConfidenceBar(double score)
    {
        var percent = (int)(score * 100);
        var cssClass = score >= 0.8 ? "confidence-high" : score >= 0.5 ? "confidence-medium" : "confidence-low";
        
        return $@"<div class=""confidence-bar-container"">
                                <div class=""confidence-bar {cssClass}"" style=""width: {percent}%;""></div>
                            </div>
                            {percent}%";
    }
}
