namespace AndroidAdbAnalyze.Analysis.Models.Sessions;

/// <summary>
/// 세션이 불완전한 이유
/// </summary>
public enum SessionIncompleteReason
{
    /// <summary>
    /// 시작 이벤트 누락
    /// </summary>
    MissingStart,
    
    /// <summary>
    /// 종료 이벤트 누락
    /// </summary>
    MissingEnd,
    
    /// <summary>
    /// 로그가 잘림 (24시간 경과, 로그 크기 제한 등)
    /// </summary>
    LogTruncated,
    
    /// <summary>
    /// 디바이스 재부팅으로 인한 로그 손실
    /// </summary>
    DeviceReboot,
    
    /// <summary>
    /// 알 수 없는 이유
    /// </summary>
    Unknown
}
