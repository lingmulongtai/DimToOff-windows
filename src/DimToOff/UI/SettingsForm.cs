using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DimToOff.Models;
using DimToOff.Services;

namespace DimToOff.UI;

internal sealed class SettingsForm : Form
{
    private static readonly Color PageBack = Color.FromArgb(243, 243, 243);
    private static readonly Color CardBack = Color.FromArgb(255, 255, 255);
    private static readonly Color TextPrimary = Color.FromArgb(32, 32, 32);
    private static readonly Color TextSecondary = Color.FromArgb(96, 96, 96);
    private static readonly Color Border = Color.FromArgb(226, 226, 226);
    private static readonly Color Accent = Color.FromArgb(0, 120, 212);

    private readonly AppSettings settings;
    private readonly SettingsService settingsService;
    private readonly StartupService startupService;
    private readonly ToggleSwitch enabledToggle;
    private readonly ToggleSwitch startWithWindowsToggle;
    private readonly ToggleSwitch showErrorsToggle;
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
        ClientSize = new Size(660, 720);
        BackColor = PageBack;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI Variable Text", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(28, 24, 28, 20),
            BackColor = PageBack
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(root);

        root.Controls.Add(CreateHeader(), 0, 0);

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = PageBack,
            Padding = new Padding(0, 0, 8, 0)
        };
        root.Controls.Add(content, 0, 1);

        enabledToggle = new ToggleSwitch { Checked = settings.Enabled };
        startWithWindowsToggle = new ToggleSwitch { Checked = settings.StartWithWindows || startupService.IsEnabled() };
        showErrorsToggle = new ToggleSwitch { Checked = settings.ShowErrorNotifications };
        displayModeBox = CreateComboBox(["Blackout", "MonitorPower"], settings.DisplayOffMode);
        thresholdBox = CreateNumber(settings.OffThreshold, 0, 10, 1);
        debounceBox = CreateNumber(settings.DebounceMs, 300, 2000, 100);
        cooldownBox = CreateNumber(settings.CooldownMs, 1500, 5000, 100);
        ignoreInputBox = CreateNumber(settings.IgnoreInputMs, 300, 2000, 100);
        saveStableBox = CreateNumber(settings.BrightnessSaveStableMs, 1000, 10000, 250);
        fadeBox = CreateNumber(settings.FadeToBlackMs, 0, 1200, 20);
        minimumRestoreBox = CreateNumber(settings.MinimumRestoreBrightness, 30, 100, 1);
        defaultRestoreBox = CreateNumber(settings.DefaultRestoreBrightness, 30, 100, 1);

        AddSection(content, "General");
        AddRow(content, "Enable DimToOff", "Watch brightness changes and blank the screen at the threshold.", enabledToggle);
        AddRow(content, "Start with Windows", "Launch DimToOff when you sign in.", startWithWindowsToggle);
        AddRow(content, "Error notifications", "Show a tray notification only when something fails.", showErrorsToggle);

        AddSection(content, "Blanking");
        AddRow(content, "Mode", "Blackout keeps Windows awake and unlocked.", displayModeBox);
        AddRow(content, "Off threshold", "Brightness at or below this value starts blanking.", thresholdBox, "%");
        AddRow(content, "Fade to black", "Softens the moment the overlay appears.", fadeBox, "ms");
        AddRow(content, "Ignore input after blank", "Filters touchpad noise right after the overlay appears.", ignoreInputBox, "ms");

        AddSection(content, "Timing");
        AddRow(content, "Debounce", "Wait time before blanking after brightness reaches the threshold.", debounceBox, "ms");
        AddRow(content, "Cooldown", "Prevents immediate re-triggering after restore.", cooldownBox, "ms");
        AddRow(content, "Brightness save stable time", "Only stable brightness is remembered for restore.", saveStableBox, "ms");

        AddSection(content, "Restore");
        AddRow(content, "Minimum restore brightness", "Restore brightness will never be lower than this.", minimumRestoreBox, "%");
        AddRow(content, "Default restore brightness", "Used when no stable brightness has been learned yet.", defaultRestoreBox, "%");

        root.Controls.Add(CreateFooter(), 0, 2);
    }

    private Control CreateHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = PageBack
        };

        var title = new Label
        {
            Text = "Settings",
            AutoSize = false,
            Location = new Point(0, 0),
            Size = new Size(560, 34),
            Font = new Font("Segoe UI Variable Display", 20F, FontStyle.Regular),
            ForeColor = TextPrimary
        };
        panel.Controls.Add(title);

        var subtitle = new Label
        {
            Text = "DimToOff",
            AutoSize = false,
            Location = new Point(2, 42),
            Size = new Size(560, 24),
            Font = new Font("Segoe UI Variable Text", 9.5F),
            ForeColor = TextSecondary
        };
        panel.Controls.Add(subtitle);

        return panel;
    }

    private Control CreateFooter()
    {
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 14, 0, 0),
            BackColor = PageBack
        };

        var saveButton = new RoundedButton("Save", Accent, Color.White);
        saveButton.Click += (_, _) => SaveAndClose();
        var cancelButton = new RoundedButton("Cancel", Color.FromArgb(250, 250, 250), TextPrimary)
        {
            BorderColor = Border
        };
        cancelButton.Click += (_, _) => Close();

        footer.Controls.Add(saveButton);
        footer.Controls.Add(cancelButton);
        return footer;
    }

    private void SaveAndClose()
    {
        settings.Enabled = enabledToggle.Checked;
        settings.StartWithWindows = startWithWindowsToggle.Checked;
        settings.ShowErrorNotifications = showErrorsToggle.Checked;
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

    private static void AddSection(FlowLayoutPanel content, string text)
    {
        content.Controls.Add(new Label
        {
            Text = text,
            Width = 594,
            Height = 30,
            Margin = new Padding(0, 12, 0, 4),
            Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Bold),
            ForeColor = TextPrimary,
            TextAlign = ContentAlignment.BottomLeft
        });
    }

    private static void AddRow(FlowLayoutPanel content, string title, string detail, Control editor, string? suffix = null)
    {
        var row = new RoundedPanel
        {
            Width = 594,
            Height = 72,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = CardBack,
            BorderColor = Border,
            Radius = 8
        };

        var titleLabel = new Label
        {
            Text = title,
            Location = new Point(18, 13),
            Size = new Size(350, 22),
            Font = new Font("Segoe UI Variable Text", 9.5F, FontStyle.Regular),
            ForeColor = TextPrimary
        };
        row.Controls.Add(titleLabel);

        var detailLabel = new Label
        {
            Text = detail,
            Location = new Point(18, 37),
            Size = new Size(370, 20),
            Font = new Font("Segoe UI Variable Text", 8.25F),
            ForeColor = TextSecondary
        };
        row.Controls.Add(detailLabel);

        editor.Location = new Point(410, 20);
        editor.Size = editor is ToggleSwitch ? new Size(46, 24) : new Size(128, 30);
        row.Controls.Add(editor);

        if (suffix is not null)
        {
            var suffixLabel = new Label
            {
                Text = suffix,
                Location = new Point(544, 25),
                Size = new Size(34, 22),
                Font = new Font("Segoe UI Variable Text", 8.25F),
                ForeColor = TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            row.Controls.Add(suffixLabel);
        }

        content.Controls.Add(row);
    }

    private static ComboBox CreateComboBox(string[] values, string selectedValue)
    {
        var box = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.White,
            ForeColor = TextPrimary,
            FlatStyle = FlatStyle.System
        };
        box.Items.AddRange(values);
        box.SelectedItem = values.FirstOrDefault(value =>
            string.Equals(value, selectedValue, StringComparison.OrdinalIgnoreCase)) ?? values[0];
        return box;
    }

    private static NumericUpDown CreateNumber(int value, int min, int max, int increment) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = Math.Clamp(value, min, max),
        BackColor = Color.White,
        ForeColor = TextPrimary,
        BorderStyle = BorderStyle.FixedSingle,
        Increment = increment,
        TextAlign = HorizontalAlignment.Right
    };

    private sealed class RoundedPanel : Panel
    {
        public int Radius { get; set; } = 8;
        public Color BorderColor { get; set; } = Border;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var background = new SolidBrush(BackColor);
            using var borderPen = new Pen(BorderColor);
            Rectangle rect = new(0, 0, Width - 1, Height - 1);
            e.Graphics.FillRoundedRectangle(background, rect, Radius);
            e.Graphics.DrawRoundedRectangle(borderPen, rect, Radius);
        }
    }

    private sealed class ToggleSwitch : CheckBox
    {
        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
            Size = new Size(46, 24);
            TabStop = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? PageBack);

            Rectangle track = new(0, 0, Width - 1, Height - 1);
            Color trackColor = Checked ? Accent : Color.FromArgb(224, 224, 224);
            using var trackBrush = new SolidBrush(trackColor);
            using var borderPen = new Pen(Checked ? Accent : Color.FromArgb(190, 190, 190));
            e.Graphics.FillRoundedRectangle(trackBrush, track, Height / 2);
            e.Graphics.DrawRoundedRectangle(borderPen, track, Height / 2);

            int knobSize = Height - 8;
            int knobX = Checked ? Width - knobSize - 5 : 5;
            Rectangle knob = new(knobX, 4, knobSize, knobSize);
            using var knobBrush = new SolidBrush(Color.White);
            e.Graphics.FillEllipse(knobBrush, knob);
        }
    }

    private sealed class RoundedButton : Button
    {
        private readonly Color fillColor;
        private readonly Color textColor;

        public Color BorderColor { get; set; } = Color.Transparent;

        public RoundedButton(string text, Color fillColor, Color textColor)
        {
            this.fillColor = fillColor;
            this.textColor = textColor;
            Text = text;
            Width = 104;
            Height = 34;
            Margin = new Padding(8, 0, 0, 0);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pevent.Graphics.Clear(Parent?.BackColor ?? PageBack);
            Rectangle rect = new(0, 0, Width - 1, Height - 1);
            using var fill = new SolidBrush(fillColor);
            pevent.Graphics.FillRoundedRectangle(fill, rect, 6);
            if (BorderColor != Color.Transparent)
            {
                using var border = new Pen(BorderColor);
                pevent.Graphics.DrawRoundedRectangle(border, rect, 6);
            }

            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                rect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
