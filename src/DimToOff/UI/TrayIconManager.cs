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
    private readonly ToolStripMenuItem startWithWindowsItem;
    private readonly Icon trayIcon;
    private bool disposed;

    public event EventHandler? TurnOffDisplayRequested;
    public event EventHandler? RestoreBrightnessRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler<bool>? EnabledChanged;
    public event EventHandler<bool>? StartWithWindowsChanged;
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
            EnabledChanged?.Invoke(this, settings.Enabled);
        };

        startWithWindowsItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = settings.StartWithWindows
        };
        startWithWindowsItem.CheckedChanged += (_, _) =>
        {
            settings.StartWithWindows = startWithWindowsItem.Checked;
            settingsService.Save(settings);
            StartWithWindowsChanged?.Invoke(this, settings.StartWithWindows);
        };

        var menu = new RoundedContextMenuStrip();
        menu.Renderer = new DarkToolStripRenderer();
        menu.BackColor = Color.FromArgb(32, 32, 36);
        menu.ForeColor = Color.FromArgb(244, 244, 245);
        menu.ShowImageMargin = true;
        menu.Padding = new Padding(8, 10, 8, 10);

        menu.Items.Add(new ToolStripLabel("DimToOff")
        {
            Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
            ForeColor = Color.White,
            Image = trayIcon.ToBitmap(),
            Padding = new Padding(4, 6, 4, 6)
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(StyleItem(enabledItem));
        menu.Items.Add(CreateItem("Blank screen now", () => TurnOffDisplayRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateItem("Restore brightness", () => RestoreBrightnessRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateItem("Settings", () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(StyleItem(startWithWindowsItem));
        menu.Items.Add(CreateItem("About", ShowAbout));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateItem("Exit", () => ExitRequested?.Invoke(this, EventArgs.Empty)));

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

    public void RefreshSettings()
    {
        enabledItem.Checked = settings.Enabled;
        startWithWindowsItem.Checked = settings.StartWithWindows;
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "DimToOff\n\nTurns the display off when laptop brightness reaches the minimum threshold, then restores the last usable brightness after input.",
            "About DimToOff",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static ToolStripMenuItem CreateItem(string text, Action action)
    {
        var item = new ToolStripMenuItem(text, null, (_, _) => action());
        return StyleItem(item);
    }

    private static ToolStripMenuItem StyleItem(ToolStripMenuItem item)
    {
        item.ForeColor = Color.FromArgb(244, 244, 245);
        item.BackColor = Color.FromArgb(32, 32, 36);
        item.Padding = new Padding(8, 7, 10, 7);
        item.Margin = new Padding(2, 1, 2, 1);
        return item;
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
        using var softBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255));

        using var crescentRegion = new Region(new Rectangle(7, 5, 18, 18));
        crescentRegion.Exclude(new Rectangle(14, 2, 18, 18));
        graphics.FillRegion(accentBrush, crescentRegion);
        graphics.FillEllipse(darkBrush, 12, 16, 11, 11);
        graphics.FillEllipse(softBrush, 16, 20, 3, 3);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private sealed class RoundedContextMenuStrip : ContextMenuStrip
    {
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            ApplyRoundedRegion();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyRoundedRegion();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Region?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ApplyRoundedRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            using GraphicsPath path = GraphicsExtensions.CreateRoundedRectanglePath(new Rectangle(0, 0, Width, Height), 12);
            Region?.Dispose();
            Region = new Region(path);
        }
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
            Rectangle bounds = new(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            Color color = e.Item.Selected
                ? Color.FromArgb(54, 54, 60)
                : Color.FromArgb(32, 32, 36);
            using var brush = new SolidBrush(color);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillRoundedRectangle(brush, bounds, 8);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Color.FromArgb(32, 32, 36));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillRoundedRectangle(brush, new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 12);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(70, 70, 78));
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawRoundedRectangle(pen, new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 12);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle box = new(e.ImageRectangle.X + 2, e.ImageRectangle.Y + 2, 14, 14);
            using var brush = new SolidBrush(Color.FromArgb(76, 194, 255));
            using var pen = new Pen(Color.FromArgb(12, 12, 16), 2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillRoundedRectangle(brush, box, 4);
            e.Graphics.DrawLine(pen, box.X + 4, box.Y + 7, box.X + 7, box.Y + 10);
            e.Graphics.DrawLine(pen, box.X + 7, box.Y + 10, box.X + 11, box.Y + 4);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(70, 70, 78));
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 14, y, e.Item.Width - 14, y);
        }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(32, 32, 36);
        public override Color ImageMarginGradientBegin => Color.FromArgb(32, 32, 36);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(32, 32, 36);
        public override Color ImageMarginGradientEnd => Color.FromArgb(32, 32, 36);
        public override Color MenuItemSelected => Color.FromArgb(54, 54, 60);
        public override Color MenuBorder => Color.FromArgb(70, 70, 78);
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

    public static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
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
