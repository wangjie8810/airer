using System.Runtime.InteropServices;

namespace Airer;

public static class MacWindow
{
    private const nuint CanJoinAllSpaces = 1u << 0;
    private const nuint Stationary = 1u << 4;
    private const nuint IgnoresCycle = 1u << 6;
    private const nuint FullScreenAuxiliary = 1u << 8;
    private const nint FloatingLevel = 3;

    public static void Configure(IntPtr view)
    {
        if (!OperatingSystem.IsMacOS())
            return;
        var window = Window(view);
        if (window == IntPtr.Zero)
            return;
        ObjC.SendNint(window, ObjC.Sel("setLevel:"), FloatingLevel);
        ObjC.SendNuint(window, ObjC.Sel("setCollectionBehavior:"), CanJoinAllSpaces | Stationary | IgnoresCycle | FullScreenAuxiliary);
        SetIgnoresMouse(view, true);
    }

    public static void SetIgnoresMouse(IntPtr view, bool ignore)
    {
        if (!OperatingSystem.IsMacOS())
            return;
        var window = Window(view);
        if (window == IntPtr.Zero)
            return;
        ObjC.SendBool(window, ObjC.Sel("setIgnoresMouseEvents:"), ignore);
    }

    private static IntPtr Window(IntPtr view)
    {
        if (view == IntPtr.Zero)
            return IntPtr.Zero;
        var window = ObjC.Send(view, ObjC.Sel("window"));
        return window == IntPtr.Zero ? view : window;
    }
}

internal static class ObjC
{
    [DllImport("/usr/lib/libobjc.A.dylib")]
    public static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    public static extern IntPtr sel_registerName(string name);

    public static IntPtr Class(string name) => objc_getClass(name);
    public static IntPtr Sel(string name) => sel_registerName(name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send1(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr Send2(IntPtr receiver, IntPtr selector, IntPtr a, IntPtr b);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr SendNint(IntPtr receiver, IntPtr selector, nint arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern nint SendRetNint(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern void SendBool(IntPtr receiver, IntPtr selector, bool arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern void SendNuint(IntPtr receiver, IntPtr selector, nuint arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr SendBytes(IntPtr receiver, IntPtr selector, IntPtr bytes, nint length);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr SendCString(IntPtr receiver, IntPtr selector, string text);
}
