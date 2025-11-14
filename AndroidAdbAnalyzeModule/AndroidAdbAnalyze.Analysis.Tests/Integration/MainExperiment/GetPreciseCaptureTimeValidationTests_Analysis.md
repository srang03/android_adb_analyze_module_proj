# GetPreciseCaptureTimeValidationTests 코드 검토 보고서

## 1. 하드코딩 검토

### ✅ 하드코딩이 없는 부분

1. **파라미터 값들** (라인 379-382)
   - `ArtifactWeights.EventCorrelationWindowSeconds` 사용 ✅
   - `ArtifactWeights.DeduplicationSimilarityThreshold` 사용 ✅
   - `ArtifactWeights.SameCameraUsageTimeThreshold` 사용 ✅
   - `ArtifactWeights.CaptureDeduplicationWindowMs` 사용 ✅

2. **샘플 시간 범위** (라인 81)
   - `ArtifactWeights.SampleTimeRanges` 사용 ✅

3. **DeviceInfo 생성** (라인 344)
   - `ArtifactWeights.CreateTestDeviceInfo()` 사용 ✅

### ⚠️ 하드코딩이 있는 부분

1. **정밀한 아티팩트 선택 로직** (라인 175-179)
   ```csharp
   .OrderByDescending(e => 
       e.EventType == "DATABASE_INSERT" ? 3 :
       e.EventType == "VIBRATION_EVENT" ? 2 : 1)
   .ThenBy(e => e.Timestamp)
   ```
   - **문제점**: 실제 비즈니스 로직(`BaseCaptureDetectionStrategy.GetPreciseCaptureTime`, 라인 430-433)과 동일한 로직을 테스트 코드에서 중복 구현
   - **비교**:
     - 비즈니스 로직: `BaseCaptureDetectionStrategy.cs` 라인 430-433
     - 테스트 코드: `GetPreciseCaptureTimeValidationTests.cs` 라인 175-179
     - **로직이 완전히 동일함** (DATABASE_INSERT=3, VIBRATION_EVENT=2, 기타=1, 타임스탬프 순)
   - **영향**: 비즈니스 로직이 변경되면 테스트 코드도 수동으로 수정해야 함

2. **이벤트 타입 문자열** (라인 133, 160, 174, 176-177)
   - `"FOREGROUND_SERVICE"`, `"DATABASE_INSERT"`, `"VIBRATION_EVENT"` 하드코딩
   - **비고**: 이벤트 타입은 상수로 정의되어 있지 않으므로, 이는 불가피한 하드코딩

3. **예비 실험 데이터** (라인 241-242)
   - `"852ms"` 하드코딩
   - **비고**: 예비 실험 결과를 문서화한 것이므로, 이는 불가피한 하드코딩

## 2. 비즈니스 로직 수행 결과 검토

### ✅ 실제 비즈니스 로직 수행 결과를 사용하는 부분

1. **CaptureTime** (라인 165)
   ```csharp
   var preciseTimestamp = capture.CaptureTime;
   ```
   - **출처**: `BaseCaptureDetectionStrategy.CreateCaptureEvent` (라인 380)
   - **비즈니스 로직**: `CaptureTime = GetPreciseCaptureTime(keyArtifact, allArtifacts)`
   - **결론**: ✅ 실제 비즈니스 로직(`GetPreciseCaptureTime`)이 수행된 결과를 사용

2. **FOREGROUND_SERVICE 타임스탬프** (라인 162)
   ```csharp
   var foregroundTimestamp = keyArtifact.Timestamp;
   ```
   - **출처**: 원본 로그 이벤트 (`_allParsedEvents`)
   - **결론**: ✅ 실제 원본 데이터를 사용

3. **타임스탬프 차이 계산** (라인 168)
   ```csharp
   var difference = preciseTimestamp - foregroundTimestamp;
   ```
   - **결론**: ✅ 실제 비즈니스 로직 결과와 원본 데이터의 차이를 계산

### ⚠️ 비즈니스 로직을 재구현한 부분

1. **정밀한 아티팩트 타입 확인** (라인 171-182)
   ```csharp
   var allArtifacts = _allParsedEvents!
       .Where(e => allArtifactIds.Contains(e.EventId))
       .Where(e => e.EventType != "FOREGROUND_SERVICE")
       .OrderByDescending(e => 
           e.EventType == "DATABASE_INSERT" ? 3 :
           e.EventType == "VIBRATION_EVENT" ? 2 : 1)
       .ThenBy(e => e.Timestamp)
       .ToList();
   var preciseArtifact = allArtifacts.FirstOrDefault();
   var preciseArtifactType = preciseArtifact?.EventType ?? "NONE";
   ```
   - **문제점**: 실제 비즈니스 로직(`GetPreciseCaptureTime`)과 동일한 로직을 테스트 코드에서 재구현
   - **비교**:
     - 비즈니스 로직: `BaseCaptureDetectionStrategy.GetPreciseCaptureTime` (라인 428-434)
     - 테스트 코드: 라인 172-179
     - **로직이 완전히 동일함**
   - **영향**: 
     - 비즈니스 로직이 변경되면 테스트 코드도 수동으로 수정해야 함
     - 테스트 코드의 정밀한 아티팩트 타입이 실제 비즈니스 로직 결과와 다를 수 있음 (동기화 문제)

## 3. 개선 제안

### 제안 1: 정밀한 아티팩트 타입 확인 로직 제거 또는 간소화

**현재 문제**: 테스트 코드에서 비즈니스 로직을 재구현하여 정밀한 아티팩트 타입을 확인

**개선 방안**:
1. **옵션 A**: 정밀한 아티팩트 타입 확인 로직 제거
   - `CaptureTime`과 `foregroundTimestamp`의 차이만 측정
   - 정밀한 아티팩트 타입은 출력하지 않음
   - **장점**: 비즈니스 로직과의 동기화 문제 해결
   - **단점**: 정밀한 아티팩트 타입 정보 손실

2. **옵션 B**: `CaptureTime`과 실제로 사용된 아티팩트의 타임스탬프를 비교하여 역추적
   ```csharp
   // CaptureTime과 일치하는 아티팩트를 찾아서 타입 확인
   var preciseArtifact = _allParsedEvents!
       .Where(e => allArtifactIds.Contains(e.EventId))
       .Where(e => e.EventType != "FOREGROUND_SERVICE")
       .Where(e => Math.Abs((e.Timestamp - capture.CaptureTime).TotalMilliseconds) < 1.0) // 1ms 이내
       .OrderByDescending(e => 
           e.EventType == "DATABASE_INSERT" ? 3 :
           e.EventType == "VIBRATION_EVENT" ? 2 : 1)
       .FirstOrDefault();
   ```
   - **장점**: 실제 비즈니스 로직 결과(`CaptureTime`)를 기반으로 역추적
   - **단점**: 타임스탬프가 정확히 일치하지 않을 수 있음

3. **옵션 C**: 현재 방식 유지하되, 주석으로 비즈니스 로직과의 동기화 필요성 명시
   - **장점**: 구현 변경 없음
   - **단점**: 동기화 문제는 해결되지 않음

### 제안 2: 비즈니스 로직 재사용 (가능한 경우)

**현재 문제**: `GetPreciseCaptureTime`이 `protected virtual`이므로 직접 호출 불가

**개선 방안**:
- `GetPreciseCaptureTime`을 `public` 또는 `internal`로 변경하고 테스트 코드에서 재사용
- **단점**: 비즈니스 로직의 캡슐화 훼손

## 4. 최종 결론

### ✅ 올바른 부분

1. **비즈니스 로직 수행 결과 사용**: `capture.CaptureTime`은 실제 비즈니스 로직(`GetPreciseCaptureTime`)이 수행된 결과를 사용 ✅
2. **원본 데이터 사용**: `foregroundTimestamp`는 원본 로그 이벤트에서 추출 ✅
3. **차이 계산**: 실제 비즈니스 로직 결과와 원본 데이터의 차이를 정확히 계산 ✅
4. **파라미터 값**: 모든 파라미터를 `ArtifactWeights` 상수에서 가져옴 ✅

### ⚠️ 개선 필요 부분

1. **정밀한 아티팩트 타입 확인 로직**: 비즈니스 로직과 동일한 로직을 테스트 코드에서 중복 구현
   - **위험도**: 중간 (비즈니스 로직 변경 시 테스트 코드도 수동 수정 필요)
   - **영향**: 테스트 코드의 정밀한 아티팩트 타입이 실제 비즈니스 로직 결과와 다를 수 있음

2. **이벤트 타입 문자열**: 하드코딩되어 있으나, 상수로 정의되어 있지 않으므로 불가피함

### 권장 사항

1. **정밀한 아티팩트 타입 확인 로직**: 옵션 B (역추적 방식) 또는 옵션 A (제거) 권장
2. **주석 추가**: 비즈니스 로직과의 동기화 필요성 명시
3. **비즈니스 로직 검증**: `CaptureTime`이 실제로 `GetPreciseCaptureTime`의 결과인지 확인하는 추가 검증 로직 고려

