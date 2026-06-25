namespace DimToOff.Services;

internal sealed class LogService
{
    private readonly object syncRoot = new();
    private readonly string logFilePath;

    public LogService()
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DimToOff",
            "logs");
        Directory.CreateDirectory(logDirectory);
        logFilePath = Path.Combine(logDirectory, "dimtooff.log");
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
    {
        string fullMessage = exception is null ? message : $"{message}: {exception}";
        Write("ERROR", fullMessage);
    }

    private void Write(string level, string message)
    {
        string line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
        lock (syncRoot)
        {
            File.AppendAllText(logFilePath, line);
        }
    }
}
