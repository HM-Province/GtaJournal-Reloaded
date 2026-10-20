using System.Runtime.InteropServices;

namespace GtaJournal.Services;

public sealed record TrayMenuItem(string Text, Action? Action, bool IsSeparator = false, bool IsChecked = false, bool IsEnabled = true)
{
    public static TrayMenuItem Separator => new("", null, IsSeparator: true);
}

/// <summary>
/// Иконка в области уведомлений на Shell_NotifyIcon. Создаёт скрытое окно для приёма сообщений
/// в UI-потоке (его очередь сообщений уже прокачивается WinUI).
/// </summary>
public sealed partial class TrayIcon : IDisposable
{
    private const int WM_APP_TRAY = 0x8000 + 1;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;
    private const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    private const int NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4;
    private const uint MF_STRING = 0x0, MF_SEPARATOR = 0x800, MF_CHECKED = 0x8, MF_GRAYED = 0x1;
    private const uint TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 0x2;

    private readonly WndProc _wndProc;
    private readonly IntPtr _hwnd;
    private readonly IntPtr _icon;
    private readonly uint _taskbarCreatedMessage;
    private readonly string _className = "GtaJournalTray_" + Guid.NewGuid().ToString("N");
    private string _tooltip;
    private bool _added;

    public TrayIcon(string iconPath, string tooltip)
    {
        _tooltip = tooltip;
        _wndProc = WindowProc;
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = _className,
        };
        RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(0, _className, "GtaJournalTray", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

        var size = GetSystemMetrics(49 /* SM_CXSMICON */);
        _icon = LoadImage(IntPtr.Zero, iconPath, 1 /* IMAGE_ICON */, size, size, 0x10 /* LR_LOADFROMFILE */);
    }

    /// <summary>Левый клик / двойной клик.</summary>
    public event Action? Activated;

    /// <summary>Пункты контекстного меню строятся заново при каждом открытии.</summary>
    public Func<IReadOnlyList<TrayMenuItem>>? MenuProvider { get; set; }

    public void Show()
    {
        if (_added) return;
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    public void Hide()
    {
        if (!_added) return;
        var data = CreateData(0);
        Shell_NotifyIcon(NIM_DELETE, ref data);
        _added = false;
    }

    public void SetTooltip(string tooltip)
    {
        _tooltip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        if (!_added) return;
        var data = CreateData(NIF_TIP);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private NOTIFYICONDATA CreateData(int flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _icon,
        szTip = _tooltip,
    };

    private IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_APP_TRAY)
        {
            switch ((int)(lParam.ToInt64() & 0xFFFF))
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    Activated?.Invoke();
                    break;
                case WM_RBUTTONUP:
                case WM_CONTEXTMENU:
                    ShowMenu();
                    break;
            }
            return IntPtr.Zero;
        }

        if (msg == _taskbarCreatedMessage && _added)
        {
            // Explorer перезапустился — иконку нужно добавить заново.
            _added = false;
            Show();
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var items = MenuProvider?.Invoke() ?? [];
        if (items.Count == 0) return;

        var menu = CreatePopupMenu();
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.IsSeparator)
                {
                    AppendMenu(menu, MF_SEPARATOR, 0, null);
                    continue;
                }
                var flags = MF_STRING | (item.IsChecked ? MF_CHECKED : 0) | (item.IsEnabled ? 0 : MF_GRAYED);
                AppendMenu(menu, flags, (nuint)(i + 1), item.Text);
            }

            GetCursorPos(out var pt);
            SetForegroundWindow(_hwnd);
            var command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);
            if (command > 0 && command <= items.Count)
                items[command - 1].Action?.Invoke();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        Hide();
        if (_icon != IntPtr.Zero) DestroyIcon(_icon);
        DestroyWindow(_hwnd);
        UnregisterClass(_className, GetModuleHandle(null));
    }

    // ---------- Win32 ----------

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClass(string className, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint load);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, nuint id, string? text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
