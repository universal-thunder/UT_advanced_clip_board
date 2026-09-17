using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using System.Text;

namespace AdvancedClipboard;

internal enum SlotMode { Copy, Cut }

internal sealed class ClipboardPayload
{
    public string[] Files { get; init; } = [];
    public string? Text { get; init; }
    public string? Rtf { get; init; }
    public string? Html { get; init; }
    public byte[]? Png { get; init; }

    public bool IsFilePayload => Files.Length > 0;
    public bool IsImagePayload => Png is { Length: > 0 } && Files.Length == 0;
    public bool IsTextPayload => !string.IsNullOrEmpty(Text) || !string.IsNullOrEmpty(Rtf) || !string.IsNullOrEmpty(Html);
    public bool IsEmpty => !IsFilePayload && !IsImagePayload && !IsTextPayload;

    public DataObject ToDataObject(SlotMode mode = SlotMode.Copy)
    {
        var data = new DataObject();
        if (Files.Length > 0)
        {
            var collection = new StringCollection();
            collection.AddRange(Files);
            data.SetFileDropList(collection);
            data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(mode == SlotMode.Cut ? 2 : 1)));
        }
        if (!string.IsNullOrEmpty(Text)) data.SetText(Text, TextDataFormat.UnicodeText);
        if (!string.IsNullOrEmpty(Rtf)) data.SetText(Rtf, TextDataFormat.Rtf);
        if (!string.IsNullOrEmpty(Html)) data.SetText(Html, TextDataFormat.Html);
        if (Png is { Length: > 0 })
        {
            using var stream = new MemoryStream(Png);
            using var source = Image.FromStream(stream);
            data.SetImage(new Bitmap(source));
        }
        data.SetData("CanIncludeInClipboardHistory", false);
        data.SetData("CanUploadToCloudClipboard", false);
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", true);
        return data;
    }

    public static ClipboardPayload? Capture(IDataObject? data)
    {
        if (data is null) return null;
        try
        {
            string[] files = [];
            if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] paths)
                files = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            string? text = SafeString(data, DataFormats.UnicodeText) ?? SafeString(data, DataFormats.Text);
            string? rtf = SafeString(data, DataFormats.Rtf);
            string? html = SafeString(data, DataFormats.Html);
            byte[]? png = null;
            if (data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is Image image)
            {
                using var output = new MemoryStream();
                image.Save(output, ImageFormat.Png);
                png = output.ToArray();
            }
            var result = new ClipboardPayload { Files = files, Text = text, Rtf = rtf, Html = html, Png = png };
            return result.IsEmpty ? null : result;
        }
        catch { return null; }
    }

    private static string? SafeString(IDataObject data, string format)
    {
        try { return data.GetDataPresent(format) ? data.GetData(format) as string : null; }
        catch { return null; }
    }
}

internal sealed class ClipboardSlot
{
    public required string Name { get; set; }
    public required string ContentType { get; init; }
    public required SlotMode Mode { get; init; }
    public required ClipboardPayload Payload { get; init; }
    public required string Source { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public bool AutoName { get; set; }

    public string ModeText => Mode == SlotMode.Copy ? "复制" : "剪切";
    public string Status
    {
        get
        {
            if (!Payload.IsFilePayload) return "可用";
            return Payload.Files.All(p => File.Exists(p) || Directory.Exists(p)) ? "可用" : "来源失效";
        }
    }

    public string Preview
    {
        get
        {
            if (Payload.IsFilePayload) return string.Join(Environment.NewLine, Payload.Files);
            if (Payload.IsImagePayload) return $"PNG图片，{Payload.Png!.Length / 1024:N0} KB";
            var value = Payload.Text ?? StripHtml(Payload.Html ?? "") ?? "富文本";
            value = Regex.Replace(value, @"\s+", " ").Trim();
            return value.Length > 280 ? value[..280] + "…" : value;
        }
    }

    private static string? StripHtml(string html) =>
        string.IsNullOrWhiteSpace(html) ? null : Regex.Replace(html, "<[^>]+>", " ");
}

internal static class ContentClassifier
{
    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
        { "chrome", "msedge", "firefox", "opera", "brave" };

    public static string Infer(ClipboardPayload payload, string sourceProcess)
    {
        if (payload.Files.Length > 1) return "文件组";
        if (payload.Files.Length == 1)
        {
            var path = payload.Files[0];
            if (Directory.Exists(path)) return "文件夹";
            var ext = Path.GetExtension(path);
            return string.IsNullOrWhiteSpace(ext) ? "文件" : ext.ToLowerInvariant();
        }
        if (payload.IsImagePayload) return Browsers.Contains(sourceProcess) ? "网页图片" : "图片";
        if (payload.IsTextPayload)
        {
            var text = (payload.Text ?? "").Trim();
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return "网址";
            if (Browsers.Contains(sourceProcess) && !string.IsNullOrEmpty(payload.Html)) return "网页文本";
            if (LooksLikeTable(text)) return "表格数据";
            if (!string.IsNullOrEmpty(payload.Rtf) || !string.IsNullOrEmpty(payload.Html)) return "富文本";
            return "纯文本";
        }
        return "应用专用数据";
    }

    private static bool LooksLikeTable(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('\t')) return false;
        var rows = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return rows.Length >= 1 && rows.Count(r => r.Contains('\t')) >= Math.Max(1, rows.Length / 2);
    }
}

internal sealed class SlotStore
{
    private readonly ClipboardSlot?[] _slots = new ClipboardSlot?[10];
    public event EventHandler? Changed;
    public IReadOnlyList<ClipboardSlot?> Slots => _slots;
    public int Count => _slots.Count(x => x is not null);
    public bool IsFull => Count == 10;
    public int NextIndex => Array.FindIndex(_slots, x => x is null) is var i && i >= 0 ? i : 9;

    public ClipboardSlot? this[int index] => index is >= 0 and < 10 ? _slots[index] : null;

    public bool NameExists(string name, int exceptIndex = -1) =>
        _slots.Select((slot, index) => (slot, index)).Any(x => x.index != exceptIndex && x.slot is not null &&
            string.Equals(Normalize(x.slot.Name), Normalize(name), StringComparison.OrdinalIgnoreCase));

    public void Put(int index, ClipboardSlot slot)
    {
        _slots[index] = slot;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(IEnumerable<int> indices)
    {
        foreach (var index in indices.Distinct()) if (index is >= 0 and < 10) _slots[index] = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Array.Clear(_slots);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Compact()
    {
        var kept = _slots.Where(x => x is not null).Cast<ClipboardSlot>().ToArray();
        Array.Clear(_slots);
        for (var i = 0; i < kept.Length; i++) _slots[i] = kept[i];
        for (var i = 0; i < kept.Length; i++)
        {
            var desired = (i + 1).ToString();
            if (kept[i].AutoName && !NameExists(desired, i)) kept[i].Name = desired;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Rename(int index, string name)
    {
        if (_slots[index] is not { } slot) return;
        slot.Name = name.Trim();
        slot.AutoName = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Normalize(string value) => value.Trim().Normalize(NormalizationForm.FormKC);
}
