using System.Runtime.InteropServices;

namespace ChessViewer;

// When launched via `dotnet run` (rather than double-clicked from Finder/the .app bundle),
// macOS does not automatically make the process the frontmost app — the window opens behind
// whatever was already focused and needs a Dock click to appear. NSApplication.activate is a
// self-activation call, not remote-control of another process, so unlike AppleScript/System
// Events automation it needs no Accessibility/Automation permission grant.
internal static class MacActivation
{
    private const string ObjCRuntime = "/usr/lib/libobjc.dylib";

    [DllImport(ObjCRuntime)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(ObjCRuntime)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_get(IntPtr receiver, IntPtr selector);

    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(IntPtr receiver, IntPtr selector, bool arg);

    public static void ActivateIfMacOS()
    {
        if (!OperatingSystem.IsMacOS()) return;

        IntPtr nsApplicationClass = objc_getClass("NSApplication");
        IntPtr sharedApplication  = objc_msgSend_get(nsApplicationClass, sel_registerName("sharedApplication"));
        objc_msgSend_bool(sharedApplication, sel_registerName("activateIgnoringOtherApps:"), true);
    }
}
