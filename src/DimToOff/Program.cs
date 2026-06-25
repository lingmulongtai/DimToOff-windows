using System.Windows.Forms;
using DimToOff.Services;

namespace DimToOff;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "DimToOff.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("DimToOff is already running.", "DimToOff", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, e) =>
        {
            var log = new LogService();
            log.Error("Unhandled UI thread exception", e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var log = new LogService();
            log.Error("Unhandled application exception", e.ExceptionObject as Exception);
        };

        Application.Run(new DimToOffApplicationContext());
    }
}
