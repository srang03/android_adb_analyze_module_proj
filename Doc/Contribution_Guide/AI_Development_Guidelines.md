# AI 개발 가이드라인

## 문서 개요
본 문서는 C# .NET 기반 AndroidAdbAnalyzeModule 프로젝트 개발 시 AI가 따라야 할 핵심 규칙과 가이드라인을 정의합니다.

---

## 1. 핵심 개발 원칙

### 1.1 오버 엔지니어링 절대 금지
- 단순하고 명확한 구조를 우선시
- 불필요한 추상화 레이어 생성 금지
- 당장 사용하지 않는 기능 구현 금지
- "나중에 필요할 수도 있다"는 이유로 복잡한 구조 추가 금지
- 복잡한 디자인 패턴 무분별 적용 금지 (Factory, Strategy, Observer 등은 명확한 필요성이 있을 때만)

### 1.2 불변성 및 캡슐화
- 모든 모델 클래스는 불변 객체로 구현
  - `init` 프로퍼티 사용 (set 금지)
  - `IReadOnlyCollection<T>`, `IReadOnlyDictionary<K,V>` 반환
  - 외부에서 내부 상태 직접 변경 불가
- 캡슐화 철저히
  - private 필드, public 프로퍼티
  - 비즈니스 로직은 클래스 내부에
- 단일 책임 원칙(SRP) 준수
  - 한 클래스는 한 가지 책임만

### 1.3 의존성 관리
- 순환 참조 절대 금지
  - 프로젝트 참조: Parser -> Analysis (단방향만 허용)
  - 클래스 참조: A -> B -> C (순환 금지)
  - 의존성 추가 전 반드시 전체 의존성 그래프 검토
- 의존성 역전 원칙(DIP) 준수
  - 상위 모듈이 하위 모듈에 직접 의존 금지
  - 인터페이스를 통한 추상화 의존
  - 예: `IAnalysisOrchestrator`, `ISessionDetector`
- 의존성 주입(DI) 패턴 사용
  - 생성자 주입 우선 (필드 주입 금지)
  - null 검증 필수: `_dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));`
  - 순환 의존성 발생 시 인터페이스 분리 또는 이벤트 기반 통신 고려

### 1.4 유지보수성
- 명확한 네이밍: 클래스, 메서드, 변수명이 용도를 명확히 표현
- XML 주석 작성: public 멤버에는 반드시 주석
- 매직 넘버/문자열 지양: 상수로 정의
- 중복 코드 최소화

### 1.5 절대 금지사항
1. 사용자 확인 없이 대규모 리팩토링
2. 요구하지 않은 추가 기능 구현
3. 추상화 레이어 과도하게 생성
4. 미래를 대비한 확장 포인트 미리 구현
5. 설정 파일 없이 하드코딩 룰 추가
6. 순환 참조 구조 생성

### 1.6 주의사항
1. LINQ 체이닝 과도하게 사용: 가독성 저하
2. 매직 넘버/문자열 방치: 상수로 정의
3. 예외 무시: try-catch로 잡고 아무 처리 안 함
4. 로깅 누락: 중요한 실행 흐름은 로깅 필요
5. 의존성 추가 시 순환 참조 검증 누락
6. 기존엑 이미 구현되어 있는 코드가 있는지 검토 수행
7. 중복되거나 누락되지 않도록 작업 수행하면서 검토 수행

---

## 2. 코딩 스타일 규칙

### 2.1 네이밍 컨벤션
- 클래스/인터페이스: PascalCase (`LogParser`, `ILogEventRepository`)
- 메서드: PascalCase (`ParseAsync`, `GetEventsByTimeRange`)
- 프로퍼티: PascalCase (`EventId`, `Timestamp`)
- private 필드: _camelCase (`_events`, `_lock`)
- 매개변수/지역변수: camelCase (`logFilePath`, `options`)
- 상수: ALL_CAPS (`MAX_FILE_SIZE_MB`, `DEFAULT_TIMEOUT`)

### 2.2 클래스 구조
```csharp
public sealed class ExampleClass  // sealed 사용 권장 (상속 불필요 시)
{
    private readonly IDependency _dependency;
    
    public ExampleClass(IDependency dependency)
    {
        _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }
    
    public string PropertyName { get; init; }  // init 사용
    public async Task<Result> MethodAsync() { ... }
}
```

### 2.3 비동기 메서드
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

### 2.4 예외 처리
- 명확한 예외 메시지 제공
- 커스텀 예외는 `Exception` 상속 후 의미 있는 이름 사용
- `throw;`로 원본 스택 트레이스 유지

```csharp
public sealed class ConfigurationValidationException : Exception
{
    public ConfigurationValidationException(string message) : base(message) { }
    public ConfigurationValidationException(string message, Exception innerException) 
        : base(message, innerException) { }
}
```

---

## 3. 프로젝트 특화 규칙

### 3.1 다양한 로그 타입 지원
- audio, battery, power, memory 등 다양한 dumpsys 로그를 파싱해야 함
- 로그 타입별 특수 처리는 최소화하고 공통 로직 재사용
- 새로운 로그 타입 추가 시 기존 코드 수정 최소화

### 3.2 외부 설정 파일 의존
- 모든 파싱 룰은 YAML 설정 파일에 정의
- 하드코딩은 복잡한 패턴(멀티라인 파싱 등)에만 제한적으로 사용
- 설정 파일 스키마 변경 시 버전 관리 필수

### 3.3 불변 객체 강제

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

### 3.4 성능 고려사항
- 10MB 미만 파일은 전체 로드 (메모리에 올림)
- 스트리밍 파싱은 추후 고려
- 실시간 처리를 위한 최적화 필수
- LINQ 사용 시 불필요한 Enumeration 최소화

### 3.5 프로젝트 구조 및 의존성
- AndroidAdbAnalyze.Parser: 로그 파싱 담당 (하위 계층)
- AndroidAdbAnalyze.Analysis: 카메라 세션/촬영 분석 담당 (상위 계층)
- 의존성 방향: Analysis -> Parser (단방향만 허용)
- Parser는 Analysis를 참조하지 않음 (역방향 의존 절대 금지)

---

## 4. 작업 프로세스

### 4.1 단계별 진행
1. 요구사항 확인: 사용자의 요구사항 정확히 파악
2. 의존성 검토: 순환 참조 및 의존성 방향 확인
3. 설계 검토: 오버 엔지니어링 없는지 확인
4. 코드 작성: 규칙 준수하며 구현
5. 사용자 피드백: 작성한 코드 검토 받기
6. 수정 및 반영: 피드백 반영

### 4.2 구현 전 체크리스트
- [ ] 이 기능이 지금 당장 필요한가?
- [ ] 더 단순한 방법은 없는가?
- [ ] 기존 코드를 재사용할 수 있는가?
- [ ] 외부에서 상태를 변경할 수 없는 구조인가?
- [ ] 다른 로그 타입에서도 재사용 가능한가?
- [ ] 순환 참조가 발생하지 않는가?
- [ ] 의존성 방향이 올바른가? (Analysis -> Parser)

### 4.3 코드 리뷰 체크리스트
- [ ] 불변성 원칙 준수 (`init`, `IReadOnly*`)
- [ ] XML 주석 작성 완료
- [ ] 예외 처리 적절
- [ ] 네이밍 컨벤션 준수
- [ ] 단위 테스트 가능한 구조
- [ ] 오버 엔지니어링 없음
- [ ] 순환 참조 없음
- [ ] 의존성 주입 패턴 준수

### 4.4 의존성 추가 시 검증 절차
1. 추가하려는 의존성의 레이어 확인 (Parser/Analysis/외부 라이브러리)
2. 기존 의존성 그래프에서 순환 참조 발생 여부 확인
3. 의존성 방향이 아키텍처 원칙과 일치하는지 확인
4. 불가피한 경우 인터페이스 분리 또는 이벤트 기반 통신 고려
5. 사용자에게 의존성 추가 이유 및 영향 범위 설명

---

## 5. 문제 상황 대응

### 5.1 명확하지 않은 요구사항
- 가정하지 말고 질문으로 명확히 하기
- 여러 옵션 제시 후 선택 받기
- 샘플 코드로 의사소통

### 5.2 기술적 제약사항 발견
- 즉시 사용자에게 알림
- 대안 제시
- 권장 방향 제안

### 5.3 순환 참조 발견 시
1. 순환 참조가 발생한 경로 명확히 분석 (A -> B -> C -> A)
2. 다음 해결 방안 중 선택
   - 인터페이스 분리: 공통 인터페이스를 별도 프로젝트/네임스페이스로 분리
   - 이벤트 기반 통신: 직접 참조 대신 이벤트/콜백 사용
   - 의존성 역전: 상위 모듈이 인터페이스 정의, 하위 모듈이 구현
3. 사용자에게 해결 방안 설명 및 승인 요청

### 5.4 버그 발견 시
1. 재현 방법 확인
2. 원인 분석 (의존성 문제, 로직 오류, 상태 변경 등)
3. 최소 수정으로 해결 방안 제시
4. 테스트 케이스 추가

---

## 6. 요약

### 6.1 핵심 4원칙
1. 단순함: 오버 엔지니어링 금지
2. 불변성: 외부에서 상태 변경 불가
3. 재사용: 다양한 로그 타입 지원
4. 의존성 관리: 순환 참조 금지, 단방향 의존성 유지

### 6.2 작업 흐름
```
요구사항 → 의존성 검토 → 설계 검토 → 구현 → 피드백 → 수정
```

### 6.3 의사결정 우선순위
```
단순함 > 명확함 > 의존성 안정성 > 성능 > 확장성
```

### 6.4 의존성 원칙
```
Analysis (상위) -> Parser (하위)
       |              |
       v              v
  인터페이스 <-  구현체
  
순환 참조 금지: A -> B -> A (X)
```

---

본 문서는 프로젝트 진행 중 지속적으로 업데이트됩니다.
