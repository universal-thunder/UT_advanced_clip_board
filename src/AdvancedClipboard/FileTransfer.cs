using Microsoft.VisualBasic.FileIO;

namespace AdvancedClipboard;

internal static class FileTransfer
{
    internal static bool Execute(IEnumerable<ClipboardSlot> slots, string destination, IWin32Window owner, out List<ClipboardSlot> completedCuts)
    {
        completedCuts = [];
        var allSucceeded = true;
        if (File.Exists(destination)) destination = Path.GetDirectoryName(destination) ?? destination;
        if (!Directory.Exists(destination))
        {
            MessageBox.Show(owner, "粘贴目标文件夹不存在。", "高级粘贴", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        foreach (var slot in slots)
        {
            var slotSucceeded = true;
            foreach (var source in slot.Payload.Files)
            {
                if (!File.Exists(source) && !Directory.Exists(source)) { slotSucceeded = false; allSucceeded = false; continue; }
                try
                {
                    var isDirectory = Directory.Exists(source);
                    var name = isDirectory
                        ? new DirectoryInfo(source).Name
                        : Path.GetFileName(source);
                    var target = Path.Combine(destination, name);
                    var samePath = string.Equals(Path.GetFullPath(source).TrimEnd('\\'),
                        Path.GetFullPath(target).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
                    if (slot.Mode == SlotMode.Copy &&
                        (samePath || File.Exists(target) || Directory.Exists(target)))
                    {
                        target = UniqueCopyTarget(destination, name, isDirectory);
                    }
                    else if (samePath)
                    {
                        slotSucceeded = false;
                        allSucceeded = false;
                        continue;
                    }

                    if (isDirectory)
                    {
                        if (slot.Mode == SlotMode.Cut) FileSystem.MoveDirectory(source, target, UIOption.AllDialogs);
                        else FileSystem.CopyDirectory(source, target, UIOption.AllDialogs);
                    }
                    else
                    {
                        if (slot.Mode == SlotMode.Cut) FileSystem.MoveFile(source, target, UIOption.AllDialogs);
                        else FileSystem.CopyFile(source, target, UIOption.AllDialogs);
                    }
                }
                catch (OperationCanceledException) { slotSucceeded = false; allSucceeded = false; break; }
                catch (Exception ex)
                {
                    slotSucceeded = false;
                    allSucceeded = false;
                    MessageBox.Show(owner, $"处理“{source}”失败：\n{ex.Message}", "高级粘贴",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                }
            }
            if (slotSucceeded && slot.Mode == SlotMode.Cut) completedCuts.Add(slot);
        }
        return allSucceeded;
    }

    private static string UniqueCopyTarget(string destination, string name, bool isDirectory)
    {
        var stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        var extension = isDirectory ? "" : Path.GetExtension(name);
        var number = 1;
        var candidate = Path.Combine(destination, $"{stem}-副本（{number++}）{extension}");
        while (File.Exists(candidate) || Directory.Exists(candidate))
            candidate = Path.Combine(destination, $"{stem}-副本（{number++}）{extension}");
        return candidate;
    }
}
