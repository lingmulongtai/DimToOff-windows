using System.IO.Pipes;
using System.Text;

namespace DimToOff.Services;

internal sealed class UiCommandService : IDisposable
{
    public const string PipeName = "DimToOff.Command";

    private readonly LogService log;
    private readonly CancellationTokenSource cancellation = new();
    private bool disposed;

    public UiCommandService(LogService log)
    {
        this.log = log;
    }

    public event EventHandler<string>? CommandReceived;

    public void Start()
    {
        _ = Task.Run(ListenLoopAsync);
    }

    private async Task ListenLoopAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellation.Token);
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                string? command = await reader.ReadLineAsync(cancellation.Token);

                if (!string.IsNullOrWhiteSpace(command))
                {
                    CommandReceived?.Invoke(this, command.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                log.Error("UI command pipe failed", ex);
                await Task.Delay(500, CancellationToken.None);
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
