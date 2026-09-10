using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DimToOff.Models;
using DimToOff.Native;
using DimToOff.Services;

namespace DimToOff.UI;

internal sealed class TrayIconManager : IDisposable
{
    private readonly AppSettings settings;
    private readonly NotifyIcon notifyIcon;
    private readonly TrayIcons icons;
    private string? pendingBalloonUrl;
    private bool disposed;

    public event EventHandler? SettingsRequested;
    public event EventHandler? TrayMenuRequested;
    public event EventHandler<string>? BalloonUrlRequested;

    public TrayIconManager(AppSettings settings, SettingsService settingsService)
    {
        this.settings = settings;
        icons = new TrayIcons();

        notifyIcon = new NotifyIcon
        {
            Icon = icons.Active,
            Text = "DimToOff",
            Visible = true
        };

        notifyIcon.MouseUp += OnMouseUp;
        notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
        RefreshSettings();
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

    /// <summary>Keeps the tray icon and its tooltip in step with the current settings.</summary>
    public void RefreshSettings()
    {
        notifyIcon.Icon = settings.Enabled ? icons.Active : icons.Paused;
        notifyIcon.Text = BuildTooltip();
    }

    private string BuildTooltip()
    {
        if (!settings.Enabled)
        {
            return "DimToOff - paused";
        }

        if (!settings.IdleBlackoutEnabled)
        {
            return "DimToOff - blanks at minimum brightness";
        }

        bool onBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
        int timeoutSeconds = onBattery
            ? settings.IdleTimeoutOnBatterySeconds
            : settings.IdleTimeoutPluggedInSeconds;

        if (timeoutSeconds <= 0)
        {
            return "DimToOff - blanks at minimum brightness";
        }

        return $"DimToOff - screen off after {FormatTimeout(timeoutSeconds)} idle";
    }

    private static string FormatTimeout(int seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds} sec";
        }

        int minutes = seconds / 60;
        if (minutes < 60)
        {
            return $"{minutes} min";
        }

        int hours = minutes / 60;
        int remainingMinutes = minutes % 60;
        return remainingMinutes == 0 ? $"{hours} hr" : $"{hours} hr {remainingMinutes} min";
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
        notifyIcon.MouseUp -= OnMouseUp;
        notifyIcon.BalloonTipClicked -= OnBalloonTipClicked;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        icons.Dispose();
    }

    /// <summary>
    /// Owns the two tray icons. <see cref="Icon.FromHandle"/> does not take ownership of the
    /// native icon, so the handles are released explicitly.
    /// </summary>
    private sealed class TrayIcons : IDisposable
    {
        private readonly List<nint> iconHandles = [];
        private bool disposed;

        public TrayIcons()
        {
            Active = CreateTrayIcon(Color.White);
            Paused = CreateTrayIcon(Color.FromArgb(120, 255, 255, 255));
        }

        public Icon Active { get; }

        public Icon Paused { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Active.Dispose();
            Paused.Dispose();

            foreach (nint handle in iconHandles)
            {
                User32.DestroyIcon(handle);
            }

            iconHandles.Clear();
        }

        private Icon CreateTrayIcon(Color strokeColor)
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
            using var monitorPen = new Pen(strokeColor, 2.6F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using var standPen = new Pen(strokeColor, 2.4F)
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

            nint iconHandle = bitmap.GetHicon();
            iconHandles.Add(iconHandle);
            return Icon.FromHandle(iconHandle);
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
}
