using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;

namespace AdvancedClipboard;

internal static class NativeMethods
{
    internal const int WM_HOTKEY = 0x0312;
    internal const uint MOD_ALT = 0x0001;
    internal const uint MOD_CONTROL = 0x0002;
    internal const uint MOD_SHIFT = 0x0004;
    internal const uint MOD_NOREPEAT = 0x4000;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const int VK_CONTROL = 0x11;
    internal const int VK_SHIFT = 0x10;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);
    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hwnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);
    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize; public uint flags;
        public IntPtr active, focus, capture, menuOwner, moveSize, caret;
        public int left, top, right, bottom;
    }
    internal static IntPtr FocusedControl(IntPtr window)
    {
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(GetWindowThreadProcessId(window, out _), ref info) ? info.focus : IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetParent(IntPtr hWnd);

    internal const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int size);

    [DllImport("shell32.dll")]
    internal static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint message, IntPtr wParam, string? lParam,
        uint flags, uint timeout, out IntPtr result);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public INPUTUNION data; }
    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mouse;
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public HARDWAREINPUT hardware;
    }

    // INPUT's native union is 32 bytes on x64 because MOUSEINPUT is its
    // largest member. Omitting it makes Marshal.SizeOf<INPUT>() return 32
    // instead of the required 40 and SendInput fails with ERROR_INVALID_PARAMETER.
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort virtualKey;
        public ushort scanCode;
        public uint flags;
        public uint time;
        public IntPtr extraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint message;
        public ushort parameterLow;
        public ushort parameterHigh;
    }

    internal static void SendCtrlKey(char key)
    {
        var vk = (ushort)char.ToUpperInvariant(key);
        var inputs = new[]
        {
            Key((ushort)VK_CONTROL, false), Key(vk, false), Key(vk, true), Key((ushort)VK_CONTROL, true)
        };
        SendInputs(inputs);
    }

    internal static async Task<bool> RestorePasteTargetAsync(IntPtr target, IntPtr focus = default)
    {
        if (target == IntPtr.Zero || !IsWindow(target)) return false;
        // Never keep input queues attached across an await/message loop.
        SetForegroundWindow(target);
        await Task.Delay(80);
        if (GetForegroundWindow() == target) return true;
        var currentThread = GetCurrentThreadId();
        var targetThread = GetWindowThreadProcessId(target, out _);
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
        var attachedTarget = false;
        var attachedForeground = false;
        try
        {
            if (targetThread != 0 && targetThread != currentThread)
                attachedTarget = AttachThreadInput(currentThread, targetThread, true);
            if (foregroundThread != 0 && foregroundThread != currentThread && foregroundThread != targetThread)
                attachedForeground = AttachThreadInput(currentThread, foregroundThread, true);

            BringWindowToTop(target);
            SetForegroundWindow(target);
            if (focus != IntPtr.Zero && focus != target && IsWindow(focus) &&
                GetWindowThreadProcessId(focus, out _) == targetThread)
                SetFocus(focus);
        }
        finally
        {
            if (attachedForeground) AttachThreadInput(currentThread, foregroundThread, false);
            if (attachedTarget) AttachThreadInput(currentThread, targetThread, false);
        }
        await Task.Delay(100);
        return GetForegroundWindow() == target;
    }

    internal static void SendCtrlShiftKey(char key)
    {
        var vk = (ushort)char.ToUpperInvariant(key);
        var inputs = new[]
        {
            Key((ushort)VK_CONTROL, false), Key((ushort)VK_SHIFT, false), Key(vk, false),
            Key(vk, true), Key((ushort)VK_SHIFT, true), Key((ushort)VK_CONTROL, true)
        };
        SendInputs(inputs);
    }

    private static void SendInputs(INPUT[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Windows未能发送模拟按键（{sent}/{inputs.Length}）。");
    }

    internal static int InputStructureSize => Marshal.SizeOf<INPUT>();

    internal static async Task WaitForCopyHotkeyReleaseAsync(char triggerKey)
    {
        // WM_HOTKEY can arrive while Ctrl/Shift/C/X are still physically down.
        // Sending Ctrl+C in that state becomes Ctrl+Shift+C and Explorer ignores it.
        var trigger = char.ToUpperInvariant(triggerKey);
        for (var i = 0; i < 100; i++)
        {
            if (!IsKeyDown(VK_CONTROL) && !IsKeyDown(VK_SHIFT) && !IsKeyDown(trigger)) return;
            await Task.Delay(15);
        }
    }

    internal static async Task WaitForModifiersReleasedAsync()
    {
        for (var i = 0; i < 100; i++)
        {
            if (!IsKeyDown(VK_CONTROL) && !IsKeyDown(VK_SHIFT)) return;
            await Task.Delay(15);
        }
        throw new InvalidOperationException("请松开 Ctrl 和 Shift 后再粘贴。");
    }

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = 1,
        data = new INPUTUNION { keyboard = new KEYBDINPUT { virtualKey = vk, flags = up ? KEYEVENTF_KEYUP : 0 } }
    };

    internal static (string Process, string Title) ForegroundSource(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            var p = Process.GetProcessById((int)pid);
            return (p.ProcessName, p.MainWindowTitle);
        }
        catch { return ("未知程序", ""); }
    }

    internal static string GetClassName(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        _ = GetClassNameW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

}
