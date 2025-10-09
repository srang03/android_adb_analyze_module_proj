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
        
        .chart-container {
            position: relative;
            width: 100%;
            height: 400px;
            margin: 20px 0;
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
