# AndroidAdbAnalyze.Analysis - Introduction

## 📖 개요

**AndroidAdbAnalyze.Analysis**는 Android ADB 로그 데이터를 기반으로 카메라 세션 및 촬영 이벤트를 포렌식적으로 분석하는 C# .NET 8 라이브러리입니다.

Parser DLL(`AndroidAdbAnalyze.Parser`)에서 파싱된 `NormalizedLogEvent` 배열을 입력받아, 상관관계 분석, 이벤트 감지, 중복 제거를 수행하고 포렌식 분석 보고서를 생성합니다.

---

## 🎯 핵심 책임

### ✅ 이 라이브러리가 제공하는 기능

1. **이벤트 중복 제거 (Event Deduplication)**
   - 여러 로그 소스에서 발생한 동일 이벤트를 통합
   - 시간 기반 및 속성 기반 유사도 계산
   - 중복 이벤트 상세 정보 추적

2. **세션 감지 (Session Detection)**
   - 카메라 사용 세션(시작~종료) 추적
   - 다중 로그 소스(`usagestats`, `media_camera`) 기반 세션 추출 및 병합
   - 불완전 세션 처리 (시작 또는 종료 누락 시 휴리스틱 추정)

3. **고수준 이벤트 감지 (Capture Detection)**
   - 카메라 촬영 이벤트 감지
   - 앱별 탐지 전략 (Strategy Pattern)
     - `BasePatternStrategy`: 기본 카메라, 무음 카메라
     - `KakaoTalkStrategy`: 카카오톡 특화
     - `TelegramStrategy`: 텔레그램 특화
   - 오탐 필터링 (스크린샷, 다운로드 패턴 제외)

4. **탐지 점수 계산 (Detection Score Calculation)**
   - 아티팩트 기반 탐지 점수 산출 (0.0 ~ 1.0)
   - 17개 이벤트 타입별 가중치 적용
   - 핵심 아티팩트 / 보조 아티팩트 구분

5. **타임라인 생성 (Timeline Building)**
   - UI 시각화를 위한 타임라인 데이터 구조 생성
   - 시간순 정렬, 자동 라벨 부여
   - 신뢰도 기반 ColorHint 제공

6. **보고서 생성 (Report Generation)**
   - HTML 형식의 포렌식 분석 보고서
   - Chart.js 기반 타임라인 차트
   - 세션/촬영 테이블, 통계, 에러/경고 섹션

---

### ❌ 이 라이브러리의 책임이 아닌 것

- **로그 파일 파싱**: `AndroidAdbAnalyze.Parser` DLL의 책임
- **UI 표시**: WPF 앱 등 상위 레이어의 책임
- **데이터베이스 저장**: 향후 별도 레이어에서 처리 예정

---

## 🏗️ 핵심 설계 원칙

### 1. 포렌식 표준 방법론 적용
- **세션 기반 접근**: 시작/종료 이벤트 페어링
- **아티팩트 기반 탐지 점수**: 직접/간접 아티팩트의 가중치 합산
- **불완전 데이터 처리**: 시작 또는 종료 누락 시 컨텍스트 기반 추정

### 2. SOLID 원칙 준수
- **단일 책임 (SRP)**: 각 클래스는 하나의 명확한 역할
- **인터페이스 분리 (ISP)**: 11개 인터페이스로 역할 분리
- **의존성 주입 (DIP)**: 생성자 주입으로 테스트 가능성 보장

### 3. 확장성 (Strategy Pattern)
- **ICaptureDetectionStrategy**: 앱별 촬영 탐지 로직 분리
- **ISessionSource**: 로그 소스별 세션 추출 로직 분리
- **IDeduplicationStrategy**: 이벤트 타입별 중복 판정 로직 분리

### 4. 불변성 (Immutability)
- 모든 데이터 모델은 `init` 키워드 사용
- `IReadOnlyList`, `IReadOnlyDictionary` 사용
- 순환 참조 방지를 위한 ID 기반 참조

### 5. YAGNI 원칙
- 지금 당장 필요하지 않은 기능은 구현하지 않음
- 단순성 우선, 복잡한 최적화는 성능 문제 발생 후 적용

---

## 📦 주요 컴포넌트

### 1. Models (9개)
- **Sessions/**: `CameraSession`, `SessionIncompleteReason`
- **Events/**: `CameraCaptureEvent`
- **Context/**: `SessionContext`, `ForegroundServiceInfo`
- **Deduplication/**: `DeduplicationInfo`
- **Options/**: `AnalysisOptions`
- **Results/**: `AnalysisResult`, `AnalysisStatistics`
- **Visualization/**: `TimelineItem`

### 2. Interfaces (11개)
- **Core**: `IAnalysisOrchestrator`, `IEventDeduplicator`, `ISessionDetector`, `ICaptureDetector`
- **Support**: `IConfidenceCalculator`, `ITimelineBuilder`, `IReportGenerator`
- **Strategy**: `ICaptureDetectionStrategy`, `IDeduplicationStrategy`, `ISessionSource`
- **Context**: `ISessionContextProvider`

### 3. Services (16개 클래스)
- **Orchestration/**: `AnalysisOrchestrator`
- **Deduplication/**: `EventDeduplicator` + 2개 전략
- **Sessions/**: `CameraSessionDetector` + 2개 소스 + `SessionContextProvider`
- **Captures/**: `CameraCaptureDetector`
- **DetectionStrategies/**: 3개 전략 (Base, KakaoTalk, Telegram)
- **Confidence/**: `ConfidenceCalculator`
- **Visualization/**: `TimelineBuilder`
- **Reports/**: `HtmlReportGenerator`, `HtmlStyles`

---

## 🚀 빠른 시작

### 설치

```bash
# NuGet을 통한 설치 (향후 제공 예정)
dotnet add package AndroidAdbAnalyze.Analysis
```

### 기본 사용 예제

```csharp
using AndroidAdbAnalyze.Analysis.Extensions;
using AndroidAdbAnalyze.Analysis.Interfaces;
using AndroidAdbAnalyze.Analysis.Models.Options;
using Microsoft.Extensions.DependencyInjection;

// 1. Dependency Injection 컨테이너 설정
var services = new ServiceCollection();
services.AddAnalysisServices();
services.AddLogging();
var provider = services.BuildServiceProvider();

// 2. AnalysisOrchestrator 인스턴스 생성
var orchestrator = provider.GetRequiredService<IAnalysisOrchestrator>();

// 3. 분석 옵션 설정
var options = new AnalysisOptions
{
    MinSessionDuration = TimeSpan.FromSeconds(1),
    MaxSessionGap = TimeSpan.FromSeconds(30),
    EventCorrelationWindow = TimeSpan.FromSeconds(30),
    MinSessionConfidence = 0.0,
    MinCaptureConfidence = 0.0
};

// 4. 분석 실행
var events = /* Parser DLL에서 파싱된 NormalizedLogEvent 배열 */;
var result = await orchestrator.AnalyzeAsync(events, options);

// 5. 결과 활용
Console.WriteLine($"세션 수: {result.Sessions.Count}");
Console.WriteLine($"촬영 수: {result.Captures.Count}");
Console.WriteLine($"처리 시간: {result.Statistics.TotalProcessingTime}");
```

자세한 사용법은 [API 사용 가이드](../03_Usage_Guides/API_Usage_Guide.md)를 참고하세요.

---

## 📚 문서 구조

- **[01_Introduction](../01_Introduction/)**: 프로젝트 개요 및 빠른 시작 (현재 문서)
- **[02_Architecture](../02_Architecture/)**: 시스템 아키텍처 및 설계 문서
  - [Architecture_Overview.md](../02_Architecture/Architecture_Overview.md)
  - [System_Architecture_Diagram.md](../02_Architecture/System_Architecture_Diagram.md)
- **[03_Usage_Guides](../03_Usage_Guides/)**: 사용 가이드 및 예제
  - [API_Usage_Guide.md](../03_Usage_Guides/API_Usage_Guide.md)
- **[04_Project_Records](../04_Project_Records/)**: 프로젝트 기록 및 보고서
  - [DevelopmentPlan.md](../04_Project_Records/DevelopmentPlan.md)
  - [Analysis_Module_Final_Report.md](../04_Project_Records/Analysis_Module_Final_Report.md)

---

## ✅ 테스트 현황

- **단위 테스트**: 100% 통과 (Analysis + Parser)
- **통합 테스트**: 100% 통과 (2/3/4/5차 샘플)
- **Ground Truth 정확도**: 100% (5개 샘플 모두)

---


