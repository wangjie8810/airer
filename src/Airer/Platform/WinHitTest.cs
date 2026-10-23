using System.Runtime.InteropServices;

namespace Airer;

public static class WinHitTest
{
    private const int GwlExStyle = -20;
    private const int GwlpWndProc = -4;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private static readonly Dictionary<IntPtr, Hook> Hooks = [];

    public static void Attach(IntPtr hwnd, Func<int, int, bool> interactive)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero || Hooks.ContainsKey(hwnd))
            return;

        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        style |= WsExNoActivate | WsExToolWindow;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(style));

        var hook = new Hook(interactive);
        hook.Previous = SetWindowLongPtr(hwnd, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(hook.Proc));
        Hooks[hwnd] = hook;
    }

    public static void Ensure(IntPtr hwnd, Func<int, int, bool> interactive)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
            return;
        if (!Hooks.TryGetValue(hwnd, out var hook))
        {
            Attach(hwnd, interactive);
            return;
        }

        var current = GetWindowLongPtr(hwnd, GwlpWndProc);
        var ours = Marshal.GetFunctionPointerForDelegate(hook.Proc);
        if (current != ours)
            hook.Previous = SetWindowLongPtr(hwnd, GwlpWndProc, ours);
    }

    public static void SetPassThrough(IntPtr hwnd, bool pass)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
            return;
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        var next = pass ? style | WsExTransparent : style & ~WsExTransparent;
        next |= WsExNoActivate | WsExToolWindow;
        if (next != style)
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(next));
    }

    public static void Detach(IntPtr hwnd)
    {
        if (!Hooks.Remove(hwnd, out var hook) || hook.Previous == IntPtr.Zero)
            return;
        SetWindowLongPtr(hwnd, GwlpWndProc, hook.Previous);
    }

    private sealed class Hook
    {
        public Hook(Func<int, int, bool> interactive)
        {
            Interactive = interactive;
            Proc = Call;
        }

        public IntPtr Previous;
        public readonly WndProc Proc;
        private readonly Func<int, int, bool> Interactive;

        private IntPtr Call(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WmNcHitTest)
            {
                var packed = lParam.ToInt64();
                var x = (short)(packed & 0xFFFF);
                var y = (short)((packed >> 16) & 0xFFFF);
                if (!Interactive(x, y))
                    return new IntPtr(HtTransparent);
            }
            return CallWindowProc(Previous, hwnd, msg, wParam, lParam);
        }
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr previous, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}
