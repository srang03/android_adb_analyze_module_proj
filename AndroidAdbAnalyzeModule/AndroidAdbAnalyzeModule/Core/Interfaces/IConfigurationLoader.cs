namespace AndroidAdbAnalyzeModule.Core.Interfaces;

/// <summary>
/// 설정 파일 로더 인터페이스
/// </summary>
/// <typeparam name="TConfig">설정 타입</typeparam>
public interface IConfigurationLoader<TConfig> where TConfig : class
{
    /// <summary>
    /// 설정 파일 동기 로드
    /// </summary>
    TConfig Load(string configPath);

    /// <summary>
    /// 설정 파일 비동기 로드
    /// </summary>
    Task<TConfig> LoadAsync(string configPath);

    /// <summary>
    /// 설정 재로드
    /// 파싱 작업 중에는 호출 불가
    /// </summary>
    void Reload();

    /// <summary>
    /// 현재 로드된 설정
    /// </summary>
    TConfig? CurrentConfiguration { get; }

    /// <summary>
    /// 설정 변경 이벤트
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
}

/// <summary>
/// 설정 변경 이벤트 인자
/// </summary>
public sealed class ConfigurationChangedEventArgs : EventArgs
{
    /// <summary>
    /// 새로운 설정
    /// </summary>
    public object NewConfiguration { get; init; }

    public ConfigurationChangedEventArgs(object newConfiguration)
    {
        NewConfiguration = newConfiguration;
    }
}

