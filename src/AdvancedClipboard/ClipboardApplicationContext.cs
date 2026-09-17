using System.IO.Pipes;
using System.Text;

namespace AdvancedClipboard;

internal sealed class ClipboardApplicationContext : ApplicationContext
{
    private readonly MainForm _form;
    private readonly CancellationTokenSource _shutdown = new();

    internal ClipboardApplicationContext(string[] initialArgs)
    {
        _form = new MainForm(ExitApplication);
        MainForm = _form;
        _form.HandleCreated += (_, _) =>
        {
            _ = RunPipeServerAsync(_shutdown.Token);
            _form.BeginInvoke(() => _form.HandleCommand(initialArgs));
        };
        _form.Show();
        _form.Hide();
    }

    private async Task RunPipeServerAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(Program.PipeName, PipeDirection.In, 8,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(token);
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                var line = await reader.ReadLineAsync(token);
                if (line is not null && !_form.IsDisposed)
                {
                    var args = CommandCodec.Decode(line);
                    _form.BeginInvoke(() => _form.HandleCommand(args));
                }
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(150, token).ContinueWith(_ => { }, TaskScheduler.Default); }
        }
    }

    private void ExitApplication()
    {
        _shutdown.Cancel();
        _form.AllowClose();
        _form.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _shutdown.Dispose();
        base.Dispose(disposing);
    }
}
