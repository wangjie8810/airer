using System.Runtime.InteropServices;

namespace Airer;

public sealed class HotkeyHub : IDisposable
{
    private const uint WmHotkey = 0x0312;
    private const uint WmRebind = 0x8001;
    private readonly Action<int> _onHotkey;
    private readonly object _gate = new();
    private readonly Dictionary<int, Action> _actions = [];
    private readonly HashSet<int> _registered = [];
    private List<(int Id, HotkeyChord Chord)> _bindings = [];
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hwnd;
    private readonly ManualResetEventSlim _ready = new(false);
    private bool _disposed;

    public HotkeyHub(Action<int> onHotkey) => _onHotkey = onHotkey;

    public bool IsRegistered(int id)
    {
        lock (_gate)
            return _registered.Contains(id);
    }

    public void Start()
    {
        if (OperatingSystem.IsWindows())
            StartWindows();
        else if (OperatingSystem.IsMacOS())
            MacHotkeys.Install(id => _onHotkey(id));
    }

    public void Replace(IReadOnlyList<(int Id, HotkeyChord Chord, Action Action)> bindings)
    {
        lock (_gate)
        {
            _bindings = bindings.Select(b => (b.Id, b.Chord)).ToList();
            _actions.Clear();
            foreach (var binding in bindings)
                _actions[binding.Id] = binding.Action;
        }

        if (OperatingSystem.IsWindows())
        {
            if (_threadId != 0)
                PostThreadMessage(_threadId, WmRebind, IntPtr.Zero, IntPtr.Zero);
        }
        else if (OperatingSystem.IsMacOS())
        {
            RebindMac();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        if (OperatingSystem.IsWindows() && _threadId != 0)
            PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        if (OperatingSystem.IsMacOS())
            MacHotkeys.Clear();
    }

    private void StartWindows()
    {
        _thread = new Thread(WindowsLoop) { IsBackground = true, Name = "Airer hotkeys" };
        _thread.Start();
        _ready.Wait(3000);
    }

    private void WindowsLoop()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
        _hwnd = CreateWindowEx(0, "Static", "AirerHotkeys", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        _ready.Set();
        if (_hwnd == IntPtr.Zero)
        {
            Log.Error("Hotkey window was not created: " + Marshal.GetLastWin32Error());
            return;
        }
        RebindWindows();
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.Message == WmHotkey)
                Fire(unchecked((int)msg.WParam.ToInt64()));
            else if (msg.Message == WmRebind)
                RebindWindows();
            else
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        RebindWindows(clearOnly: true);
    }

    private void RebindWindows(bool clearOnly = false)
    {
        foreach (var id in new[] { 1, 2, 3 })
            UnregisterHotKey(_hwnd, id);
        var ok = new HashSet<int>();
        if (!clearOnly && !_disposed)
        {
            List<(int Id, HotkeyChord Chord)> bindings;
            lock (_gate)
                bindings = _bindings.ToList();
            foreach (var (id, chord) in bindings)
            {
                var vk = chord.WindowsVirtualKey();
                if (vk == null || !chord.IsValid)
                    continue;
                if (RegisterHotKey(_hwnd, id, chord.WindowsModifiers(), vk.Value))
                    ok.Add(id);
                else
                    Log.Error($"Could not register {chord.Display()} ({Marshal.GetLastWin32Error()})");
            }
        }
        lock (_gate)
        {
            _registered.Clear();
            foreach (var id in ok)
                _registered.Add(id);
        }
    }

    private void RebindMac()
    {
        List<(int Id, HotkeyChord Chord)> bindings;
        lock (_gate)
            bindings = _bindings.ToList();
        var ok = MacHotkeys.Replace(bindings);
        lock (_gate)
        {
            _registered.Clear();
            foreach (var id in ok)
                _registered.Add(id);
        }
    }

    private void Fire(int id)
    {
        Action? action;
        lock (_gate)
            _actions.TryGetValue(id, out action);
        action?.Invoke();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out Msg msg, IntPtr hwnd, uint min, uint max);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out Msg msg, IntPtr hwnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg msg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg msg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}

internal static class MacHotkeys
{
    private const string Toolbox = "/System/Library/Frameworks/Carbon.framework/Frameworks/HIToolbox.framework/HIToolbox";
    private static readonly CarbonHandler HandlerDelegate = Handle;
    private static Action<int>? _onHotkey;
    private static bool _installed;
    private static readonly List<IntPtr> Refs = [];

    public static void Install(Action<int> onHotkey)
    {
        _onHotkey = onHotkey;
        if (_installed)
            return;
        var spec = new EventTypeSpec { EventClass = 0x6B657962, EventKind = 5 };
        var status = InstallEventHandler(GetApplicationEventTarget(), HandlerDelegate, 1, [spec], IntPtr.Zero, out _);
        _installed = status == 0;
        if (!_installed)
            Log.Error("Carbon hotkey handler was not installed: " + status);
    }

    public static HashSet<int> Replace(IReadOnlyList<(int Id, HotkeyChord Chord)> bindings)
    {
        Clear();
        var ok = new HashSet<int>();
        foreach (var (id, chord) in bindings)
        {
            var code = chord.MacKeyCode();
            if (code == null || !chord.IsValid)
                continue;
            var hotkeyId = new EventHotKeyId { Signature = 0x41495252, Id = (uint)id };
            var status = RegisterEventHotKey(code.Value, chord.MacModifiers(), hotkeyId, GetApplicationEventTarget(), 0, out var refs);
            if (status == 0 && refs != IntPtr.Zero)
            {
                Refs.Add(refs);
                ok.Add(id);
            }
            else
            {
                Log.Error($"Could not register {chord.Display()} ({status})");
            }
        }
        return ok;
    }

    public static void Clear()
    {
        foreach (var refs in Refs)
            UnregisterEventHotKey(refs);
        Refs.Clear();
    }

    private static int Handle(IntPtr next, IntPtr evt, IntPtr user)
    {
        var status = GetEventParameter(evt, 0x2D2D2D2D, 0x686B6964, IntPtr.Zero, Marshal.SizeOf<EventHotKeyId>(), IntPtr.Zero, out var id);
        if (status == 0)
            _onHotkey?.Invoke((int)id.Id);
        return 0;
    }

    private delegate int CarbonHandler(IntPtr next, IntPtr evt, IntPtr user);

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyId
    {
        public uint Signature;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [DllImport(Toolbox)]
    private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyId id, IntPtr target, uint options, out IntPtr hotkey);

    [DllImport(Toolbox)]
    private static extern int UnregisterEventHotKey(IntPtr hotkey);

    [DllImport(Toolbox)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(Toolbox)]
    private static extern int InstallEventHandler(IntPtr target, CarbonHandler handler, int count, EventTypeSpec[] types, IntPtr user, out IntPtr handlerRef);

    [DllImport(Toolbox)]
    private static extern int GetEventParameter(IntPtr evt, uint name, uint type, IntPtr actualType, nint size, IntPtr actualSize, out EventHotKeyId data);
}
