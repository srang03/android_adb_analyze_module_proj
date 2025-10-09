# AI 개발 가이드라인 및 작업 규칙

## 문서 개요
본 문서는 AndroidAdbAnalyzeModule 프로젝트 개발 시 AI가 따라야 할 규칙과 가이드라인을 정의합니다.

---

## 핵심 개발 원칙

### 1. 오버 엔지니어링 절대 금지
- **단순하고 명확한 구조**를 우선시
- 불필요한 추상화 레이어 생성 금지
- 당장 사용하지 않는 기능 구현 금지
- "나중에 필요할 수도 있다"는 이유로 복잡한 구조 추가 금지

### 2. 객체지향 및 캡슐화
- **불변성(Immutability)** 원칙 준수
  - `init` 프로퍼티 사용 (set 금지)
  - `IReadOnly*` 컬렉션 반환
  - 외부에서 내부 상태 직접 변경 불가
- **캡슐화** 철저히
  - private 필드, public 프로퍼티
  - 비즈니스 로직은 클래스 내부에
- **단일 책임 원칙(SRP)** 준수
  - 한 클래스는 한 가지 책임만

### 3. 유지보수성
- 명확한 네이밍: 클래스, 메서드, 변수명이 용도를 명확히 표현
- XML 주석 작성: public 멤버에는 반드시 주석
- 매직 넘버/문자열 지양: 상수로 정의
- 중복 코드 최소화

---

## 코딩 스타일 규칙

### 네이밍 컨벤션
- **클래스/인터페이스**: PascalCase (`LogParser`, `ILogEventRepository`)
- **메서드**: PascalCase (`ParseAsync`, `GetEventsByTimeRange`)
- **프로퍼티**: PascalCase (`EventId`, `Timestamp`)
- **private 필드**: _camelCase (`_events`, `_lock`)
- **매개변수/지역변수**: camelCase (`logFilePath`, `options`)
- **상수**: ALL_CAPS (`MAX_FILE_SIZE_MB`, `DEFAULT_TIMEOUT`)

### 클래스 구조
```csharp
public sealed class ExampleClass  // sealed 사용 권장 (상속 불필요 시)
{
    // 1. private 필드
    private readonly IDependency _dependency;
    
    // 2. 생성자
    public ExampleClass(IDependency dependency)
    {
        _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }
    
    // 3. public 프로퍼티
    public string PropertyName { get; init; }
    
    // 4. public 메서드
    public async Task<Result> MethodAsync()
    {
        // 구현
    }
    
    // 5. private 메서드
    private void HelperMethod()
    {
        // 구현
    }
}
```

### 비동기 메서드
- 비동기 메서드는 `Async` 접미사 필수
- `CancellationToken` 매개변수 제공 (선택사항, 기본값 `default`)
- `Task<T>` 반환 (void 반환 시 `Task`)

```csharp
public async Task<ParsingResult> ParseAsync(
    string logFilePath,
    LogParsingOptions options,
    CancellationToken cancellationToken = default)
{
    // 구현
}
```

### 예외 처리
- 명확한 예외 메시지 제공
- 커스텀 예외는 `Exception` 상속 후 의미 있는 이름 사용
- `throw;`로 원본 스택 트레이스 유지

```csharp
public sealed class ConfigurationValidationException : Exception
{
    public ConfigurationValidationException(string message) 
        : base(message) { }
    
    public ConfigurationValidationException(string message, Exception innerException) 
        : base(message, innerException) { }
}
```

---

## 프로젝트 특화 규칙

### 1. 다양한 로그 타입 지원
- **audio, battery, power, memory 등 다양한 dumpsys 로그를 파싱**해야 함
- 로그 타입별 특수 처리는 최소화하고 **공통 로직 재사용**
- 새로운 로그 타입 추가 시 기존 코드 수정 최소화

### 2. 외부 설정 파일 의존
- 모든 파싱 룰은 **YAML 설정 파일에 정의**
- 하드코딩은 **복잡한 패턴에만 제한적으로 사용**
- 설정 파일 스키마 변경 시 **버전 관리 및 마이그레이션** 필수

### 3. 상태 변경 불가
- 외부에서 내부 상태를 직접 변경할 수 없도록 설계
- 모든 모델 클래스는 **불변 객체**로 구현
- 빌더 패턴 또는 `with` 표현식 사용 권장

```csharp
// 좋은 예: 불변 객체
public sealed class NormalizedLogEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object> Attributes { get; init; }
}

// 나쁜 예: 가변 객체
public class NormalizedLogEvent
{
    public Guid EventId { get; set; }  // set 사용 금지
    public Dictionary<string, object> Attributes { get; set; }  // IReadOnly* 사용
}
```

### 4. 플러그인 시스템
- **Phase 1에서는 인터페이스만 정의**, 실제 구현은 추후
- 복잡한 패턴은 일단 **하드코딩**으로 처리
- 반복되는 복잡한 패턴 확인 시 플러그인화 고려

### 5. 성능 고려사항
- **10MB 미만 파일은 전체 로드** (메모리에 올림)
- **스트리밍 파싱은 추후 고려**
- 실시간 처리를 위한 최적화 필수
- LINQ 사용 시 불필요한 Enumeration 최소화

---

## 작업 프로세스

### 단계별 진행
1. **요구사항 확인**: 사용자의 요구사항 정확히 파악
2. **설계 검토**: 오버 엔지니어링 없는지 확인
3. **코드 작성**: 규칙 준수하며 구현
4. **사용자 피드백**: 작성한 코드 검토 받기
5. **수정 및 반영**: 피드백 반영
6. **다음 단계 진행**: Phase별 순차 진행

### 구현 전 확인사항
- [ ] 이 기능이 **지금 당장 필요**한가?
- [ ] 더 **단순한 방법**은 없는가?
- [ ] **기존 코드를 재사용**할 수 있는가?
- [ ] 외부에서 **상태를 변경할 수 없는** 구조인가?
- [ ] **다른 로그 타입에서도 재사용** 가능한가?

### 코드 리뷰 체크리스트
- [ ] 불변성 원칙 준수 (`init`, `IReadOnly*`)
- [ ] XML 주석 작성 완료
- [ ] 예외 처리 적절
- [ ] 네이밍 컨벤션 준수
- [ ] 단위 테스트 가능한 구조
- [ ] 오버 엔지니어링 없음

---

## 커밋 및 문서 관리

### 코드 작성 시
- **작은 단위로 구현**: 한 번에 하나의 기능만
- **사용자와 함께 검토**: 각 단계마다 피드백
- **DevelopmentPlan.md 체크박스 업데이트**: 완료된 항목 체크

### 문서 작성
- **간결하고 명확하게**: 불필요한 설명 제거
- **예제 코드 포함**: 이해를 돕는 간단한 예제
- **업데이트 유지**: 코드 변경 시 문서도 함께 수정

---

## AI 작업 시 금지사항

### ❌ 절대 하지 말 것
1. **사용자 확인 없이 대규모 리팩토링**
2. **요구하지 않은 추가 기능 구현**
3. **복잡한 디자인 패턴 무분별하게 적용** (Factory, Strategy, Observer 등)
4. **추상화 레이어 과도하게 생성**
5. **미래를 대비한 확장 포인트 미리 구현**
6. **설정 파일 없이 하드코딩 룰 추가**

### ⚠️ 주의할 것
1. **LINQ 체이닝 과도하게 사용**: 가독성 저하
2. **매직 넘버/문자열 방치**: 상수로 정의
3. **예외 무시**: try-catch로 잡고 아무 처리 안 함
4. **로깅 누락**: 중요한 실행 흐름은 로깅 필요

---

## 사용자 질문 시 대응

### 명확하지 않은 요구사항
- 가정하지 말고 **질문으로 명확히 하기**
- 여러 옵션 제시 후 선택 받기
- 샘플 코드로 의사소통

### 기술적 제약사항 발견
- **즉시 사용자에게 알림**
- 대안 제시
- 권장 방향 제안

### 오버 엔지니어링 가능성
- 사용자에게 **더 단순한 방법 제안**
- 현재 필요한 기능만 구현하도록 유도

---

## AI 커맨드 템플릿

### 새로운 기능 구현 요청 시
```
1. 요구사항 확인:
   - [요구사항 정리]

2. 구현 방법:
   - [간단한 방법 우선 제안]

3. 영향 범위:
   - 수정할 파일: [파일 목록]
   - 추가할 클래스: [클래스 목록]

4. 확인 필요:
   - [불명확한 부분 질문]

진행해도 될까요?
```

### 코드 검토 요청 시
```
[작성한 코드]

체크사항:
- [x] 불변성 원칙 준수
- [x] XML 주석 작성
- [ ] 단위 테스트 작성 (다음 단계)

검토 부탁드립니다.
```

---

## 프로젝트 특수 상황 대응

### 로그 샘플 추가 시
1. 로그 구조 분석
2. 기존 파서로 처리 가능한지 확인
3. 불가능하면 최소한의 수정 제안
4. 설정 파일 업데이트 방향 제시

### 버그 발견 시
1. 재현 방법 확인
2. 원인 분석
3. 최소 수정으로 해결 방안 제시
4. 테스트 케이스 추가

### 성능 이슈 발견 시
1. 병목 지점 특정
2. 프로파일링 데이터 확인
3. 최적화 방법 제안 (단, 가독성 희생 최소화)

---

## 버전 관리

### 설정 파일 버전
- `configSchemaVersion`: 설정 파일 스키마 버전
- 버전 변경 시 마이그레이터 구현 필수
- 하위 호환성 최대한 유지

### 코드 버전
- AssemblyVersion: 메이저 변경 시만 업데이트
- AssemblyFileVersion: 매 릴리스마다 업데이트
- 플러그인 API 버전: 인터페이스 변경 시 명시

---

## 요약

### 핵심 3원칙
1. **단순함**: 오버 엔지니어링 금지
2. **불변성**: 외부에서 상태 변경 불가
3. **재사용**: 다양한 로그 타입 지원

### 작업 흐름
```
요구사항 → 설계 검토 → 구현 → 피드백 → 수정 → 다음 단계
```

### 의사결정 우선순위
```
단순함 > 명확함 > 성능 > 확장성
```

---

**이 문서는 프로젝트 진행 중 지속적으로 업데이트됩니다.**

