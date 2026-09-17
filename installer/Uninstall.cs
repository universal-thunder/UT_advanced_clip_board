using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class UninstallProgram
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        try
        {
            if (args.Length == 2 && args[0] == "--cleanup")
            {
                InstallEngine.Uninstall(args[1], true);
                MessageBox.Show("卸载完成。用户自行添加的文件会保留。", "高级剪贴板", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string root = InstallEngine.NormalizeRoot(AppDomain.CurrentDomain.BaseDirectory);
            if (!File.Exists(Path.Combine(root, InstallEngine.ManifestName))) throw new InvalidOperationException("请从安装目录启动卸载程序。");
            if (MessageBox.Show("确定卸载高级剪贴板？请先关闭程序，关闭会清空槽位。", "卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            InstallEngine.EnsureStopped(root);
            // Run from a temporary copy so the installed uninstaller is not locked.
            string helper = Path.Combine(Path.GetTempPath(), "AdvancedClipboard-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, helper);
            Process.Start(new ProcessStartInfo(helper, "--cleanup " + InstallEngine.Quote(root)) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "卸载未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Environment.ExitCode = 1;
        }
    }
}
