using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace AdvancedClipboard;

internal sealed class ClipboardSnapshot
{
    private readonly DataObject? _data;
    private ClipboardSnapshot(DataObject? data) => _data = data;

    internal static ClipboardSnapshot Capture()
    {
        IDataObject? source;
        try { source = Clipboard.GetDataObject(); }
        catch { return new ClipboardSnapshot(null); }
        if (source is null) return new ClipboardSnapshot(null);

        var clone = new DataObject();
        var added = false;
        foreach (var format in source.GetFormats(false))
        {
            try
            {
                var value = source.GetData(format, false);
                switch (value)
                {
                    case string text:
                        clone.SetData(format, false, text); added = true; break;
                    case string[] strings:
                        clone.SetData(format, false, strings.ToArray()); added = true; break;
                    case byte[] bytes:
                        clone.SetData(format, false, bytes.ToArray()); added = true; break;
                    case MemoryStream stream:
                        clone.SetData(format, false, new MemoryStream(stream.ToArray())); added = true; break;
                    case Bitmap bitmap:
                        clone.SetData(format, false, new Bitmap(bitmap)); added = true; break;
                    case Image image:
                        clone.SetData(format, false, new Bitmap(image)); added = true; break;
                }
            }
            catch { }
        }
        return new ClipboardSnapshot(added ? clone : null);
    }

    internal void Restore()
    {
        ClipboardRetry(() =>
        {
            if (_data is null) Clipboard.Clear();
            else Clipboard.SetDataObject(_data, true);
        });
    }

    internal static void Set(ClipboardPayload payload, SlotMode mode = SlotMode.Copy) =>
        ClipboardRetry(() => Clipboard.SetDataObject(payload.ToDataObject(mode), true));

    internal static ClipboardPayload? ReadPayload()
    {
        IDataObject? data = null;
        ClipboardRetry(() => data = Clipboard.GetDataObject());
        return ClipboardPayload.Capture(data);
    }

    private static void ClipboardRetry(Action action)
    {
        Exception? last = null;
        for (var i = 0; i < 8; i++)
        {
            try { action(); return; }
            catch (ExternalException ex) { last = ex; Thread.Sleep(35 + i * 20); }
        }
        if (last is not null) throw last;
    }
}

internal static class ExplorerLocator
{
    internal static string[] SelectedItemsFor(IntPtr hwnd)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) return [];
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                // Only accept the foreground Explorer window (or a child that
                // belongs to its same top-level window). Looking through other
                // Explorer windows can return an old selection from another tab.
                var bestScore = int.MinValue;
                string[] bestPaths = [];
                var targetRoot = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
                foreach (dynamic window in shell.Windows())
                {
                    try
                    {
                        var windowHwnd = new IntPtr((long)window.HWND);
                        dynamic selected = window.Document.SelectedItems();
                        var paths = new List<string>();
                        try
                        {
                            foreach (dynamic item in selected)
                            {
                                try
                                {
                                    string path = item.Path;
                                    if ((File.Exists(path) || Directory.Exists(path)) &&
                                        !paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                                        paths.Add(path);
                                }
                                catch { }
                            }
                        }
                        finally
                        {
                            if (Marshal.IsComObject(selected)) Marshal.FinalReleaseComObject(selected);
                        }
                        if (paths.Count == 0) continue;

                        var score = 0;
                        if (windowHwnd == hwnd) score = 100;
                        else if (targetRoot != IntPtr.Zero &&
                                 NativeMethods.GetAncestor(windowHwnd, NativeMethods.GA_ROOT) == targetRoot)
                            score = 80;
                        if (score == 0) continue;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPaths = paths.ToArray();
                        }
                    }
                    catch { }
                }
                return bestPaths;
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }
        catch { }
        return [];
    }

    internal static string? DestinationFor(IntPtr hwnd)
    {
        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var desktop = NativeMethods.GetShellWindow();
            var targetRoot = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
            var targetClass = NativeMethods.GetClassName(hwnd);
            var rootClass = NativeMethods.GetClassName(targetRoot);
            if (IsDesktopWindow(hwnd, desktop) || IsDesktopClass(targetClass) || IsDesktopClass(rootClass))
                return desktopPath;

            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) return null;
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                string? bestPath = null;
                var bestScore = 0;
                foreach (dynamic window in shell.Windows())
                {
                    try
                    {
                        var windowHwnd = new IntPtr((long)window.HWND);
                        var score = windowHwnd == hwnd ? 100 :
                            targetRoot != IntPtr.Zero && NativeMethods.GetAncestor(windowHwnd, NativeMethods.GA_ROOT) == targetRoot ? 80 : 0;
                        if (score <= bestScore) continue;
                        var path = new Uri((string)window.LocationURL).LocalPath;
                        if (!Directory.Exists(path)) continue;
                        bestScore = score;
                        bestPath = path;
                    }
                    catch { }
                }
                if (bestPath is not null) return bestPath;

                return null;
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }
        catch { }
        return null;
    }

    private static bool IsDesktopClass(string value) => value is
        "Progman" or "WorkerW" or "SHELLDLL_DefView" or "SysListView32";

    private static bool IsDesktopWindow(IntPtr hwnd, IntPtr shellWindow)
    {
        for (var current = hwnd; current != IntPtr.Zero; current = NativeMethods.GetParent(current))
        {
            if (current == shellWindow || IsDesktopClass(NativeMethods.GetClassName(current))) return true;
        }
        return false;
    }
}
