using System.Drawing;
using System.Windows.Forms;
using DimToOff.Models;
using DimToOff.Services;

namespace DimToOff.UI;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings settings;
    private readonly SettingsService settingsService;
    private readonly StartupService startupService;
    private readonly CheckBox enabledBox;
    private readonly CheckBox startWithWindowsBox;
    private readonly CheckBox showErrorsBox;
    private readonly ComboBox displayModeBox;
    private readonly NumericUpDown thresholdBox;
    private readonly NumericUpDown debounceBox;
    private readonly NumericUpDown cooldownBox;
    private readonly NumericUpDown ignoreInputBox;
    private readonly NumericUpDown saveStableBox;
    private readonly NumericUpDown fadeBox;
    private readonly NumericUpDown minimumRestoreBox;
    private readonly NumericUpDown defaultRestoreBox;

    public event EventHandler? SettingsSaved;

    public SettingsForm(AppSettings settings, SettingsService settingsService, StartupService startupService)
    {
        this.settings = settings;
        this.settingsService = settingsService;
        this.startupService = startupService;

        Text = "DimToOff Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 560);
        BackColor = Color.FromArgb(18, 18, 20);
        ForeColor = Color.FromArgb(244, 244, 245);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(22),
            BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        Controls.Add(root);

        var title = new Label
        {
            Text = "DimToOff",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 18F),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12,
            BackColor = BackColor
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        for (int i = 0; i < content.RowCount; i++)
        {
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        }

        root.Controls.Add(content, 0, 1);

        enabledBox = CreateCheckBox("Enable DimToOff", settings.Enabled);
        startWithWindowsBox = CreateCheckBox("Start with Windows", settings.StartWithWindows || startupService.IsEnabled());
        showErrorsBox = CreateCheckBox("Show error notifications", settings.ShowErrorNotifications);

        displayModeBox = CreateComboBox(["Blackout", "MonitorPower"], settings.DisplayOffMode);
        thresholdBox = CreateNumber(settings.OffThreshold, 0, 10, "%");
        debounceBox = CreateNumber(settings.DebounceMs, 300, 2000, "ms");
        cooldownBox = CreateNumber(settings.CooldownMs, 1500, 5000, "ms");
        ignoreInputBox = CreateNumber(settings.IgnoreInputMs, 300, 2000, "ms");
        saveStableBox = CreateNumber(settings.BrightnessSaveStableMs, 1000, 10000, "ms");
        fadeBox = CreateNumber(settings.FadeToBlackMs, 0, 1200, "ms");
        minimumRestoreBox = CreateNumber(settings.MinimumRestoreBrightness, 30, 100, "%");
        defaultRestoreBox = CreateNumber(settings.DefaultRestoreBrightness, 30, 100, "%");

        AddFullRow(content, 0, enabledBox);
        AddFullRow(content, 1, startWithWindowsBox);
        AddFullRow(content, 2, showErrorsBox);
        AddRow(content, 3, "Blanking mode", displayModeBox);
        AddRow(content, 4, "Off threshold", thresholdBox);
        AddRow(content, 5, "Debounce", debounceBox);
        AddRow(content, 6, "Cooldown", cooldownBox);
        AddRow(content, 7, "Ignore input after blank", ignoreInputBox);
        AddRow(content, 8, "Brightness save stable time", saveStableBox);
        AddRow(content, 9, "Fade to black", fadeBox);
        AddRow(content, 10, "Minimum restore brightness", minimumRestoreBox);
        AddRow(content, 11, "Default restore brightness", defaultRestoreBox);

        var separator = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(39, 39, 42)
        };
        root.Controls.Add(separator, 0, 2);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 14, 0, 0),
            BackColor = BackColor
        };
        root.Controls.Add(footer, 0, 3);

        var saveButton = CreateButton("Save");
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = CreateButton("Cancel");
        cancelButton.Click += (_, _) => Close();
        footer.Controls.Add(saveButton);
        footer.Controls.Add(cancelButton);
    }

    private void SaveAndClose()
    {
        settings.Enabled = enabledBox.Checked;
        settings.StartWithWindows = startWithWindowsBox.Checked;
        settings.ShowErrorNotifications = showErrorsBox.Checked;
        settings.DisplayOffMode = displayModeBox.SelectedItem?.ToString() ?? "Blackout";
        settings.OffThreshold = (int)thresholdBox.Value;
        settings.DebounceMs = (int)debounceBox.Value;
        settings.CooldownMs = (int)cooldownBox.Value;
        settings.IgnoreInputMs = (int)ignoreInputBox.Value;
        settings.BrightnessSaveStableMs = (int)saveStableBox.Value;
        settings.FadeToBlackMs = (int)fadeBox.Value;
        settings.MinimumRestoreBrightness = (int)minimumRestoreBox.Value;
        settings.DefaultRestoreBrightness = Math.Max((int)defaultRestoreBox.Value, settings.MinimumRestoreBrightness);

        settingsService.Save(settings);
        startupService.SetEnabled(settings.StartWithWindows);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private static CheckBox CreateCheckBox(string text, bool isChecked) => new()
    {
        Text = text,
        Checked = isChecked,
        AutoSize = true,
        Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(244, 244, 245),
        FlatStyle = FlatStyle.Flat
    };

    private static ComboBox CreateComboBox(string[] values, string selectedValue)
    {
        var box = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(32, 32, 36),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        box.Items.AddRange(values);
        box.SelectedItem = values.FirstOrDefault(value =>
            string.Equals(value, selectedValue, StringComparison.OrdinalIgnoreCase)) ?? values[0];
        return box;
    }

    private static NumericUpDown CreateNumber(int value, int min, int max, string suffix)
    {
        var number = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(32, 32, 36),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Increment = suffix == "%" ? 1 : 100
        };
        return number;
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Width = 104,
        Height = 34,
        FlatStyle = FlatStyle.Flat,
        BackColor = text == "Save" ? Color.FromArgb(97, 218, 251) : Color.FromArgb(39, 39, 42),
        ForeColor = text == "Save" ? Color.FromArgb(8, 9, 12) : Color.White,
        Margin = new Padding(8, 0, 0, 0)
    };

    private static void AddFullRow(TableLayoutPanel panel, int row, Control control)
    {
        panel.Controls.Add(control, 0, row);
        panel.SetColumnSpan(control, 2);
    }

    private static void AddRow(TableLayoutPanel panel, int row, string labelText, Control editor)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(212, 212, 216),
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(editor, 1, row);
    }
}
