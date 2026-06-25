using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DimToOff.Models;
using DimToOff.Services;

namespace DimToOff.UI;

internal sealed class TrayIconManager : IDisposable
{
    private readonly AppSettings settings;
    private readonly SettingsService settingsService;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem enabledItem;
    private readonly Icon trayIcon;
    private bool disposed;

    public event EventHandler? TurnOffDisplayRequested;
    public event EventHandler? RestoreBrightnessRequested;
    public event EventHandler? ExitRequested;

    public TrayIconManager(AppSettings settings, SettingsService settingsService)
    {
        this.settings = settings;
        this.settingsService = settingsService;

        trayIcon = CreateTrayIcon();

        enabledItem = new ToolStripMenuItem("Enabled")
        {
            CheckOnClick = true,
            Checked = settings.Enabled
        };
        enabledItem.CheckedChanged += (_, _) =>
        {
            settings.Enabled = enabledItem.Checked;
            settingsService.Save(settings);
        };

        var menu = new ContextMenuStrip();
        menu.Renderer = new DarkToolStripRenderer();
        menu.BackColor = Color.FromArgb(24, 24, 27);
        menu.ForeColor = Color.FromArgb(244, 244, 245);
        menu.ShowImageMargin = true;
        menu.Padding = new Padding(8);

        menu.Items.Add(new ToolStripLabel("DimToOff")
        {
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            ForeColor = Color.White,
            Image = trayIcon.ToBitmap()
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(enabledItem);
        menu.Items.Add(new ToolStripMenuItem("Blank Screen Now", null, (_, _) => TurnOffDisplayRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("Restore Brightness", null, (_, _) => RestoreBrightnessRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => ShowSettingsPlaceholder()));
        menu.Items.Add(new ToolStripMenuItem("Start with Windows")
        {
            Enabled = false,
            Checked = settings.StartWithWindows
        });
        menu.Items.Add(new ToolStripMenuItem("About", null, (_, _) => ShowAbout()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty)));

        notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "DimToOff",
            ContextMenuStrip = menu,
            Visible = true
        };
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

    private void ShowSettingsPlaceholder()
    {
        MessageBox.Show(
            "Settings UI is not implemented in the MVP. Edit %APPDATA%\\DimToOff\\settings.json if needed.",
            "DimToOff Settings",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "DimToOff\n\nTurns the display off when laptop brightness reaches the minimum threshold, then restores the last usable brightness after input.",
            "About DimToOff",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
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

        using var monitorBrush = new LinearGradientBrush(
            new Rectangle(5, 7, 22, 15),
            Color.FromArgb(32, 34, 40),
            Color.FromArgb(8, 9, 12),
            LinearGradientMode.Vertical);
        using var accentPen = new Pen(Color.FromArgb(97, 218, 251), 2);
        using var dimBrush = new SolidBrush(Color.FromArgb(97, 218, 251));
        using var cutBrush = new SolidBrush(Color.FromArgb(8, 9, 12));

        graphics.FillRoundedRectangle(monitorBrush, new Rectangle(5, 7, 22, 15), 4);
        graphics.DrawRoundedRectangle(accentPen, new Rectangle(5, 7, 22, 15), 4);
        graphics.FillRectangle(dimBrush, 13, 22, 6, 3);
        graphics.FillRoundedRectangle(dimBrush, new Rectangle(10, 25, 12, 2), 1);
        graphics.FillEllipse(dimBrush, 15, 10, 8, 8);
        graphics.FillEllipse(cutBrush, 18, 8, 8, 8);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
    {
        public DarkToolStripRenderer()
            : base(new DarkColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle bounds = new(Point.Empty, e.Item.Size);
            Color color = e.Item.Selected
                ? Color.FromArgb(39, 39, 42)
                : Color.FromArgb(24, 24, 27);
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, bounds);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(63, 63, 70));
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(24, 24, 27);
        public override Color ImageMarginGradientBegin => Color.FromArgb(24, 24, 27);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(24, 24, 27);
        public override Color ImageMarginGradientEnd => Color.FromArgb(24, 24, 27);
        public override Color MenuItemSelected => Color.FromArgb(39, 39, 42);
        public override Color MenuBorder => Color.FromArgb(63, 63, 70);
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using GraphicsPath path = CreateRoundedRectanglePath(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using GraphicsPath path = CreateRoundedRectanglePath(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
