using System.IO.Pipes;
using System.Text;

namespace DimToOff.Settings.Services;

internal sealed class TrayCommandClient
{
    private readonly string pipeName;

    public TrayCommandClient(string pipeName)
    {
        this.pipeName = pipeName;
    }

    public async Task SendAsync(string command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(350);
            await using var writer = new StreamWriter(pipe, Encoding.UTF8)
            {
                AutoFlush = true
            };
            await writer.WriteLineAsync(command);
        }
        catch
        {
        }
    }
}
