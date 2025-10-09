# 시간 범위 필터링 문제 근본 원인 분석

## 📊 현상

### 테스트 설정
```csharp
var startTime = new DateTime(2025, 10, 7, 23, 13, 0);
var endTime = new DateTime(2025, 10, 7, 23, 30, 0);
var events = await ParseSampleLogsAsync("5차 샘플", startTime, endTime);
```

### 결과
- **파싱된 이벤트**: **0개** ❌
- **예상**: 581개 (하루 전체 파싱 시)

---

## 🔍 근본 원인 분석

### 1. 로그 파일 타임스탬프
```log
10-07 23:13:36:356  // MM-dd HH:mm:ss:fff 형식 (년도 없음)
```

### 2. 연도 추론 로직 (`TimestampNormalizer.cs`)
```csharp
// Line 81: EndToEndAnalysisTests.cs
CurrentTime = DateTime.Now  // 2025-10-08 (테스트 실행 시점)

// Line 119: TimestampNormalizer.cs
var deviceTime = _deviceInfo.CurrentTime;  // 2025-10-08
var year = deviceTime.Year;  // 2025

// Line 122-130: 연도 추론
var candidateTime = new DateTime(2025, 10, 7, 23, 13, 36, 356);

// Line 132-136: 미래 시간 체크
if (candidateTime > deviceTime)  // 2025-10-07 23:13 < 2025-10-08 → false
{
    // 미래가 아니므로 작년으로 변경 안 함
}

// 결과: 2025-10-07 23:13:36.356 (로컬 시간, Asia/Seoul)
```

### 3. UTC 변환 (`TimestampNormalizer.cs`)
```csharp
// Line 93: EndToEndAnalysisTests.cs
ConvertToUtc = true

// Line 105-110: TimestampNormalizer.cs
if (_convertToUtc)
{
    normalizedTime = ConvertToUtc(normalizedTime, format);
}

// Line 157-159: UTC 변환 (Asia/Seoul = UTC+9)
return TimeZoneInfo.ConvertTimeToUtc(localTime, _deviceTimeZone);

// 결과: 2025-10-07 14:13:36.356 UTC (23:13 - 9시간 = 14:13)
```

### 4. 시간 범위 필터링
```csharp
// 테스트 코드
var startTime = new DateTime(2025, 10, 7, 23, 13, 0);  // DateTimeKind.Unspecified
var endTime = new DateTime(2025, 10, 7, 23, 30, 0);

// 파서가 필터링할 때 이를 어떻게 해석하는가?
// Option A: UTC로 해석 → 2025-10-07 23:13:00 UTC
// Option B: 로컬로 해석 → 2025-10-07 23:13:00 Asia/Seoul → 2025-10-07 14:13:00 UTC

// 로그 타임스탬프 (UTC 변환 후): 2025-10-07 14:13:36 UTC
// 필터 범위 (Option A): 2025-10-07 23:13:00 UTC ~ 23:30:00 UTC
// 결과: 14:13 < 23:13 → 필터링으로 제외됨 → 0개
```

---

## 💡 해결 방안

### Option 1: UTC 변환 비활성화 (권장)
```csharp
var options = new LogParsingOptions 
{ 
    ConvertToUtc = false,  // 로컬 시간 유지
    StartTime = new DateTime(2025, 10, 7, 23, 13, 0),
    EndTime = new DateTime(2025, 10, 7, 23, 30, 0)
};
```

**장점**:
- 로그 원본 시간 그대로 사용
- 직관적이고 이해하기 쉬움
- 시간대 변환 오류 방지

**단점**:
- 여러 시간대의 로그를 비교할 때 불편할 수 있음 (현재는 단일 디바이스이므로 문제 없음)

### Option 2: 시간 범위를 UTC로 명시
```csharp
var kstStartTime = new DateTime(2025, 10, 7, 23, 13, 0, DateTimeKind.Local);
var kstEndTime = new DateTime(2025, 10, 7, 23, 30, 0, DateTimeKind.Local);

var utcStartTime = kstStartTime.ToUniversalTime();  // 2025-10-07 14:13:00 UTC
var utcEndTime = kstEndTime.ToUniversalTime();      // 2025-10-07 14:30:00 UTC

var options = new LogParsingOptions 
{ 
    ConvertToUtc = true,
    StartTime = utcStartTime,
    EndTime = utcEndTime
};
```

**장점**:
- UTC 기준으로 일관성 유지
- 시간대 혼동 방지

**단점**:
- 테스트 코드가 복잡해짐
- 사용자가 UTC로 변환해야 함

### Option 3: CurrentTime을 로그 날짜 기준으로 설정
```csharp
var deviceInfo = new DeviceInfo
{
    TimeZone = "Asia/Seoul",
    CurrentTime = new DateTime(2025, 10, 7, 23, 59, 59),  // 로그 날짜 기준
    AndroidVersion = "15",
    Manufacturer = "Samsung",
    Model = "SM-G991N"
};
```

**장점**:
- 연도 추론이 정확해짐

**단점**:
- 여전히 UTC 변환 문제는 해결 안 됨

---

## 🎯 권장 해결책: Option 1 (UTC 변환 비활성화)

### 이유
1. **단순성**: 로그 원본 시간을 그대로 사용
2. **직관성**: 시나리오 데이터 시트의 시간과 직접 매칭 가능
3. **안정성**: 시간대 변환 오류 방지
4. **적합성**: 현재는 단일 디바이스 분석이므로 UTC 변환 불필요

### 구현
```csharp
// ParseLogFileAsync 수정
var options = new LogParsingOptions 
{ 
    MaxFileSizeMB = 50,
    DeviceInfo = deviceInfo,
    ConvertToUtc = false,  // ✅ 변경
    StartTime = startTime,
    EndTime = endTime
};
```

---

## 📌 추가 고려사항

### Q1: 연도가 없을 경우 2개의 연도 데이터가 하나의 로그 파일에 있을 수 있는데?

**A**: `TimestampNormalizer.AddYearInformation` 로직이 이미 처리:
```csharp
// Line 132-136
if (candidateTime > deviceTime)
{
    // 미래 시간이면 작년으로 설정
    candidateTime = candidateTime.AddYears(-1);
}
```

**예시**:
- `CurrentTime` = 2025-01-05 (새해 초)
- 로그 타임스탬프 = `12-31 23:59` (작년 연말)
- `candidateTime` = 2025-12-31 23:59
- `candidateTime > CurrentTime` → true
- 결과: 2024-12-31 23:59 ✅

**문제점**:
- 로그 파일이 정확히 연도 경계를 넘는 경우 (예: 12-31 23:00 ~ 01-01 01:00)
- 이 경우 `CurrentTime`을 로그 파일 중간 날짜로 설정하면 해결 가능

### Q2: 분석 시점 기준으로 연도를 추론하면 안 되는가?

**A**: 현재 구현이 바로 그것입니다:
```csharp
CurrentTime = DateTime.Now  // 분석 시점 기준
```

**문제**:
- 로그가 과거 것이면 (예: 2024년 10월 로그를 2025년 10월에 분석)
- 잘못된 연도가 추론될 수 있음

**해결책**:
- 로그 파일 자체에서 연도 정보 추출 (예: `usagestats.log`는 연도 포함)
- 또는 사용자가 명시적으로 연도 지정

---

## ✅ 액션 아이템

1. ✅ `ParseLogFileAsync`에서 `ConvertToUtc = false`로 변경
2. ✅ 테스트 실행하여 이벤트 파싱 확인
3. ⏳ piid 중복 제거 로직 검증
4. ⏳ Ground Truth 재검증

