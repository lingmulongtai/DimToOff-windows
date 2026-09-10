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
    private string? pendingBalloonUrl;
    private bool disposed;

    public event EventHandler? SettingsRequested;
    public event EventHandler? TrayMenuRequested;
    public event EventHandler<string>? BalloonUrlRequested;

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
        notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
    }

    public void ShowError(string title, string message, bool force = false)
    {
        if (!force && !settings.ShowErrorNotifications)
        {
            return;
        }

        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = message;
        notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
        pendingBalloonUrl = null;
        notifyIcon.ShowBalloonTip(5000);
    }

    public void ShowInformation(string title, string message, string? clickUrl = null)
    {
        notifyIcon.BalloonTipTitle = title;
        notifyIcon.BalloonTipText = message;
        notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        pendingBalloonUrl = clickUrl;
        notifyIcon.ShowBalloonTip(7000);
    }

    public void ShowUpdateAvailable(AvailableUpdate update)
    {
        string installerHint = update.HasInstaller
            ? "Click to open the release page and download the installer."
            : "Click to open the release page.";
        ShowInformation("DimToOff update available", $"{update.TagName} is available. {installerHint}", update.ReleaseUrl);
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
        else if (e.Button == MouseButtons.Left)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(pendingBalloonUrl))
        {
            BalloonUrlRequested?.Invoke(this, pendingBalloonUrl);
        }

        pendingBalloonUrl = null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        notifyIcon.BalloonTipClicked -= OnBalloonTipClicked;
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

        using var glowPen = new Pen(Color.FromArgb(80, 0, 0, 0), 4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var monitorPen = new Pen(Color.White, 2.6F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var standPen = new Pen(Color.White, 2.4F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var monitorBounds = new RectangleF(5.5F, 7.5F, 21F, 13.5F);
        using GraphicsPath monitorPath = CreateRoundedRectanglePath(monitorBounds, 3.5F);
        graphics.DrawPath(glowPen, monitorPath);
        graphics.DrawPath(monitorPen, monitorPath);
        graphics.DrawLine(glowPen, 16F, 21F, 16F, 24.5F);
        graphics.DrawLine(glowPen, 11.5F, 25F, 20.5F, 25F);
        graphics.DrawLine(standPen, 16F, 21F, 16F, 24.5F);
        graphics.DrawLine(standPen, 11.5F, 25F, 20.5F, 25F);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static GraphicsPath CreateRoundedRectanglePath(RectangleF bounds, float radius)
    {
        float diameter = radius * 2F;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
