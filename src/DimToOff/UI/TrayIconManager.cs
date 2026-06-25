using DimToOff.Models;
using DimToOff.Services;

namespace DimToOff.UI;

internal sealed class TrayIconManager : IDisposable
{
    private readonly AppSettings settings;
    private readonly SettingsService settingsService;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem enabledItem;
    private bool disposed;

    public event EventHandler? TurnOffDisplayRequested;
    public event EventHandler? RestoreBrightnessRequested;
    public event EventHandler? ExitRequested;

    public TrayIconManager(AppSettings settings, SettingsService settingsService)
    {
        this.settings = settings;
        this.settingsService = settingsService;

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
        menu.Items.Add(new ToolStripLabel("DimToOff"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(enabledItem);
        menu.Items.Add(new ToolStripMenuItem("Turn Off Display Now", null, (_, _) => TurnOffDisplayRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("Restore Brightness Now", null, (_, _) => RestoreBrightnessRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(new ToolStripMenuItem("Settings...", null, (_, _) => ShowSettingsPlaceholder()));
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
            Icon = SystemIcons.Application,
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
    }
}
