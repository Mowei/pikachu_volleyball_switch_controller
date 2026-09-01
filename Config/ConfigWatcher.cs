namespace SwitchMotionBridge.Config;

// 監控單一設定檔變更，並在偵測到修改後（去抖動）觸發回呼，讓使用者編輯設定檔後無需重啟程式即可套用。
internal sealed class ConfigWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounceTimer;

    public ConfigWatcher(string filePath, Action onChanged)
    {
        var directory = Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory;
        var fileName = Path.GetFileName(filePath);

        // 編輯器儲存時可能連續觸發多次變更事件，以計時器延遲合併為一次重新載入
        _debounceTimer = new System.Timers.Timer(300) { AutoReset = false };
        _debounceTimer.Elapsed += (_, _) => onChanged();

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += (_, _) => RestartDebounce();
        _watcher.Created += (_, _) => RestartDebounce();
    }

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
