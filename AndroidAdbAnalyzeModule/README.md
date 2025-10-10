# Android ADB Analyze Solution

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Android ADB dumpsys 로그를 분석하여 포렌식 증거를 추출하는 .NET 8 솔루션입니다. 솔루션은 두 개의 주요 프로젝트로 구성됩니다:

-   **AndroidAdbAnalyze.Parser**: 로그 파싱 및 정규화 담당 라이브러리.
-   **AndroidAdbAnalyze.Analysis**: 파싱된 데이터를 분석하여 카메라 세션, 촬영 이벤트 등을 감지하는 분석 라이브러리.

## 🚀 주요 기능

-   ✅ **YAML 기반 설정**: 외부 설정 파일로 파싱 규칙 정의 (코드 수정 불필요)
-   ✅ **7가지 로그 타입 지원**: `audio`, `vibrator`, `usagestats`, `camera_worker`, `activity`, `media.camera`, `media.metrics`
-   ✅ **이벤트 분석**: 카메라 세션 시작/종료, 사진/동영상 촬영 이벤트 감지
-   ✅ **타임스탬프 정규화**: 8가지 포맷 지원, UTC 자동 변환
-   ✅ **HTML 보고서 생성**: 분석 결과를 시각화한 HTML 보고서 생성
-   ✅ **InMemory Repository**: 파싱된 이벤트 저장 및 쿼리

## 📦 설치

### NuGet 패키지 (예정)
```bash
dotnet add package AndroidAdbAnalyze.Parser
dotnet add package AndroidAdbAnalyze.Analysis
```

### 프로젝트 참조
```xml
<ItemGroup>
  <ProjectReference Include="..\AndroidAdbAnalyze.Parser\AndroidAdbAnalyze.Parser.csproj" />
  <ProjectReference Include="..\AndroidAdbAnalyze.Analysis\AndroidAdbAnalyze.Analysis.csproj" />
</ItemGroup>
```

## 🔧 빠른 시작

### 1. 설정 및 로그 파일 준비
```
/solution_root
├── configs/
│   └── adb_audio_config.yaml
└── logs/
    └── audio.txt
```

### 2. 로그 파싱 (`Parser` DLL 사용)
```csharp
using AndroidAdbAnalyze.Parser.Configuration.Loaders;
using AndroidAdbAnalyze.Parser.Core.Models;
using AndroidAdbAnalyze.Parser.Parsing;

// 1. 설정 파일 로드
var configLoader = new YamlConfigurationLoader();
var configuration = await configLoader.LoadAsync("configs/adb_audio_config.yaml");

// 2. 디바이스 정보 설정
var deviceInfo = new DeviceInfo
{
    TimeZone = "Asia/Seoul",
    CurrentTime = DateTime.Now,
    AndroidVersion = "15"
};
var options = new LogParsingOptions { DeviceInfo = deviceInfo };

// 3. 파서 생성 및 실행
var parser = new AdbLogParser(configuration);
var result = await parser.ParseAsync("logs/audio.txt", options);

// 4. 결과 확인
if (result.Success)
{
    Console.WriteLine($"✅ {result.Events.Count}개 이벤트 파싱됨");
}
```

### 3. 이벤트 분석 (`Analysis` DLL 사용)
```csharp
using AndroidAdbAnalyze.Analysis.Services.Orchestration;
using AndroidAdbAnalyze.Analysis.Models.Options;

// 1. 분석 오케스트레이터 생성
var orchestrator = new AnalysisOrchestrator();

// 2. 분석 실행
var analysisOptions = new AnalysisOptions();
var analysisResult = await orchestrator.AnalyzeAsync(result.Events, analysisOptions);

// 3. 결과 확인
Console.WriteLine($"- 감지된 세션: {analysisResult.Sessions.Count}개");
Console.WriteLine($"- 감지된 촬영: {analysisResult.Captures.Count}개");
```

## 📚 문서

-   **Parser 프로젝트 문서**
    -   [API 사용 가이드](./AndroidAdbAnalyzeModule/AndroidAdbAnalyze.Parser/Docs/03_Usage_Guides/API_Usage_Guide.md)
    -   [설정 가이드](./AndroidAdbAnalyzeModule/AndroidAdbAnalyze.Parser/Docs/03_Usage_Guides/Configuration_Guide.md)
    -   [아키텍처](./AndroidAdbAnalyzeModule/AndroidAdbAnalyze.Parser/Docs/02_Architecture/Architecture.md)
-   **Analysis 프로젝트 문서**
    -   [API 사용 가이드](./AndroidAdbAnalyzeModule/AndroidAdbAnalyze.Analysis/Docs/API_Usage_Guide.md)
    -   [아키텍처](./AndroidAdbAnalyzeModule/AndroidAdbAnalyze.Analysis/Docs/Architecture_Overview.md)
-   **개발 가이드**
    -   [AI 개발 가이드라인](./Doc/Contribution_Guide/AI_Development_Guidelines.md)
    -   [AI 문서화 가이드라인](./Doc/Contribution_Guide/AI_Documentation_Guidelines.md)

## 🎯 책임 범위

### ✅ `Parser` DLL의 책임
-   로그 파일 파싱 (Section Splitting, Regex Matching)
-   데이터 전처리 (타임스탬프 정규화, 필드 변환)
-   정규화된 이벤트 생성 (`NormalizedLogEvent`)

### ✅ `Analysis` DLL의 책임
-   **상관관계 분석** (여러 이벤트 간 관계 분석)
-   **이벤트 감지** (카메라 촬영, 앱 실행 등)
-   **타임라인 생성** 및 보고서 데이터 구성

## 🧪 테스트

```bash
cd AndroidAdbAnalyzeModule
dotnet test
```

**테스트 결과:**
-   Parser: 모든 단위/통합 테스트 통과
-   Analysis: 모든 단위/통합 테스트 통과

## 🔧 기술 스택

-   **.NET 8.0** - 타겟 프레임워크
-   **YamlDotNet** - YAML 설정 파일 파싱
-   **xUnit** - 단위 테스트
-   **FluentAssertions** - 테스트 Assertion

## 🏗️ 솔루션 아키텍처

```
AndroidAdbAnalyzeModule/
├── AndroidAdbAnalyze.Parser/      # 로그 파싱 및 정규화
│   ├── Core/
│   ├── Configuration/
│   └── Parsing/
├── AndroidAdbAnalyze.Analysis/    # 이벤트 분석 및 보고
│   ├── Interfaces/
│   ├── Models/
│   └── Services/
└── AndroidAdbAnalyze.sln
```

---

**버전**: 1.1.0
**최종 업데이트**: 2025-10-10


