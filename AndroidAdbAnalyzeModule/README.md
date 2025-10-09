# Android ADB Analyze Module

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

Android ADB dumpsys 로그를 파싱하고 전처리하여 정규화된 이벤트로 변환하는 C# .NET 8 라이브러리입니다.

## 🚀 주요 기능

- ✅ **YAML 기반 설정**: 외부 설정 파일로 파싱 규칙 정의 (코드 수정 불필요)
- ✅ **7가지 로그 타입 지원**: audio, vibrator, usagestats, camera_worker, activity, media.camera, media.metrics
- ✅ **섹션 기반 파싱**: 로그 파일을 논리적 섹션으로 분할하여 파싱
- ✅ **타임스탬프 정규화**: 8가지 포맷 지원, UTC 자동 변환
- ✅ **멀티 안드로이드 버전**: 버전별 설정 파일 지원
- ✅ **에러 처리**: 상세한 예외 정보 및 통계 제공
- ✅ **InMemory Repository**: 파싱된 이벤트 저장 및 쿼리

## 📦 설치

### NuGet 패키지 (예정)
```bash
dotnet add package AndroidAdbAnalyzeModule
```

### 프로젝트 참조
```xml
<ItemGroup>
  <ProjectReference Include="..\AndroidAdbAnalyzeModule\AndroidAdbAnalyzeModule.csproj" />
</ItemGroup>
```

## 🔧 빠른 시작

### 1. 설정 파일 로드
```csharp
using AndroidAdbAnalyzeModule.Configuration.Loaders;
using AndroidAdbAnalyzeModule.Core.Models;
using AndroidAdbAnalyzeModule.Parsing;

// 설정 파일 로드
var configLoader = new YamlConfigurationLoader("configs/adb_audio_config.yaml");
var configuration = await configLoader.LoadAsync("configs/adb_audio_config.yaml");
```

### 2. 로그 파싱
```csharp
// 디바이스 정보 설정
var deviceInfo = new DeviceInfo
{
    TimeZone = "Asia/Seoul",
    CurrentTime = DateTime.Now,
    AndroidVersion = "15"
};

var options = new LogParsingOptions { DeviceInfo = deviceInfo };

// 파서 생성 및 실행
var parser = new AdbLogParser(configuration);
var result = await parser.ParseAsync("logs/audio.txt", options);

// 결과 확인
if (result.Success)
{
    Console.WriteLine($"✅ {result.Events.Count}개 이벤트 파싱됨");
    Console.WriteLine($"처리 시간: {result.Statistics.ElapsedTime.TotalMilliseconds}ms");
    Console.WriteLine($"성공률: {result.Statistics.SuccessRate:P2}");
}
```

### 3. 이벤트 조회
```csharp
using AndroidAdbAnalyzeModule.Repositories;

// Repository에 저장
var repository = new InMemoryLogEventRepository();
await repository.SaveEventsAsync(result.Events);

// 시간 범위로 조회
var events = await repository.GetEventsByTimeRangeAsync(
    DateTime.UtcNow.AddHours(-1),
    DateTime.UtcNow,
    eventType: "PLAYER_CREATED"
);
```

## 📚 문서

- [API 사용 가이드](AndroidAdbAnalyzeModule/Docs/API_Usage_Guide.md) - 상세 사용법 및 예제
- [개발 계획](AndroidAdbAnalyzeModule/Docs/DevelopmentPlan.md) - 프로젝트 개발 로드맵
- [개발 가이드라인](AndroidAdbAnalyzeModule/Docs/AI_Development_Guidelines.md) - 코딩 규칙 및 원칙
- [플러그인 아키텍처](AndroidAdbAnalyzeModule/Docs/PluginArchitecture.md) - 확장 가능한 플러그인 설계

## 🎯 책임 범위

### ✅ 이 DLL의 책임
- 로그 파일 파싱 (Section Splitting, Regex Matching)
- 데이터 전처리 (타임스탬프 정규화, 필드 변환)
- 정규화된 이벤트 생성 (`NormalizedLogEvent`)
- 에러 처리 및 통계 제공

### ❌ 상위 애플리케이션의 책임
- **상관관계 분석** (여러 이벤트 간 관계 분석)
- **이벤트 감지** (카메라 촬영, 앱 실행 등)
- **타임라인 생성** (시각화용 데이터 구조)
- **UI 표시** (테이블, 차트 등)

## 🗂️ 지원 로그 타입

| 로그 타입 | 파일명 | dumpsys 명령 |
|----------|--------|--------------|
| Audio | `audio.txt` | `dumpsys media.audio_flinger` |
| Vibrator | `vibrator_manager.txt` | `dumpsys vibrator_manager` |
| UsageStats | `usagestats.txt` | `dumpsys usagestats` |
| Camera Worker | `media.camera.worker.txt` | Camera lifecycle logs |
| Activity | `activity.txt` | `dumpsys activity` |
| Media Camera | `media.camera.txt` | Camera connect/disconnect |
| Media Metrics | `media.metrics.txt` | Media extractor/audio track |

## 🧪 테스트

```bash
cd AndroidAdbAnalyzeModule
dotnet test
```

**테스트 결과:**
- ✅ 47/47 테스트 통과
- ✅ 34개 End-to-End 테스트
- ✅ 13개 에러 케이스 테스트

## 🔧 기술 스택

- **.NET 8.0** - 타겟 프레임워크
- **YamlDotNet** - YAML 설정 파일 파싱
- **Microsoft.Extensions.Logging** - 로깅
- **xUnit** - 단위 테스트
- **FluentAssertions** - 테스트 Assertion

## 🏗️ 아키텍처

```
AndroidAdbAnalyzeModule/
├── Core/                          # 핵심 모델 및 인터페이스
│   ├── Models/                    # 데이터 모델
│   ├── Interfaces/                # 인터페이스 정의
│   └── Exceptions/                # 커스텀 예외
├── Configuration/                 # 설정 파일 관리
│   ├── Loaders/                   # YAML 로더
│   ├── Models/                    # 설정 모델
│   └── Validators/                # 설정 검증
├── Parsing/                       # 파싱 로직
│   ├── LineParsers/               # Regex 기반 라인 파서
│   └── SectionSplitters/          # 섹션 분할
├── Preprocessing/                 # 전처리
│   └── TimestampNormalizer.cs     # 타임스탬프 정규화
└── Repositories/                  # 데이터 저장소
    └── InMemoryLogEventRepository.cs
```

## 📈 성능

- **처리 속도**: 약 1-2 MB/s
- **메모리 사용**: 파일 크기의 약 2-3배
- **RegexLineParser 캐싱**: 파서 인스턴스당 패턴 미리 컴파일
- **최대 파일 크기**: 기본 500MB (설정 가능)

## 🤝 기여

프로젝트 관리자에게 문의하세요.

## 📄 라이선스

MIT License

## 📞 문의

추가 문의사항이나 버그 리포트는 프로젝트 관리자에게 문의하세요.

---

**버전**: 1.0.0  
**최종 업데이트**: 2025-10-04


