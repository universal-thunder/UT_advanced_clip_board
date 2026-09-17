using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

internal static class InstallEngine
{
    internal const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AdvancedClipboard.User";
    internal const string Marker = "AdvancedClipboard.InstallManifest.v1";
    internal const string ManifestName = "install-manifest.txt";

    internal static string NormalizeRoot(string path)
    {
        string root = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        if (root.Length < 4 || string.Equals(root, Path.GetPathRoot(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("请选择独立的程序文件夹，不能直接安装到磁盘根目录。");
        return root;
    }

    internal static void EnsureStopped(string root)
    {
        foreach (Process process in Process.GetProcessesByName("AdvancedClipboard"))
        {
            using (process)
            {
                try
                {
                    if (string.Equals(Path.GetDirectoryName(process.MainModule.FileName), root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("请先关闭此目录中的高级剪贴板，再继续操作。关闭会清空槽位。");
                }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
    }

    internal static void Install(string destination, string shortcutFolder, bool register, Action<int, string> progress)
    {
        string root = NormalizeRoot(destination);
        EnsureStopped(root);
        if (Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length != 0)
            throw new InvalidOperationException("安装目录必须为空，以免覆盖已有文件。请选择新的独立文件夹；升级请先卸载旧版。");
        if (File.Exists(root)) throw new InvalidOperationException("安装位置是文件，请选择文件夹。");
        if (register)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                if (key != null) throw new InvalidOperationException("已存在安装版，请先从 Windows 设置中卸载旧版。");
        }
        string shortcut = string.IsNullOrEmpty(shortcutFolder) ? "" : Path.Combine(Path.GetFullPath(shortcutFolder), "UTboard.lnk");
        if (!string.IsNullOrEmpty(shortcut) && !Directory.Exists(Path.GetDirectoryName(shortcut)))
            throw new InvalidOperationException("快捷方式目录不存在，请重新选择。");
        byte[] previousShortcut = File.Exists(shortcut) ? File.ReadAllBytes(shortcut) : null;
        bool createdRoot = !Directory.Exists(root), shortcutWritten = false, registered = false;
        List<string> written = new List<string>();
        try
        {
            Directory.CreateDirectory(root);
            using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            {
                if (payload == null) throw new InvalidOperationException("安装包缺少程序主体。");
                using (ZipArchive archive = new ZipArchive(payload, ZipArchiveMode.Read))
                {
                    int count = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string file = OwnedPath(root, entry.FullName);
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(file));
                        // Record before opening: a failed extraction can leave a partial file.
                        written.Add(entry.FullName);
                        using (Stream input = entry.Open())
                        using (FileStream output = new FileStream(file, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
                        progress(++count * 90 / archive.Entries.Count, entry.Name);
                    }
                }
            }
            if (!File.Exists(Path.Combine(root, "AdvancedClipboard.exe")) || !File.Exists(Path.Combine(root, "coreclr.dll")))
                throw new InvalidOperationException("程序主体或自带运行环境不完整。");
            if (!string.IsNullOrEmpty(shortcut))
            {
                shortcutWritten = true;
                WriteShortcut(shortcut, root);
            }
            List<string> manifest = new List<string> { Marker, shortcut };
            manifest.AddRange(written);
            File.WriteAllLines(Path.Combine(root, ManifestName), manifest.ToArray(), Encoding.UTF8);
            written.Add(ManifestName);
            if (register)
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    registered = true;
                    key.SetValue("DisplayName", "高级剪贴板 AdvancedClipboard");
                    key.SetValue("DisplayVersion", "0.1.0-beta");
                    key.SetValue("Publisher", "AdvancedClipboard contributors");
                    key.SetValue("InstallLocation", root);
                    key.SetValue("DisplayIcon", Path.Combine(root, "AdvancedClipboard.exe"));
                    key.SetValue("UninstallString", Quote(Path.Combine(root, "Uninstall.exe")));
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    long bytes = 0;
                    foreach (string relative in written) bytes += new FileInfo(OwnedPath(root, relative)).Length;
                    key.SetValue("EstimatedSize", (int)(bytes / 1024), RegistryValueKind.DWord);
                }
            }
            progress(100, "安装完成");
        }
        catch
        {
            if (registered) RemoveRegistration(root);
            if (shortcutWritten)
            {
                if (previousShortcut != null) File.WriteAllBytes(shortcut, previousShortcut);
                else if (File.Exists(shortcut)) File.Delete(shortcut);
            }
            foreach (string relative in written)
                try { File.Delete(OwnedPath(root, relative)); } catch { }
            try
            {
                List<string> rollbackFiles = new List<string>();
                foreach (string relative in written) rollbackFiles.Add(OwnedPath(root, relative));
                PruneEmptyParents(root, rollbackFiles);
            }
            catch { }
            if (createdRoot && Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root);
            throw;
        }
    }

    internal static void Uninstall(string destination, bool register)
    {
        string root = NormalizeRoot(destination);
        string manifestFile = Path.Combine(root, ManifestName);
        string[] lines = File.ReadAllLines(manifestFile, Encoding.UTF8);
        if (lines.Length < 3 || lines[0] != Marker) throw new InvalidOperationException("缺少有效安装清单，拒绝删除目录。");
        EnsureStopped(root);
        // Validate the complete list before deleting any file. Never recursively remove the directory.
        List<string> files = new List<string>();
        for (int i = 2; i < lines.Length; i++) files.Add(OwnedPath(root, lines[i]));
        foreach (string file in files) File.Delete(file);
        PruneEmptyParents(root, files);
        string shortcut = lines[1];
        if (!string.IsNullOrEmpty(shortcut) && File.Exists(shortcut) && ShortcutTargets(shortcut, root)) File.Delete(shortcut);
        if (register) RemoveRegistration(root);
        File.Delete(manifestFile);
        if (Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root);
    }

    private static void PruneEmptyParents(string root, IEnumerable<string> files)
    {
        HashSet<string> parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in files)
        {
            string parent = Path.GetDirectoryName(file);
            while (parent.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                parents.Add(parent);
                parent = Path.GetDirectoryName(parent);
            }
        }
        List<string> ordered = new List<string>(parents);
        ordered.Sort(delegate(string a, string b) { return b.Length.CompareTo(a.Length); });
        foreach (string parent in ordered)
            if (Directory.Exists(parent) && Directory.GetFileSystemEntries(parent).Length == 0) Directory.Delete(parent);
    }

    internal static string OwnedPath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new InvalidOperationException("安装清单包含无效路径。");
        string full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("安装清单中的路径超出安装目录。");
        return full;
    }

    private static void RemoveRegistration(string root)
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            if (key == null || !string.Equals(key.GetValue("InstallLocation") as string, root, StringComparison.OrdinalIgnoreCase)) return;
        Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
    }

    private static void WriteShortcut(string path, string root)
    {
        object shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            dynamic dispatch = shell;
            shortcut = dispatch.CreateShortcut(path);
            dynamic link = shortcut;
            link.TargetPath = Path.Combine(root, "AdvancedClipboard.exe");
            link.Arguments = "--show";
            link.WorkingDirectory = root;
            link.Description = "高级剪贴板 — 十个命名复制/剪切槽位";
            link.IconLocation = Path.Combine(root, "AdvancedClipboard.exe") + ",0";
            link.Save();
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }

    private static bool ShortcutTargets(string path, string root)
    {
        object shell = null, shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            dynamic dispatch = shell;
            shortcut = dispatch.CreateShortcut(path);
            dynamic link = shortcut;
            return string.Equals((string)link.TargetPath, Path.Combine(root, "AdvancedClipboard.exe"), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }

    internal static string Quote(string value)
    {
        if (value.IndexOf('"') >= 0) throw new ArgumentException("路径不能包含引号。");
        return "\"" + value.TrimEnd('\\') + "\"";
    }
}
