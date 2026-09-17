using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;

internal static class InstallerTests
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = InstallEngine.NormalizeRoot(args[0]);
        try
        {
            string failedRoot = root + "-failure";
            bool rejected = false;
            try
            {
                InstallEngine.Install(failedRoot, null, false, delegate(int value, string file)
                {
                    if (file == "dotnet-LICENSE.txt") throw new IOException("Simulated extraction failure");
                });
            }
            catch (IOException) { rejected = true; }
            if (!rejected || Directory.Exists(failedRoot)) throw new Exception("Failed installation not rolled back");
            string shortcutDir = root + "-shortcuts";
            Directory.CreateDirectory(shortcutDir);
            InstallEngine.Install(root, shortcutDir, false, delegate { });
            string shortcut = Path.Combine(shortcutDir, "UTboard.lnk");
            if (!File.Exists(shortcut)) throw new Exception("Shortcut not created");
            using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using (ZipArchive archive = new ZipArchive(payload, ZipArchiveMode.Read))
            using (SHA256 hash = SHA256.Create())
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    using (Stream original = entry.Open())
                    using (Stream extracted = File.OpenRead(InstallEngine.OwnedPath(root, entry.FullName)))
                        if (Convert.ToBase64String(hash.ComputeHash(original)) != Convert.ToBase64String(hash.ComputeHash(extracted)))
                            throw new Exception("Extraction checksum mismatch: " + entry.FullName);
                }
            }
            // Unknown user files must survive uninstall.
            File.WriteAllText(Path.Combine(root, "user-file.txt"), "preserve");
            try { InstallEngine.OwnedPath(root, "..\\outside.txt"); throw new Exception("Traversal accepted"); }
            catch (InvalidOperationException) { }
            Process process = Process.Start(new ProcessStartInfo(Path.Combine(root, "AdvancedClipboard.exe"), "--self-test") { UseShellExecute = false });
            if (!process.WaitForExit(30000) || process.ExitCode != 0) throw new Exception("Installed app self-test failed");
            InstallEngine.Uninstall(root, false);
            if (File.Exists(shortcut)) throw new Exception("Shortcut not removed");
            Directory.Delete(shortcutDir);
            if (!File.Exists(Path.Combine(root, "user-file.txt")) || File.Exists(Path.Combine(root, "AdvancedClipboard.exe")))
                throw new Exception("Uninstall ownership failed");
            File.Delete(Path.Combine(root, "user-file.txt"));
            Directory.Delete(root);
            Console.WriteLine("INSTALLER_TEST_OK: rollback, extraction hashes, installed app, shortcut creation/removal, path boundary, uninstall preserves user files");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
