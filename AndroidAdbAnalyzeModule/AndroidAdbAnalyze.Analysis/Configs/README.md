# 아티팩트 탐지 설정 가이드

## 📋 개요

이 폴더에는 아티팩트 기반 촬영 탐지 설정 파일이 포함되어 있습니다.
YAML 파일을 통해 **빌드 없이** 런타임에 탐지 로직을 조정할 수 있습니다.

---

## 📁 파일 구조

```
Configs/
├── artifact-detection-config.example.yaml  # 예시 파일 (참고용)
└── artifact-detection-config.yaml          # 실제 설정 파일 (선택 사항)
```

---

## 🚀 사용 방법

### 1️⃣ 기본 동작 (YAML 없이)

설정 파일이 없어도 정상 동작합니다. 코드 내부의 기본값을 사용합니다.

```csharp
// 자동으로 ConfigurationProvider.GetDefault() 사용
var calculator = new ConfidenceCalculator(logger);
var strategy = new BasePatternStrategy(logger, calculator);
```

### 2️⃣ YAML 파일 사용

#### Step 1: YAML 파일 생성

```bash
# 예시 파일을 복사하여 실제 설정 파일 생성
cp artifact-detection-config.example.yaml artifact-detection-config.yaml
```

#### Step 2: 설정 수정

`artifact-detection-config.yaml` 파일을 열어 원하는 값으로 수정합니다.

예시:
```yaml
artifactWeights:
  capture:
    DATABASE_INSERT: 0.6  # 기존 0.5 → 0.6으로 증가
    VIBRATION_EVENT: 0.5  # 기존 0.4 → 0.5로 증가

validation:
  hapticTypeCameraShutter: 50061
```

#### Step 3: 코드에서 로드

```csharp
using AndroidAdbAnalyze.Analysis.Configuration;

// YAML 파일에서 설정 로드
var config = YamlConfigurationLoader.LoadFromFile(
    "Configs/artifact-detection-config.yaml",
    logger);

// Configuration을 주입하여 객체 생성
var calculator = new ConfidenceCalculator(logger, config);
var strategy = new BasePatternStrategy(logger, calculator, config);
```

#### Optional: Try 패턴

```csharp
var (success, config) = YamlConfigurationLoader.TryLoadFromFile(
    "Configs/artifact-detection-config.yaml",
    logger);

if (success)
{
    Console.WriteLine("✅ YAML 설정 로드 성공");
}
else
{
    Console.WriteLine("⚠️ YAML 로드 실패, 기본값 사용");
}
```

---

## ✅ 안전성 보장

### Fallback 전략

YAML 파일 로드 실패 시 자동으로 기본값으로 fallback합니다:

1. **파일이 없음** → 경고 로그 + 기본값 반환
2. **파싱 오류** → 에러 로그 + 기본값 반환
3. **유효성 검증 실패** → 경고 로그 + 그대로 사용

### Backward Compatibility

기존 코드는 **전혀 수정하지 않아도** 정상 동작합니다:

```csharp
// 기존 코드 (변경 없음)
var calculator = new ConfidenceCalculator(logger);
var strategy = new BasePatternStrategy(logger, calculator);

// ✅ 정상 동작 (기본값 사용)
```

---

## 📊 설정 항목 설명

### 1. 아티팩트 가중치 (artifactWeights)

| 카테고리 | 설명 | 항목 수 |
|---------|------|---------|
| session | 세션 완전성 점수 계산용 | 5개 |
| capture | 촬영 탐지 점수 계산용 | 14개 |

**범위**: 0.0 ~ 1.0

### 2. 전략별 아티팩트 분류 (strategies)

| 전략 | 패키지 패턴 | 설명 |
|------|------------|------|
| base_pattern | null | fallback 전략 (모든 앱) |
| telegram | org.telegram.messenger | Telegram 전용 |
| kakao_talk | com.kakao.talk | KakaoTalk 전용 |

각 전략마다:
- **keyArtifacts**: 촬영 100% 확정
- **conditionalKeyArtifacts**: 조건부 확정
- **supportingArtifacts**: 보조 증거

### 3. 검증 상수 (validation)

| 항목 | 기본값 | 설명 |
|------|--------|------|
| hapticTypeCameraShutter | 50061 | 촬영 버튼 햅틱 타입 |
| playerEventStateStarted | "started" | PLAYER_EVENT 상태 |
| playerTagCamera | "CAMERA" | PLAYER_CREATED 태그 |
| serviceClassPostProcess | "PostProcessService" | Foreground Service 클래스명 |

### 4. 분석 옵션 (analysisOptions)

| 카테고리 | 항목 | 기본값 | 설명 |
|---------|------|--------|------|
| thresholds | minConfidence | 0.3 | 최소 신뢰도 (30%) |
| thresholds | deduplicationSimilarity | 0.8 | 중복 제거 유사도 (80%) |
| timeWindows | maxSessionGapMinutes | 5 | 세션 간 최대 간격 |
| timeWindows | eventCorrelationSeconds | 30 | 이벤트 상관관계 윈도우 |
| timeWindows | captureDeduplicationSeconds | 1 | 촬영 중복 제거 윈도우 |

---

## ⚠️ 주의사항

### 1. 이벤트 타입 이름

YAML 파일의 이벤트 타입 이름은 `LogEventTypes` 클래스의 상수와 **정확히 일치**해야 합니다.

**올바른 예시**:
```yaml
capture:
  DATABASE_INSERT: 0.5  # ✅ 정확
  VIBRATION_EVENT: 0.4  # ✅ 정확
```

**잘못된 예시**:
```yaml
capture:
  database_insert: 0.5  # ❌ 대소문자 오류
  VibrationEvent: 0.4   # ❌ 네이밍 오류
```

### 2. 가중치 범위

가중치는 반드시 `0.0 ~ 1.0` 범위여야 합니다.

```yaml
capture:
  DATABASE_INSERT: 0.5   # ✅ OK
  VIBRATION_EVENT: 1.5   # ❌ 범위 초과 (경고 로그)
  PLAYER_EVENT: -0.1     # ❌ 음수 (경고 로그)
```

### 3. 백업 유지

설정 변경 전 항상 백업을 유지하세요:

```bash
cp artifact-detection-config.yaml artifact-detection-config.yaml.backup
```

---

## 🧪 테스트

### 단위 테스트에서 사용

```csharp
// 테스트용 YAML 문자열 로드
var yamlContent = @"
artifactWeights:
  capture:
    DATABASE_INSERT: 0.7
";

var config = YamlConfigurationLoader.LoadFromString(yamlContent, logger);
var calculator = new ConfidenceCalculator(logger, config);

// DATABASE_INSERT 가중치가 0.7인지 검증
Assert.Equal(0.7, calculator.GetEventTypeWeight("DATABASE_INSERT"));
```

---

## 📚 참고

- **Configuration 모델**: `Models/Configuration/ArtifactDetectionConfig.cs`
- **기본값 제공자**: `Configuration/ConfigurationProvider.cs`
- **YAML 로더**: `Configuration/YamlConfigurationLoader.cs`

---

## 🔄 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|----------|
| 1.0.0 | 2025-10-17 | Phase 7 완료: YAML 로드 기능 추가 |
| 0.9.0 | 2025-10-17 | Phase 1-6: Configuration 모델 전환 |

