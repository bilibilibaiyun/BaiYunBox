using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BaiYunBox.UI;

/// <summary>
/// 承载 libmpv 渲染的 WPF HwndHost：创建一个 Win32 子窗口，交给 mpv 作为 wid 渲染目标。
/// </summary>
public sealed class MpvHost : HwndHost
{
    private const string WindowClassName = "BaiYunBox.MpvHost";

    private IntPtr _childHwnd = IntPtr.Zero;

    public Services.MpvPlayer Player { get; } = new();

    /// <summary>子窗口创建完成后触发，参数为子窗口 HWND。</summary>
    public event EventHandler<IntPtr>? ChildCreated;

    static MpvHost()
    {
        RegisterWindowClass();
    }

    private static void RegisterWindowClass()
    {
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = DefWindowProc,
            hInstance = GetModuleHandle(null),
            lpszClassName = WindowClassName,
        };
        if (RegisterClassEx(ref wc) == 0)
        {
            // 可能已注册（同进程二次创建），忽略
        }
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _childHwnd = CreateWindowEx(
            0,
            WindowClassName,
            "",
            WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN,
            0, 0, 0, 0,
            hwndParent.Handle,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);

        if (_childHwnd == IntPtr.Zero)
            throw new InvalidOperationException("创建 mpv 渲染子窗口失败");

        ChildCreated?.Invoke(this, _childHwnd);
        return new HandleRef(this, _childHwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        try
        {
            Player.Dispose();
        }
        finally
        {
            if (hwnd.Handle != IntPtr.Zero)
                DestroyWindow(hwnd.Handle);
        }
    }

    // ---------- Win32 ----------

    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_CLIPSIBLINGS = 0x04000000;
    private const int WS_CLIPCHILDREN = 0x02000000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public MpvWndProc lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    private delegate IntPtr MpvWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
