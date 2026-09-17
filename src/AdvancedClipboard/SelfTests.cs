namespace AdvancedClipboard;

internal static class SelfTests
{
    internal static int Run()
    {
        try
        {
            Assert(NativeMethods.InputStructureSize == (IntPtr.Size == 8 ? 40 : 28), "SendInput结构体大小");
            Assert(ContentClassifier.Infer(new ClipboardPayload { Text = "https://example.com/a" }, "chrome") == "网址", "URL分类");
            Assert(ContentClassifier.Infer(new ClipboardPayload { Text = "甲\t乙\n1\t2" }, "excel") == "表格数据", "表格分类");
            Assert(ContentClassifier.Infer(new ClipboardPayload { Text = "普通内容" }, "notepad") == "纯文本", "文本分类");

            var testRoot = Path.Combine(Path.GetTempPath(), $"AdvancedClipboard-SelfTest-{Guid.NewGuid():N}");
            var sourceDir = Path.Combine(testRoot, "source");
            var destinationDir = Path.Combine(testRoot, "destination");
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(destinationDir);
            try
            {
                var sourceFile = Path.Combine(sourceDir, "sample.txt");
                File.WriteAllText(sourceFile, "copy test");
                var filePayload = new ClipboardPayload { Files = [sourceFile] };
                Assert(ContentClassifier.Infer(filePayload, "explorer") == ".txt", "文件扩展名分类");
                Assert(FileTransfer.Execute([Slot("file", filePayload)], destinationDir, new TestWindow(), out _), "文件复制执行");
                Assert(File.Exists(Path.Combine(destinationDir, "sample.txt")), "文件复制结果");
                Assert(FileTransfer.Execute([Slot("file-copy-1", filePayload)], destinationDir, new TestWindow(), out _), "重名文件复制1");
                Assert(File.Exists(Path.Combine(destinationDir, "sample-副本（1）.txt")), "重名文件编号1");
                Assert(FileTransfer.Execute([Slot("file-copy-2", filePayload)], destinationDir, new TestWindow(), out _), "重名文件复制2");
                Assert(File.Exists(Path.Combine(destinationDir, "sample-副本（2）.txt")), "重名文件编号2");
                Assert(FileTransfer.Execute([Slot("same-folder", filePayload)], sourceDir, new TestWindow(), out _), "同目录复制执行");
                Assert(File.Exists(Path.Combine(sourceDir, "sample-副本（1）.txt")), "同目录生成编号副本");
            }
            finally
            {
                if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            }

            var store = new SlotStore();
            var payload = new ClipboardPayload { Text = "测试" };
            store.Put(0, Slot("1", payload));
            store.Put(2, Slot("自定义", payload, false));
            Assert(store.NextIndex == 1, "空槽分配");
            store.Compact();
            Assert(store[0]?.Name == "1" && store[1]?.Name == "自定义" && store[2] is null, "槽位整理");
            Assert(store.NameExists(" 自定义 "), "名称规范化查重");
            var conflictStore = new SlotStore();
            conflictStore.Put(2, Slot("3", payload));
            conflictStore.Put(4, Slot("1", payload, false));
            conflictStore.Compact();
            Assert(conflictStore[0]?.Name == "3" && conflictStore[1]?.Name == "1", "整理保留自定义名称且不产生重名");
            Assert(PastePickerForm.AreCompatible([Slot("a", payload), Slot("b", payload)], out _), "文本多选兼容");
            Assert(!PastePickerForm.AreCompatible([Slot("a", payload), Slot("b", new ClipboardPayload { Png = [1] })], out _), "混合类型拦截");
            Console.WriteLine("SELF_TEST_OK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SELF_TEST_FAILED: " + ex.Message);
            return 1;
        }
    }

    private static ClipboardSlot Slot(string name, ClipboardPayload payload, bool auto = true) => new()
    {
        Name = name, AutoName = auto, ContentType = "纯文本", Mode = SlotMode.Copy,
        Payload = payload, Source = "test"
    };

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class TestWindow : IWin32Window
    {
        public IntPtr Handle => IntPtr.Zero;
    }
}
