using System.IO.Pipes;
using System.Text;
using System.Security.Principal;
using System.Diagnostics;

namespace AdvancedClipboard;

internal static class Program
{
    // Different Windows accounts/sessions must not share a singleton or pipe.
    private static readonly string InstanceScope =
        WindowsIdentity.GetCurrent().User!.Value + "." + Process.GetCurrentProcess().SessionId;
    internal static readonly string MutexName = "Local\\AdvancedClipboard.Singleton.2026." + InstanceScope;
    internal static readonly string PipeName = "AdvancedClipboard.CommandPipe.2026." + InstanceScope;

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = SelfTests.Run();
            return;
        }

        ApplicationConfiguration.Initialize();
        if (args.Contains("--remove-legacy-integration", StringComparer.OrdinalIgnoreCase))
        {
            RegistryInstaller.RemoveLegacyIntegration();
            return;
        }
        if (args.Contains("--install", StringComparer.OrdinalIgnoreCase))
        {
            RegistryInstaller.Install(Application.ExecutablePath);
            return;
        }
        if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            RegistryInstaller.Uninstall();
            return;
        }
        using var mutex = new Mutex(true, MutexName, out var isPrimary);
        if (!isPrimary)
        {
            SendToPrimary(args);
            return;
        }

        Application.Run(new ClipboardApplicationContext(args));
        GC.KeepAlive(mutex);
    }

    private static void SendToPrimary(string[] args)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(2500);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine(CommandCodec.Encode(args));
        }
        catch
        {
            MessageBox.Show("高级剪贴板正在启动，请稍后再试。", "高级剪贴板",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
