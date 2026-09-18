using System.Windows;

namespace BaiYunBox;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 初始化日志与配置目录
        try
        {
            Core.AppLogger.Init(Core.AppPaths.LogDirectory);
            Core.AppPaths.EnsureDirectories();
            Core.AppLogger.Info($"BaiYun Box 启动，版本 {Core.AppPaths.AppVersion}");
        }
        catch (Exception ex)
        {
            // 日志初始化失败不阻断启动
            System.Diagnostics.Debug.WriteLine("日志初始化失败: " + ex.Message);
        }

        // 单实例：已存在实例则聚焦并退出
        if (!TryAcquireSingleInstance())
        {
            Core.AppLogger.Info("检测到已有实例运行，本次启动退出。");
            Shutdown();
            return;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ReleaseSingleInstance();
        base.OnExit(e);
    }

    private System.Threading.Mutex? _mutex;

    private bool TryAcquireSingleInstance()
    {
        _mutex = new System.Threading.Mutex(true, "BaiYunBox_SingleInstance_1.0", out bool createdNew);
        return createdNew;
    }

    private void ReleaseSingleInstance()
    {
        try
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch
        {
            // 忽略释放失败
        }
    }
}
