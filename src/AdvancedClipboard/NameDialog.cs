namespace AdvancedClipboard;

internal sealed class NameDialog : Form
{
    private readonly TextBox _name = new();
    private readonly Label _error = new();
    private readonly Func<string, bool> _exists;
    internal string ResultName => _name.Text.Trim();
    internal bool UsedDefaultName { get; private set; }

    internal NameDialog(int slotNumber, SlotMode mode, string type, Func<string, bool> exists, string? existingName = null)
    {
        _exists = exists;
        Text = existingName is null ? $"为{(mode == SlotMode.Copy ? "复制" : "剪切")}内容命名" : "重命名槽位";
        Font = new Font("Microsoft YaHei UI", 10f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(430, 205);

        var title = new Label
        {
            Text = existingName is null ? $"内容将存入 {slotNumber} 号槽" : $"重命名 {slotNumber} 号槽",
            Font = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(24, 22)
        };
        var detail = new Label
        {
            Text = existingName is null ? $"类型：{type}　方式：{(mode == SlotMode.Copy ? "复制" : "剪切")}" : "名称不能为空，也不能与其他槽位重复。",
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(25, 59)
        };
        _name.SetBounds(25, 91, 380, 32);
        _name.Font = new Font("Microsoft YaHei UI", 11f);
        _name.Text = existingName ?? slotNumber.ToString();
        _name.SelectAll();
        _name.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            Confirm(existingName is null ? slotNumber.ToString() : existingName);
        };
        _error.AutoSize = true;
        _error.ForeColor = Theme.Danger;
        _error.Location = new Point(25, 128);

        var ok = Theme.Button("确定", primary: true);
        ok.SetBounds(314, 157, 91, 36);
        ok.AutoSize = false;
        ok.Click += (_, _) => Confirm(existingName is null ? slotNumber.ToString() : existingName);
        var cancel = Theme.Button("取消");
        cancel.SetBounds(215, 157, 91, 36);
        cancel.AutoSize = false;
        cancel.DialogResult = DialogResult.Cancel;

        Controls.AddRange([title, detail, _name, _error, cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) =>
        {
            TopMost = true;
            Activate();
            BringToFront();
            _name.Focus();
            _name.SelectAll();
        };
    }

    private void Confirm(string defaultName)
    {
        var value = _name.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            _error.Text = "名称不能为空。";
            _name.Focus();
            return;
        }
        if (value.Any(char.IsControl))
        {
            _error.Text = "名称不能包含控制字符。";
            return;
        }
        if (_exists(value))
        {
            _error.Text = "该名称已经存在，请换一个名称。";
            _name.SelectAll();
            _name.Focus();
            return;
        }
        UsedDefaultName = string.Equals(value, defaultName, StringComparison.Ordinal);
        DialogResult = DialogResult.OK;
        Close();
    }
}
