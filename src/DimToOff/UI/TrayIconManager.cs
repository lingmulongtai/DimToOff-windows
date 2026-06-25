using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DimToOff.Models;
using DimToOff.Services;

namespace DimToOff.UI;

internal sealed class TrayIconManager : IDisposable
{
    private readonly AppSettings settings;
    private readonly NotifyIcon notifyIcon;
    private readonly Icon trayIcon;
    private bool disposed;

    public event EventHandler? SettingsRequested;
    public event EventHandler? TrayMenuRequested;

    public TrayIconManager(AppSettings settings, SettingsService settingsService)
    {
        this.settings = settings;
        trayIcon = CreateTrayIcon();

        notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "DimToOff",
            Visible = true
        };

        notifyIcon.MouseUp += OnMouseUp;
        notifyIcon.DoubleClick += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowError(string title, string message)
    {
        if (!settings.ShowErrorNotifications)
        {
            return;
        }

        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = message;
        notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
        notifyIcon.ShowBalloonTip(5000);
    }

    public void RefreshSettings()
    {
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            TrayMenuRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        trayIcon.Dispose();
    }

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var accentBrush = new SolidBrush(Color.FromArgb(76, 194, 255));
        using var darkBrush = new SolidBrush(Color.FromArgb(14, 14, 18));
        using var softBrush = new SolidBrush(Color.FromArgb(230, 255, 255, 255));

        using var crescentRegion = new Region(new Rectangle(7, 5, 18, 18));
        crescentRegion.Exclude(new Rectangle(14, 2, 18, 18));
        graphics.FillRegion(accentBrush, crescentRegion);
        graphics.FillEllipse(darkBrush, 12, 16, 11, 11);
        graphics.FillEllipse(softBrush, 16, 20, 3, 3);

        return Icon.FromHandle(bitmap.GetHicon());
    }
}
