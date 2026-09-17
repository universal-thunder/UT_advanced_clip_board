namespace AdvancedClipboard;

internal static class PasteLog
{
    internal static void Write(string message)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "paste-diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 512000) File.WriteAllText(path, "");
            File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
