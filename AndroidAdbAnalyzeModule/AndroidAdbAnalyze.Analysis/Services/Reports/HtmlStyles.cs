namespace AndroidAdbAnalyze.Analysis.Services.Reports;

/// <summary>
/// HTML 보고서 CSS 스타일 관리
/// </summary>
internal static class HtmlStyles
{
    public const string CSS = @"
        /* ========== 전역 스타일 ========== */
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.65;
            color: #2c3e50;
            background-color: #ecf0f1;
            padding: 20px;
            letter-spacing: 0.02em;
        }
        
        .container {
            max-width: 1200px;
            margin: 0 auto;
            background-color: #ffffff;
            box-shadow: 0 0 20px rgba(0, 0, 0, 0.1);
        }
        
        /* ========== 헤더 ========== */
        .report-header {
            background: linear-gradient(135deg, #2c3e50 0%, #34495e 100%);
            color: white;
            padding: 40px;
            text-align: center;
            border-bottom: 5px solid #3498db;
        }
        
        .report-header h1 {
            font-size: 2.5em;
            margin-bottom: 10px;
            font-weight: 600;
        }
        
        .report-header .subtitle {
            font-size: 1.2em;
            opacity: 0.9;
        }
        
        /* ========== 메타데이터 섹션 ========== */
        .metadata-section {
            background-color: #f8f9fa;
            padding: 30px 40px;
            border-left: 4px solid #3498db;
        }
        
        .metadata-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-top: 15px;
        }
        
        .metadata-item {
            display: flex;
            flex-direction: column;
        }
        
        .metadata-label {
            font-weight: 600;
            color: #7f8c8d;
            font-size: 0.85em;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            margin-bottom: 5px;
        }
        
        .metadata-value {
            font-size: 1.1em;
            color: #2c3e50;
            font-weight: 500;
        }
        
        /* ========== 컨텐츠 섹션 ========== */
        .content-section {
            padding: 40px;
        }
        
        .section-title {
            font-size: 1.8em;
            color: #2c3e50;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 3px solid #3498db;
            font-weight: 600;
        }
        
        .subsection-title {
            font-size: 1.3em;
            color: #34495e;
            margin-top: 30px;
            margin-bottom: 15px;
            font-weight: 600;
        }
        
        /* ========== Executive Summary ========== */
        .executive-summary {
            background: linear-gradient(135deg, #e8f4f8 0%, #f0f8ff 100%);
            padding: 30px;
            border-radius: 8px;
            border-left: 5px solid #3498db;
            margin-bottom: 30px;
        }
        
        .summary-stats {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 20px;
            margin-top: 20px;
        }
        
        .stat-card {
            background-color: white;
            padding: 20px;
            border-radius: 8px;
            text-align: center;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
            transition: transform 0.2s;
        }
        
        .stat-card:hover {
            transform: translateY(-3px);
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        }
        
        .stat-number {
            font-size: 2.5em;
            font-weight: bold;
            color: #3498db;
            margin-bottom: 5px;
        }
        
        .stat-label {
            font-size: 0.9em;
            color: #7f8c8d;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }
        
        /* ========== 테이블 스타일 ========== */
        .data-table {
            width: 100%;
            border-collapse: collapse;
            margin: 20px 0;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
            border-radius: 8px;
            overflow: hidden;
        }
        
        .data-table thead {
            background: linear-gradient(135deg, #34495e 0%, #2c3e50 100%);
            color: white;
        }
        
        .data-table th {
            padding: 15px;
            text-align: left;
            font-weight: 600;
            text-transform: uppercase;
            font-size: 0.85em;
            letter-spacing: 0.5px;
        }
        
        .data-table td {
            padding: 14px 16px;
            border-bottom: 1px solid #ecf0f1;
        }
        
        .data-table tbody tr:hover {
            background-color: #f8f9fa;
        }
        
        .data-table tbody tr:last-child td {
            border-bottom: none;
        }
        
        /* ========== 상태 배지 ========== */
        .badge {
            display: inline-block;
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 0.85em;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }
        
        .badge-success {
            background-color: #d4edda;
            color: #155724;
        }
        
        .badge-warning {
            background-color: #fff3cd;
            color: #856404;
        }
        
        .badge-danger {
            background-color: #f8d7da;
            color: #721c24;
        }
        
        .badge-info {
            background-color: #d1ecf1;
            color: #0c5460;
        }
        
        /* ========== 신뢰도 바 ========== */
        .confidence-bar-container {
            width: 100px;
            height: 20px;
            background-color: #ecf0f1;
            border-radius: 10px;
            overflow: hidden;
            display: inline-block;
            vertical-align: middle;
        }
        
        .confidence-bar {
            height: 100%;
            transition: width 0.3s;
        }
        
        .confidence-high {
            background: linear-gradient(90deg, #27ae60 0%, #2ecc71 100%);
        }
        
        .confidence-medium {
            background: linear-gradient(90deg, #f39c12 0%, #f1c40f 100%);
        }
        
        .confidence-low {
            background: linear-gradient(90deg, #e74c3c 0%, #c0392b 100%);
        }
        
        /* ========== 타임라인 섹션 ========== */
        .timeline-container {
            margin: 30px 0;
            position: relative;
        }
        
        .chart-controls {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
            padding: 0 20px;
            max-width: 1400px;
            margin-left: auto;
            margin-right: auto;
        }
        
        .btn-reset-zoom {
            background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            font-size: 0.95em;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
            box-shadow: 0 2px 5px rgba(52, 152, 219, 0.3);
        }
        
        .btn-reset-zoom:hover {
            background: linear-gradient(135deg, #2980b9 0%, #21618c 100%);
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(52, 152, 219, 0.4);
        }
        
        .btn-reset-zoom:active {
            transform: translateY(0);
            box-shadow: 0 2px 4px rgba(52, 152, 219, 0.3);
        }
        
        .zoom-hint {
            font-size: 0.85em;
            color: #7f8c8d;
            font-style: italic;
        }
        
        /* 타임라인 헤더 (스크롤 밖) */
        .timeline-header {
            text-align: center;
            margin: 20px 0;
            padding: 15px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 8px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }
        
        .timeline-title {
            font-size: 18px;
            font-weight: bold;
            color: #ffffff;
            margin: 0 0 8px 0;
        }
        
        .timeline-date {
            font-size: 15px;
            color: #f0f0f0;
            margin: 0;
        }
        
        /* 타임라인 범례 컨테이너 (스크롤 밖) */
        .timeline-legend-container {
            display: flex;
            flex-wrap: wrap;
            justify-content: center;
            gap: 20px;
            margin: 15px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 8px;
            border: 1px solid #dee2e6;
        }
        
        .legend-item {
            display: flex;
            align-items: center;
            gap: 8px;
            font-size: 14px;
            font-weight: 500;
            color: #2c3e50;
        }
        
        .legend-box {
            width: 20px;
            height: 14px;
            border-radius: 3px;
            border: 2px solid;
        }
        
        .legend-dot {
            width: 10px;
            height: 10px;
            border-radius: 50%;
            border: 2px solid;
        }
        
        .scroll-hint {
            color: #7f8c8d;
            font-size: 14px;
            font-style: italic;
        }
        
        /* 차트 메인 래퍼 (Flexbox) */
        .chart-main-wrapper {
            display: flex;
            width: 100%;
            margin: 20px 0;
            border: 2px solid #dee2e6;
            border-radius: 8px;
            background-color: #ffffff;
            overflow: hidden;
        }
        
        /* Y축 + 범례 고정 영역 (왼쪽 150px) */
        .timeline-y-axis-fixed {
            width: 150px;
            min-width: 150px;
            background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
            border-right: 3px solid #dee2e6;
            padding: 15px 10px;
            display: flex;
            flex-direction: column;
            justify-content: space-between;
        }
        
        /* Y축 제목 */
        .y-axis-title {
            font-size: 13px;
            font-weight: bold;
            color: #2c3e50;
            text-align: center;
            margin-bottom: 15px;
            padding-bottom: 10px;
            border-bottom: 2px solid #dee2e6;
        }
        
        /* Y축 레이블 영역 */
        .y-axis-labels {
            flex: 1;
            display: flex;
            flex-direction: column;
            justify-content: space-around;
            padding: 20px 0;
        }
        
        /* 개별 Y축 레이블 */
        .y-label-item {
            font-size: 12px;
            font-weight: 600;
            color: #34495e;
            text-align: right;
            padding: 8px 10px 8px 5px;
            border-right: 3px solid #3498db;
            border-radius: 4px 0 0 4px;
        }
        
        /* 왼쪽 범례 영역 */
        .timeline-legend-left {
            margin-top: 15px;
            padding-top: 15px;
            border-top: 2px solid #dee2e6;
        }
        
        .timeline-legend-left .legend-item {
            display: flex;
            align-items: center;
            gap: 6px;
            font-size: 11px;
            font-weight: 500;
            color: #2c3e50;
            margin-bottom: 8px;
        }
        
        .timeline-legend-left .legend-box {
            width: 16px;
            height: 10px;
            border-radius: 2px;
            border: 2px solid;
            flex-shrink: 0;
        }
        
        .timeline-legend-left .legend-dot {
            width: 8px;
            height: 8px;
            border-radius: 50%;
            border: 2px solid;
            flex-shrink: 0;
        }
        
        .timeline-legend-left .legend-label {
            line-height: 1.2;
        }
        
        /* 차트 스크롤 영역 (오른쪽) */
        .chart-scroll-area {
            flex: 1;
            overflow-x: auto;
            overflow-y: hidden;
            position: relative;
        }
        
        /* 차트 컨테이너 (고정 크기) */
        .chart-container-fixed {
            width: 3000px;
            height: 400px;
            position: relative;
        }
        
        /* ========== 알림 박스 ========== */
        .alert {
            padding: 15px 20px;
            border-radius: 8px;
            margin: 15px 0;
            border-left: 5px solid;
        }
        
        .alert-warning {
            background-color: #fff3cd;
            border-color: #f39c12;
            color: #856404;
        }
        
        .alert-error {
            background-color: #f8d7da;
            border-color: #e74c3c;
            color: #721c24;
        }
        
        .alert-info {
            background-color: #d1ecf1;
            border-color: #3498db;
            color: #0c5460;
        }
        
        /* ========== 푸터 ========== */
        .report-footer {
            background-color: #2c3e50;
            color: white;
            padding: 30px 40px;
            text-align: center;
            font-size: 0.9em;
        }
        
        .report-footer p {
            margin: 5px 0;
            opacity: 0.8;
        }
        
        /* ========== 인쇄 스타일 ========== */
        @media print {
            body {
                background-color: white;
                padding: 0;
            }
            
            .container {
                box-shadow: none;
            }
            
            .stat-card {
                break-inside: avoid;
            }
            
            .data-table {
                page-break-inside: avoid;
            }
        }";
}
