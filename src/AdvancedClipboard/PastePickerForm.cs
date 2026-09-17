namespace AdvancedClipboard;

internal sealed class PastePickerForm : Form
{
    private readonly CheckedListBox _items = new();
    private readonly Label _hint = new();
    private readonly CheckBox _pasteToDesktop = new();
    private readonly List<(int Index, ClipboardSlot Slot)> _available;
    internal IReadOnlyList<int> SelectedIndices { get; private set; } = [];
    internal bool PasteToDesktop => _pasteToDesktop.Checked;

    internal PastePickerForm(SlotStore store, bool filesOnly = false)
    {
        _available = store.Slots.Select((slot, index) => (Index: index, Slot: slot))
            .Where(x => x.Slot is not null && (!filesOnly || x.Slot.Payload.IsFilePayload))
            .Select(x => (x.Index, x.Slot!)).ToList();

        Text = "选择要粘贴的内容";
        Font = new Font("Microsoft YaHei UI", 9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(560, 430);

        var title = new Label { Text = "高级粘贴", Font = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold), AutoSize = true, Location = new Point(22, 18) };
        var subtitle = new Label { Text = "可多选兼容内容；粘贴顺序按照槽号排列。", ForeColor = Theme.Muted, AutoSize = true, Location = new Point(24, 55) };
        _items.SetBounds(24, 86, 512, 250);
        _items.BorderStyle = BorderStyle.FixedSingle;
        _items.CheckOnClick = true;
        _items.Font = new Font("Microsoft YaHei UI", 10f);
        foreach (var x in _available)
            _items.Items.Add($"{x.Index + 1,2}　{x.Slot.Name,-16}　{x.Slot.ContentType,-8}　{x.Slot.ModeText}");
        _items.DoubleClick += (_, _) => { if (_items.SelectedIndex >= 0) { ClearAndCheck(_items.SelectedIndex); Confirm(); } };
        _pasteToDesktop.Text = "粘贴到桌面";
        _pasteToDesktop.SetBounds(24, 343, 140, 25);
        _pasteToDesktop.AutoSize = false;
        _hint.SetBounds(168, 343, 368, 25);
        _hint.ForeColor = Theme.Danger;

        var paste = Theme.Button("粘贴", primary: true);
        paste.SetBounds(422, 378, 114, 36);
        paste.AutoSize = false;
        paste.Click += (_, _) => Confirm();
        var cancel = Theme.Button("取消");
        cancel.SetBounds(320, 378, 94, 36);
        cancel.AutoSize = false;
        cancel.DialogResult = DialogResult.Cancel;
        Controls.AddRange([title, subtitle, _items, _pasteToDesktop, _hint, cancel, paste]);
        AcceptButton = paste;
        CancelButton = cancel;
        Shown += (_, _) =>
        {
            TopMost = true;
            Activate();
            BringToFront();
        };
    }

    private void ClearAndCheck(int selected)
    {
        for (var i = 0; i < _items.Items.Count; i++) _items.SetItemChecked(i, i == selected);
    }

    private void Confirm()
    {
        var indices = _items.CheckedIndices.Cast<int>().Select(i => _available[i].Index).Order().ToArray();
        if (indices.Length == 0) { _hint.Text = "请至少选择一个槽位。"; return; }
        var slots = indices.Select(i => _available.First(x => x.Index == i).Slot).ToArray();
        if (!AreCompatible(slots, out var reason)) { _hint.Text = reason; return; }
        if (PasteToDesktop && slots.Any(x => !x.Payload.IsFilePayload))
        {
            _hint.Text = "粘贴到桌面仅支持文件或文件夹。";
            return;
        }
        SelectedIndices = indices;
        DialogResult = DialogResult.OK;
        Close();
    }

    internal static bool AreCompatible(IReadOnlyList<ClipboardSlot> slots, out string reason)
    {
        reason = "";
        if (slots.Count <= 1) return true;
        if (slots.All(x => x.Payload.IsFilePayload)) return true;
        if (slots.All(x => x.Payload.IsTextPayload && !x.Payload.IsFilePayload && !x.Payload.IsImagePayload)) return true;
        if (slots.All(x => x.Payload.IsImagePayload)) return true;
        reason = "文件、文字和图片不能在一次操作中混合粘贴。";
        return false;
    }
}
