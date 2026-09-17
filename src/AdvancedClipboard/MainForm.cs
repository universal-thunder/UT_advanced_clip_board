using System.ComponentModel;

namespace AdvancedClipboard;

internal sealed class MainForm : Form
{
    private const uint HotkeyModifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;
    private const int HkCopy = 100, HkCut = 101, HkPaste = 102, HkView = 103, HkDigitBase = 200;
    private readonly SlotStore _store = new();
    private readonly DataGridView _grid = new();
    private readonly TextBox _preview = new();
    private readonly Label _summary = new();
    private readonly Action _exit;
    private readonly System.Windows.Forms.Timer _shellBatchTimer = new() { Interval = 280 };
    private readonly List<string> _shellBatchPaths = [];
    private SlotMode _shellBatchMode;
    private bool _allowClose;
    private bool _busy;
    private IntPtr _lastExternalWindow;
    private IntPtr _lastExternalFocus;

    internal MainForm(Action exit)
    {
        _exit = exit;
        Text = "高级剪贴板";
        Font = new Font("Microsoft YaHei UI", 9.5f);
        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(850, 610);
        ClientSize = new Size(980, 680);
        Icon = SystemIcons.Application;

        BuildUi();
        _store.Changed += (_, _) => RefreshGrid();
        _shellBatchTimer.Tick += (_, _) => FlushShellBatch();
    }

    private void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 105, BackColor = Theme.Surface, Padding = new Padding(24, 16, 24, 10) };
        var title = new Label { Text = "高级剪贴板", Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold), AutoSize = true, Location = new Point(24, 14) };
        var subtitle = new Label
        {
            Text = "Ctrl+Shift+C 复制　 Ctrl+Shift+X 剪切　 Ctrl+Shift+V 选择粘贴　 Ctrl+Shift+B 查看　 Ctrl+Shift+数字 直接粘贴",
            ForeColor = Theme.Muted, AutoSize = true, Location = new Point(27, 59)
        };
        _summary.Text = "0 / 10 个槽位";
        _summary.ForeColor = Theme.Primary;
        _summary.AutoSize = true;
        _summary.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _summary.Location = new Point(ClientSize.Width - 126, 25);
        header.Controls.AddRange([title, subtitle, _summary]);

        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Theme.Surface;
        _grid.BorderStyle = BorderStyle.None;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = true;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.RowTemplate.Height = 38;
        _grid.ColumnHeadersHeight = 40;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.PrimaryLight;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(226, 231, 255);
        _grid.DefaultCellStyle.SelectionForeColor = Theme.Text;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Number", HeaderText = "编号", Width = 62 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "名称", FillWeight = 35, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "文件类型", Width = 125 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "复制/剪切", Width = 105 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "来源", FillWeight = 30, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态", Width = 90 });
        _grid.SelectionChanged += (_, _) => UpdatePreview();
        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && _store[e.RowIndex] is not null)
            {
                Hide();
                _ = PasteIndicesAsync([e.RowIndex], _lastExternalWindow, savedFocus: _lastExternalFocus);
            }
        };

        _preview.Dock = DockStyle.Bottom;
        _preview.Height = 105;
        _preview.Multiline = true;
        _preview.ReadOnly = true;
        _preview.ScrollBars = ScrollBars.Vertical;
        _preview.BackColor = Color.FromArgb(251, 251, 253);
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.ForeColor = Theme.Muted;
        _preview.Font = new Font("Microsoft YaHei UI", 9f);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 14, 22, 0), BackColor = Theme.Background };
        var surface = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(1) };
        surface.Controls.Add(_grid);
        surface.Controls.Add(_preview);
        body.Controls.Add(surface);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 66, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(18, 14, 18, 10), BackColor = Theme.Background, WrapContents = false
        };
        var close = Theme.Button("关闭程序"); close.Click += (_, _) => ConfirmExit();
        var clearAll = Theme.Button("清空全部", danger: true); clearAll.Click += (_, _) => ClearAll();
        var remove = Theme.Button("删除所选", danger: true); remove.Click += (_, _) => RemoveSelected();
        var compact = Theme.Button("整理槽位"); compact.Click += (_, _) => _store.Compact();
        var rename = Theme.Button("重命名"); rename.Click += (_, _) => RenameSelected();
        var paste = Theme.Button("粘贴所选", primary: true); paste.Click += (_, _) => PasteSelected();
        footer.Controls.AddRange([close, clearAll, remove, compact, rename, paste]);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
        Resize += (_, _) => _summary.Left = ClientSize.Width - _summary.Width - 28;
        RefreshGrid();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Register(HkCopy, 'C'); Register(HkCut, 'X'); Register(HkPaste, 'V'); Register(HkView, 'B');
        for (var d = 0; d <= 9; d++) Register(HkDigitBase + d, (char)('0' + d));
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        foreach (var id in new[] { HkCopy, HkCut, HkPaste, HkView }) NativeMethods.UnregisterHotKey(Handle, id);
        for (var d = 0; d <= 9; d++) NativeMethods.UnregisterHotKey(Handle, HkDigitBase + d);
        base.OnHandleDestroyed(e);
    }

    private void Register(int id, char key)
    {
        if (!NativeMethods.RegisterHotKey(Handle, id, HotkeyModifiers, key))
            BeginInvoke(() => MessageBox.Show(this, $"快捷键 Ctrl+Shift+{key} 已被其他程序占用。", "高级剪贴板", MessageBoxButtons.OK, MessageBoxIcon.Warning));
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY)
        {
            if (_busy || OwnedForms.Any(f => f.Visible)) return;
            var id = m.WParam.ToInt32();
            var target = NativeMethods.GetForegroundWindow();
            if (id == HkCopy) _ = CaptureFromFocusedAsync(SlotMode.Copy, target);
            else if (id == HkCut) _ = CaptureFromFocusedAsync(SlotMode.Cut, target);
            else if (id == HkPaste)
                ShowPastePicker(target);
            else if (id == HkView) ShowManager(target);
            else if (id is >= HkDigitBase and <= HkDigitBase + 9)
            {
                var digit = id - HkDigitBase;
                var index = digit == 0 ? 9 : digit - 1;
                _ = PasteIndicesAsync([index], target);
            }
            return;
        }
        base.WndProc(ref m);
    }

    internal void HandleCommand(string[] args)
    {
        if (args.Length == 0) { ShowManager(NativeMethods.GetForegroundWindow()); return; }
        switch (args[0].ToLowerInvariant())
        {
            case "--background": break;
            case "--show": ShowManager(NativeMethods.GetForegroundWindow()); break;
            case "--shell-copy" when args.Length >= 3:
                QueueShellCapture(args[1].Equals("cut", StringComparison.OrdinalIgnoreCase) ? SlotMode.Cut : SlotMode.Copy, args[2]);
                break;
            case "--shell-paste" when args.Length >= 2:
                var target = args[1];
                if (File.Exists(target)) target = Path.GetDirectoryName(target) ?? target;
                ShowPastePicker(NativeMethods.GetForegroundWindow(), target);
                break;
            default: ShowManager(NativeMethods.GetForegroundWindow()); break;
        }
    }

    private async Task CaptureFromFocusedAsync(SlotMode mode, IntPtr target)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var source = NativeMethods.ForegroundSource(target);

            // Explorer exposes concrete filesystem paths. Capture those paths
            // directly so the advanced hotkey is independent of the ordinary
            // Windows clipboard and SendInput restrictions.
            var selectedPaths = ExplorerLocator.SelectedItemsFor(target);
            if (selectedPaths.Length > 0)
            {
                AddPayload(new ClipboardPayload { Files = selectedPaths }, mode, source.Process);
                return;
            }

            var snapshot = ClipboardSnapshot.Capture();
            var sequence = NativeMethods.GetClipboardSequenceNumber();
            await NativeMethods.WaitForCopyHotkeyReleaseAsync(mode == SlotMode.Copy ? 'C' : 'X');
            NativeMethods.SetForegroundWindow(target);
            await Task.Delay(100);
            NativeMethods.SendCtrlKey(mode == SlotMode.Copy ? 'C' : 'X');
            var changed = false;
            for (var i = 0; i < 24; i++)
            {
                await Task.Delay(50);
                if (NativeMethods.GetClipboardSequenceNumber() != sequence) { changed = true; break; }
            }
            if (!changed)
            {
                MessageBox.Show("当前位置没有可复制或剪切的内容。", "高级剪贴板", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var payload = ClipboardSnapshot.ReadPayload();
            snapshot.Restore();
            if (payload is null)
            {
                MessageBox.Show("该软件使用了暂不支持的专用剪贴板格式。", "高级剪贴板", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var added = AddPayload(payload, mode, source.Process);
            if (!added && mode == SlotMode.Cut && !payload.IsFilePayload)
            {
                NativeMethods.SetForegroundWindow(target);
                NativeMethods.SendCtrlKey('Z');
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"操作失败：{ex.Message}", "高级剪贴板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _busy = false; }
    }

    private bool AddPayload(ClipboardPayload payload, SlotMode mode, string source)
    {
        var index = _store.NextIndex;
        if (_store.IsFull)
        {
            var old = _store[9];
            var risk = old?.Mode == SlotMode.Cut && !old.Payload.IsFilePayload
                ? "\n\n10号槽包含已从原位置剪掉的内容，替换后可能无法恢复。" : "";
            if (MessageBox.Show($"高级剪贴板已满。继续后将清除10号槽，并把新内容放入10号槽。{risk}",
                    "槽位已满", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                return false;
        }
        var type = ContentClassifier.Infer(payload, source);
        using var dialog = new NameDialog(index + 1, mode, type, name => _store.NameExists(name, index));
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        _store.Put(index, new ClipboardSlot
        {
            Name = dialog.ResultName,
            AutoName = dialog.UsedDefaultName,
            ContentType = type,
            Mode = mode,
            Payload = payload,
            Source = string.IsNullOrWhiteSpace(source) ? "未知程序" : source
        });
        return true;
    }

    private void QueueShellCapture(SlotMode mode, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path))) return;
        if (_shellBatchPaths.Count > 0 && mode != _shellBatchMode) FlushShellBatch();
        _shellBatchMode = mode;
        if (!_shellBatchPaths.Contains(path, StringComparer.OrdinalIgnoreCase)) _shellBatchPaths.Add(path);
        _shellBatchTimer.Stop();
        _shellBatchTimer.Start();
    }

    private void FlushShellBatch()
    {
        _shellBatchTimer.Stop();
        if (_shellBatchPaths.Count == 0) return;
        var paths = _shellBatchPaths.ToArray();
        _shellBatchPaths.Clear();
        var payload = new ClipboardPayload { Files = paths };
        AddPayload(payload, _shellBatchMode, "explorer");
    }

    private void ShowPastePicker(IntPtr target, string? explicitDestination = null)
    {
        if (_store.Count == 0)
        {
            MessageBox.Show("高级剪贴板为空。", "高级粘贴", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var focus = NativeMethods.FocusedControl(target);
        int[] indices;
        var pasteToDesktop = false;
        using (var picker = new PastePickerForm(_store))
        {
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            indices = picker.SelectedIndices.ToArray();
            pasteToDesktop = picker.PasteToDesktop;
        }
        if (pasteToDesktop)
            explicitDestination = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        // Dispose the modal and unwind WM_HOTKEY before activating another app.
        BeginInvoke(() => _ = PasteIndicesAsync(indices, target, explicitDestination, focus));
    }

    private async Task PasteIndicesAsync(IReadOnlyList<int> indices, IntPtr target, string? explicitDestination = null, IntPtr savedFocus = default)
    {
        if (_busy) return;
        var focus = savedFocus != IntPtr.Zero ? savedFocus : NativeMethods.FocusedControl(target);
        var selected = indices.Distinct().Order().Select(i => (Index: i, Slot: _store[i]))
            .Where(x => x.Slot is not null).Select(x => (x.Index, Slot: x.Slot!)).ToArray();
        if (selected.Length == 0)
        {
            MessageBox.Show("对应槽位为空。", "高级粘贴", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!PastePickerForm.AreCompatible(selected.Select(x => x.Slot).ToArray(), out var reason))
        {
            MessageBox.Show(reason, "高级粘贴", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _busy = true;
        try
        {
            // For Explorer and the desktop, use the concrete Shell destination.
            // This permits deterministic collision names and avoids focus-based
            // key injection. Other applications still receive standard Ctrl+V.
            var directDestination = explicitDestination;
            if (directDestination is null && selected.All(x => x.Slot.Payload.IsFilePayload))
                directDestination = ExplorerLocator.DestinationFor(target);
            if (directDestination is not null && selected.All(x => x.Slot.Payload.IsFilePayload))
            {
                var succeeded = FileTransfer.Execute(selected.Select(x => x.Slot), directDestination, this, out var directCompletedCuts);
                var done = selected.Where(x => directCompletedCuts.Contains(x.Slot)).Select(x => x.Index).ToArray();
                if (done.Length > 0) _store.Remove(done);
                if (succeeded) NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            var snapshot = ClipboardSnapshot.Capture();
            uint? writtenSequence = null;
            try
            {
                await NativeMethods.WaitForModifiersReleasedAsync();
                if (!await NativeMethods.RestorePasteTargetAsync(target, focus))
                {
                    MessageBox.Show("无法回到原来的粘贴位置。请在目标位置重新按一次高级粘贴。",
                        "高级粘贴", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (selected.All(x => x.Slot.Payload.IsTextPayload && !x.Slot.Payload.IsFilePayload && !x.Slot.Payload.IsImagePayload) && selected.Length > 1)
                {
                    var joined = string.Join(Environment.NewLine, selected.Select(x => x.Slot.Payload.Text ?? ""));
                    ClipboardSnapshot.Set(new ClipboardPayload { Text = joined });
                    writtenSequence = NativeMethods.GetClipboardSequenceNumber();
                    await Task.Delay(200);
                    EnsurePasteTarget(target);
                    NativeMethods.SendCtrlKey('V');
                    await Task.Delay(700);
                }
                else
                {
                    foreach (var item in selected)
                    {
                        if (!await NativeMethods.RestorePasteTargetAsync(target, focus))
                            throw new InvalidOperationException("原粘贴窗口无法激活，槽位已保留。");
                        ClipboardSnapshot.Set(item.Slot.Payload, item.Slot.Mode);
                        writtenSequence = NativeMethods.GetClipboardSequenceNumber();
                        // Allow Explorer to process its clipboard-change notification
                        // and enable the Paste command before delivering the key.
                        await Task.Delay(200);
                        EnsurePasteTarget(target);
                        PasteLog.Write($"send files={item.Slot.Payload.Files.Length} mode={item.Slot.Mode} target={target} class={NativeMethods.GetClassName(target)} focus={NativeMethods.GetClassName(NativeMethods.FocusedControl(target))}");
                        NativeMethods.SendCtrlKey('V');
                        await Task.Delay(item.Slot.Payload.IsFilePayload ? 2400 : 750);
                    }
                }
            }
            finally
            {
                // Do not overwrite a newer ordinary copy made by the user.
                if (writtenSequence is { } sequence && NativeMethods.GetClipboardSequenceNumber() == sequence)
                    snapshot.Restore();
            }

            var completedCutIndices = selected.Where(x => x.Slot.Mode == SlotMode.Cut &&
                (!x.Slot.Payload.IsFilePayload || x.Slot.Payload.Files.All(p => !File.Exists(p) && !Directory.Exists(p))))
                .Select(x => x.Index).ToArray();
            if (completedCutIndices.Length > 0) _store.Remove(completedCutIndices);
        }
        catch (Exception ex)
        {
            PasteLog.Write(ex.ToString());
            MessageBox.Show($"粘贴失败，剪切槽位已保留：\n{ex.Message}", "高级粘贴", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _busy = false; }
    }

    private static void EnsurePasteTarget(IntPtr target)
    {
        if (NativeMethods.GetForegroundWindow() != target)
            throw new InvalidOperationException("粘贴前焦点发生变化，本次未发送按键；请回到目标位置重试。");
    }

    private void ShowManager(IntPtr previous)
    {
        if (previous != Handle)
        {
            _lastExternalWindow = previous;
            _lastExternalFocus = NativeMethods.FocusedControl(previous);
        }
        RefreshGrid();
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void RefreshGrid()
    {
        var selected = SelectedIndices().ToHashSet();
        _grid.Rows.Clear();
        for (var i = 0; i < 10; i++)
        {
            var slot = _store[i];
            var row = _grid.Rows[_grid.Rows.Add(i + 1, slot?.Name ?? "—", slot?.ContentType ?? "—", slot?.ModeText ?? "—", slot?.Source ?? "—", slot?.Status ?? "空")];
            row.Tag = i;
            if (slot is null) row.DefaultCellStyle.ForeColor = Color.FromArgb(165, 169, 180);
            if (selected.Contains(i)) row.Selected = true;
        }
        _summary.Text = $"{_store.Count} / 10 个槽位";
        _summary.Left = ClientSize.Width - _summary.Width - 28;
        UpdatePreview();
    }

    private int[] SelectedIndices() => _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).Order().ToArray();
    private void UpdatePreview()
    {
        var indices = SelectedIndices();
        _preview.Text = indices.Length == 1 && _store[indices[0]] is { } slot
            ? $"名称：{slot.Name}　 类型：{slot.ContentType}　 方式：{slot.ModeText}\r\n来源：{slot.Source}　 时间：{slot.CreatedAt:HH:mm:ss}\r\n{slot.Preview}"
            : indices.Length > 1 ? $"已选择 {indices.Length} 个槽位。" : "选择一个槽位可查看内容预览。";
    }

    private void PasteSelected()
    {
        var indices = SelectedIndices();
        if (indices.Length == 0) { MessageBox.Show("请先选择槽位。", "高级剪贴板"); return; }
        Hide();
        _ = PasteIndicesAsync(indices, _lastExternalWindow, savedFocus: _lastExternalFocus);
    }

    private void RenameSelected()
    {
        var indices = SelectedIndices();
        if (indices.Length != 1 || _store[indices[0]] is not { } slot)
        {
            MessageBox.Show("请选择一个非空槽位。", "重命名槽位"); return;
        }
        using var dialog = new NameDialog(indices[0] + 1, slot.Mode, slot.ContentType,
            name => _store.NameExists(name, indices[0]), slot.Name);
        if (dialog.ShowDialog(this) == DialogResult.OK) _store.Rename(indices[0], dialog.ResultName);
    }

    private void RemoveSelected()
    {
        var indices = SelectedIndices().Where(i => _store[i] is not null).ToArray();
        if (indices.Length == 0) return;
        if (indices.Any(i => _store[i]!.Mode == SlotMode.Cut && !_store[i]!.Payload.IsFilePayload) &&
            MessageBox.Show("所选槽位包含已从原位置剪掉的内容，删除后可能无法恢复。是否继续？", "删除剪切内容",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _store.Remove(indices);
    }

    private void ClearAll()
    {
        if (_store.Count == 0) return;
        var warning = _store.Slots.Any(x => x?.Mode == SlotMode.Cut && !x.Payload.IsFilePayload)
            ? "其中包含已从原位置剪掉的内容，清空后可能无法恢复。\n\n" : "";
        if (MessageBox.Show(warning + "确定清空全部高级剪贴板槽位吗？", "清空全部",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) _store.Clear();
    }

    private void ConfirmExit()
    {
        if (_store.Count > 0 && MessageBox.Show("完全退出会立即清空全部高级剪贴板内容。是否退出？", "完全退出",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _store.Clear();
        _exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            BeginInvoke(ConfirmExit);
            return;
        }
        base.OnFormClosing(e);
    }

    internal void AllowClose() => _allowClose = true;
}
