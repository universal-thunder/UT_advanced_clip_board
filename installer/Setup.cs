using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class SetupProgram
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new SetupForm());
    }
}

internal sealed class SetupForm : Form
{
    private readonly TextBox destination = new TextBox();
    private readonly TextBox shortcut = new TextBox();
    private readonly CheckBox createShortcut = new CheckBox();
    private readonly CheckBox launch = new CheckBox();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly Label status = new Label();
    private readonly Button install = new Button();
    private bool busy, finished;

    internal SetupForm()
    {
        Text = "高级剪贴板 · 安装";
        Font = new Font("Microsoft YaHei UI", 10);
        BackColor = Color.FromArgb(247, 248, 251);
        ClientSize = new Size(660, 510);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AddLabel("高级剪贴板", 24, 20, 600, 40, 21);
        AddLabel("离线安装 · 自带 .NET 10 · 不开机启动 · 不添加右键菜单", 26, 70, 608, 30, 10);
        AddLabel("程序安装位置（不是压缩包下载位置）", 26, 112, 580, 28, 10);
        destination.SetBounds(26, 145, 505, 30);
        destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AdvancedClipboard");
        Button browseInstall = Browse(541, 143, delegate
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择程序存放位置，将创建 AdvancedClipboard 子文件夹";
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    destination.Text = string.Equals(Path.GetFileName(dialog.SelectedPath), "AdvancedClipboard", StringComparison.OrdinalIgnoreCase)
                        ? dialog.SelectedPath : Path.Combine(dialog.SelectedPath, "AdvancedClipboard");
            }
        });
        createShortcut.Text = "创建快捷方式（可选择桌面或其他文件夹）";
        createShortcut.Checked = true;
        createShortcut.SetBounds(26, 204, 600, 30);
        shortcut.SetBounds(26, 242, 505, 30);
        shortcut.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Button browseShortcut = Browse(541, 240, delegate
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择快捷方式保存文件夹";
                if (dialog.ShowDialog(this) == DialogResult.OK) shortcut.Text = dialog.SelectedPath;
            }
        });
        createShortcut.CheckedChanged += delegate { shortcut.Enabled = browseShortcut.Enabled = createShortcut.Checked; };
        AddLabel("仅为当前用户安装，无需管理员权限。卸载入口会加入 Windows 已安装应用。", 26, 285, 610, 44, 9);
        launch.Text = "安装完成后启动高级剪贴板";
        launch.Checked = true;
        launch.SetBounds(26, 335, 530, 28);
        LinkLabel license = new LinkLabel { Text = "查看 MIT 许可证与第三方说明" };
        license.SetBounds(26, 371, 380, 25);
        license.LinkClicked += delegate
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LICENSE"))
            using (StreamReader reader = new StreamReader(stream))
                MessageBox.Show(this, reader.ReadToEnd() + "\n\n.NET 运行时第三方许可及声明随安装文件提供。", "许可证", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        status.SetBounds(26, 400, 608, 25);
        progress.SetBounds(26, 430, 608, 12);
        install.Text = "安装";
        install.SetBounds(522, 455, 112, 36);
        install.Click += Install;
        Controls.AddRange(new Control[] { destination, shortcut, createShortcut, launch, progress, status, install, license });
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
        AcceptButton = install;
    }

    private void AddLabel(string text, int x, int y, int width, int height, float fontSize)
    {
        Controls.Add(new Label { Text = text, Bounds = new Rectangle(x, y, width, height), Font = new Font(Font.FontFamily, fontSize) });
    }

    private Button Browse(int x, int y, Action action)
    {
        Button button = new Button { Text = "浏览…", Bounds = new Rectangle(x, y, 93, 34) };
        button.Click += delegate { action(); };
        Controls.Add(button);
        return button;
    }

    private void Install(object sender, EventArgs e)
    {
        if (finished) { Close(); return; }
        string root;
        try
        {
            if (Process.GetProcessesByName("AdvancedClipboard").Length > 0)
                throw new InvalidOperationException("请先关闭正在运行的高级剪贴板，再安装新版，避免启动时打开旧版本。");
            root = InstallEngine.NormalizeRoot(destination.Text.Trim());
            if (createShortcut.Checked)
            {
                string path = Path.Combine(Path.GetFullPath(shortcut.Text.Trim()), "UTboard.lnk");
                if (File.Exists(path) && MessageBox.Show(this, "此位置已有同名快捷方式。是否替换？", "确认快捷方式", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            }
            busy = true;
            foreach (Control control in Controls) control.Enabled = false;
            InstallEngine.Install(root, createShortcut.Checked ? shortcut.Text.Trim() : null, true, delegate(int value, string file)
            {
                progress.Value = value;
                status.Text = value == 100 ? "安装完成" : "正在安装：" + file;
                Application.DoEvents();
            });
            finished = true;
            if (launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(root, "AdvancedClipboard.exe"), "--show") { WorkingDirectory = root, UseShellExecute = true });
            install.Text = "完成";
        }
        catch (Exception error)
        {
            MessageBox.Show(this, error.Message, finished ? "安装成功，启动失败" : "安装未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            status.Text = finished ? "安装完成，请手动启动程序。" : "请调整位置后重试。";
            if (!finished) foreach (Control control in Controls) control.Enabled = true;
            install.Text = finished ? "完成" : "安装";
        }
        finally { busy = false; install.Enabled = true; }
    }
}
