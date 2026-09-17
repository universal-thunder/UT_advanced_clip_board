using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace AdvancedClipboard;

internal static class RegistryInstaller
{
    private const string Classes = @"Software\Classes";

    internal static void Install(string executable)
    {
        executable = Path.GetFullPath(executable);
        RemoveLegacyIntegration();
        CreateDesktopShortcut(executable);
    }

    internal static void Uninstall()
    {
        RemoveLegacyIntegration();
        var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "UTboard.lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
    }

    internal static void RemoveLegacyIntegration()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
            run?.DeleteValue("AdvancedClipboard", false);

        foreach (var parent in new[] { @"*\shell", @"Directory\shell", @"AllFilesystemObjects\shell", @"Directory\Background\shell", @"DesktopBackground\Shell" })
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"{Classes}\{parent}", true);
            foreach (var verb in VerbNames) key?.DeleteSubKeyTree(verb, false);
        }
        RefreshShellMenus();
    }

    private static readonly string[] VerbNames =
        ["AdvancedClipboard.Copy", "AdvancedClipboard.Cut", "AdvancedClipboard.Paste", "AdvancedClipboard.View"];

    private static void CreateDesktopShortcut(string executable)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "UTboard.lnk");
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic shortcut = shell.CreateShortcut(path);
            shortcut.TargetPath = executable;
            shortcut.Arguments = "--show";
            shortcut.WorkingDirectory = Path.GetDirectoryName(executable);
            shortcut.Description = "查看高级剪贴板";
            shortcut.IconLocation = executable + ",0";
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
        catch { }
    }

    private static void RefreshShellMenus()
    {
        NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        NativeMethods.SendMessageTimeout(
            new IntPtr(0xffff), 0x001A, IntPtr.Zero, "Software\\Classes",
            0x0002, 3000, out _);
    }
}
