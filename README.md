# Android ADB Analyze Module

Android ADB dumpsys 로그를 분석하여 카메라 사용 및 촬영 행위에 대한 포렌식 증거를 추출하는 .NET 8 기반 분석 모듈입니다.

## 개요

본 프로젝트는 Android 디바이스의 ADB(Android Debug Bridge) 로그를 수집하고 분석하여 카메라 세션, 촬영 이벤트, 앱별 카메라 사용 패턴 등을 체계적으로 탐지합니다. 계층적 아티팩트 분류 체계를 통해 카메라 사용과 실제 촬영을 명확히 구분하며, 7가지 로그 소스를 통합 분석하여 높은 정확도의 포렌식 증거를 제공합니다.

## 주요 기능

### 로그 파싱 및 정규화
- 7가지 로그 타입 지원: audio, vibrator, usagestats, camera_worker, activity, media.camera, media.metrics
- YAML 기반 파싱 규칙 정의로 코드 수정 없이 설정 변경 가능
- 8가지 타임스탬프 포맷 자동 감지 및 UTC 변환
- 정규화된 이벤트 구조로 일관된 데이터 처리

### 이벤트 분석 및 탐지
- 카메라 세션 시작/종료 자동 감지
- 사진/동영상 촬영 이벤트 식별
- 앱별 카메라 사용 패턴 분석
- 계층적 아티팩트 기반 신뢰도 산출
- 중복 이벤트 제거 및 상관관계 분석

### 보고서 생성
- HTML 기반 시각화 보고서 자동 생성
- 타임라인 기반 이벤트 전개도
- 세션별 상세 정보 및 통계
- JSON 형식 구조화 데이터 출력

## 시스템 요구사항

- .NET 8.0 Runtime 이상
- Android Platform Tools (ADB 포함)
- Windows, macOS, Linux 지원

## 설치 방법

### 1. 저장소 클론
```bash
git clone https://github.com/srang03/android_adb_analyze_module_proj.git
cd android_adb_analyze_module_proj
```

### 2. 빌드
```bash
cd AndroidAdbAnalyzeModule
dotnet build
```

### 3. 테스트 실행
```bash
dotnet test
```

## 사용 방법

### CLI 도구 실행

#### 기본 분석
```bash
cd AndroidAdbAnalyzeModule/AndroidAdbAnalyze.Console.Executor
dotnet run -- analyze
```

#### 고급 옵션
```bash
# 출력 디렉토리 지정
dotnet run -- analyze --output-dir ./my-analysis

# 시간 범위 필터링
dotnet run -- analyze \
  --start-time 2025-10-18T10:00:00 \
  --end-time 2025-10-18T12:00:00

# HTML 보고서 생성 제외
dotnet run -- analyze --no-html-report
```

### 라이브러리로 사용

#### 파싱 단계
```csharp
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;

var configLoader = new YamlConfigurationLoader();
var configuration = await configLoader.LoadAsync("configs/adb_audio_config.yaml");

var deviceInfo = new DeviceInfo
{
    TimeZone = "Asia/Seoul",
    CurrentTime = DateTime.Now,
    AndroidVersion = "15"
};

var parser = new AdbLogParser(configuration);
var result = await parser.ParseAsync("logs/audio.txt", 
    new LogParsingOptions { DeviceInfo = deviceInfo });
```

#### 분석 단계
```csharp
using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Interfaces;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAnalysisServices();
services.AddLogging();
var provider = services.BuildServiceProvider();

var orchestrator = provider.GetRequiredService<IAnalysisOrchestrator>();
var analysisResult = await orchestrator.AnalyzeAsync(
    result.Events, 
    new AnalysisOptions()
);

Console.WriteLine($"감지된 세션: {analysisResult.Sessions.Count}");
Console.WriteLine($"감지된 촬영: {analysisResult.Captures.Count}");
```

## 프로젝트 구조

```
AndroidAdbAnalyzeModule/
├── AndroidAdbAnalyze.Parser/              # 로그 파싱 라이브러리
│   ├── Core/                              # 핵심 파싱 로직
│   ├── Configuration/                     # YAML 설정 로더
│   ├── Parsing/                           # 파서 구현
│   └── Preprocessing/                     # 전처리 로직
├── AndroidAdbAnalyze.Parser.Tests/        # 파서 테스트
├── AndroidAdbAnalyze.Analysis/            # 분석 라이브러리
│   ├── Services/
│   │   ├── Sessions/                      # 세션 탐지
│   │   ├── Captures/                      # 촬영 탐지
│   │   ├── DetectionStrategies/           # 앱별 탐지 전략
│   │   ├── Confidence/                    # 신뢰도 계산
│   │   ├── Deduplication/                 # 중복 제거
│   │   └── Reports/                       # 보고서 생성
│   ├── Models/                            # 데이터 모델
│   └── Interfaces/                        # 인터페이스 정의
├── AndroidAdbAnalyze.Analysis.Tests/      # 분석 테스트
├── AndroidAdbAnalyze.Console.Executor/    # CLI 실행 도구
└── AndroidAdbAnalyze.Console.Executor.Tests/
```

## 지원 로그 타입

| 로그 타입 | dumpsys 서비스 | 필수 여부 | 용도 |
|----------|---------------|---------|------|
| activity | activity | 필수 | 앱 활동 추적 |
| audio | audio | 필수 | 오디오 이벤트 탐지 |
| media.camera | media.camera | 필수 | 카메라 API 사용 |
| media.camera.worker | media.camera.worker | 선택 | Samsung DB INSERT |
| media.metrics | media.metrics | 필수 | 미디어 메트릭 |
| usagestats | usagestats | 필수 | 앱 사용 통계 |
| vibrator_manager | vibrator_manager | 필수 | 진동 이벤트 |

## 출력 결과

### 디렉토리 구조
```
./logs/
├── device_info.json                # 디바이스 정보
├── collection_summary.txt          # 로그 수집 요약
├── raw_logs/                       # 원본 로그
│   ├── activity.log
│   ├── audio.log
│   └── ...
├── analysis_result.json            # 분석 결과 (JSON)
├── report.html                     # HTML 보고서
└── errors.log                      # 오류 로그
```

### 분석 결과 JSON 구조
```json
{
  "deviceInfo": {
    "deviceId": "...",
    "timeZone": "Asia/Seoul"
  },
  "sessions": [
    {
      "sessionId": "...",
      "packageName": "com.example.app",
      "startTime": "2025-10-18T10:00:00Z",
      "endTime": "2025-10-18T10:05:00Z",
      "captures": [...]
    }
  ],
  "statistics": {
    "totalSessions": 5,
    "totalCaptures": 12
  }
}
```

## 설정 파일

### appsettings.json
```json
{
  "Adb": {
    "ExecutablePath": null,
    "CommandTimeout": 60,
    "RetryCount": 3
  },
  "Analysis": {
    "MinConfidenceThreshold": 0.3,
    "EventCorrelationWindowSeconds": 30,
    "MaxSessionGapMinutes": 5
  }
}
```

### 아티팩트 탐지 설정 (YAML)
```yaml
artifact_patterns:
  - name: "CameraOpen"
    category: "PRIMARY"
    weight: 10
    patterns:
      - regex: "Camera device opened"
        confidence: 0.9
```

## 개발 및 기여

### 테스트 실행
```bash
# 전체 테스트
dotnet test

# 특정 프로젝트
dotnet test AndroidAdbAnalyze.Parser.Tests
dotnet test AndroidAdbAnalyze.Analysis.Tests
```

### 빌드 설정
- Target Framework: .NET 8.0
- Nullable: Enabled
- ImplicitUsings: Enabled

## 기술 스택

- .NET 8.0
- YamlDotNet 16.3.0
- xUnit (테스트 프레임워크)
- FluentAssertions (테스트 어설션)
- Microsoft.Extensions.Logging

## 라이선스

본 프로젝트는 연구 목적으로 개발되었습니다.

## 문의

프로젝트 관련 문의사항이나 이슈는 GitHub Issues를 통해 제출해 주시기 바랍니다.

## 버전 정보

- 현재 버전: 1.0.0
- 최종 업데이트: 2025-11-04

