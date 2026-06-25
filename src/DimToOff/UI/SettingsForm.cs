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
    private readonly ChoiceField displayModeBox;
    private readonly NumberField thresholdBox;
    private readonly NumberField debounceBox;
    private readonly NumberField cooldownBox;
    private readonly NumberField ignoreInputBox;
    private readonly NumberField saveStableBox;
    private readonly NumberField fadeBox;
    private readonly NumberField minimumRestoreBox;
    private readonly NumberField defaultRestoreBox;

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
            Padding = new Padding(0, 0, 4, 0)
        };
        content.HorizontalScroll.Enabled = false;
        content.HorizontalScroll.Visible = false;
        content.Resize += (_, _) => ResizeContentChildren(content);
        root.Controls.Add(content, 0, 1);

        enabledToggle = new ToggleSwitch { Checked = settings.Enabled };
        startWithWindowsToggle = new ToggleSwitch { Checked = settings.StartWithWindows || startupService.IsEnabled() };
        showErrorsToggle = new ToggleSwitch { Checked = settings.ShowErrorNotifications };
        displayModeBox = new ChoiceField(["Blackout", "MonitorPower"], settings.DisplayOffMode);
        thresholdBox = new NumberField(settings.OffThreshold, 0, 10, 1, "%");
        debounceBox = new NumberField(settings.DebounceMs, 300, 2000, 100, "ms");
        cooldownBox = new NumberField(settings.CooldownMs, 1500, 5000, 100, "ms");
        ignoreInputBox = new NumberField(settings.IgnoreInputMs, 300, 2000, 100, "ms");
        saveStableBox = new NumberField(settings.BrightnessSaveStableMs, 1000, 10000, 250, "ms");
        fadeBox = new NumberField(settings.FadeToBlackMs, 0, 1200, 20, "ms");
        minimumRestoreBox = new NumberField(settings.MinimumRestoreBrightness, 30, 100, 1, "%");
        defaultRestoreBox = new NumberField(settings.DefaultRestoreBrightness, 30, 100, 1, "%");

        AddSection(content, "General");
        AddRow(content, "Enable DimToOff", "Watch brightness changes and blank the screen at the threshold.", enabledToggle);
        AddRow(content, "Start with Windows", "Launch DimToOff when you sign in.", startWithWindowsToggle);
        AddRow(content, "Error notifications", "Show a tray notification only when something fails.", showErrorsToggle);

        AddSection(content, "Blanking");
        AddRow(content, "Mode", "Blackout keeps Windows awake and unlocked.", displayModeBox);
        AddRow(content, "Off threshold", "Brightness at or below this value starts blanking.", thresholdBox);
        AddRow(content, "Fade to black", "Softens the moment the overlay appears.", fadeBox);
        AddRow(content, "Ignore input after blank", "Filters touchpad noise right after the overlay appears.", ignoreInputBox);

        AddSection(content, "Timing");
        AddRow(content, "Debounce", "Wait time before blanking after brightness reaches the threshold.", debounceBox);
        AddRow(content, "Cooldown", "Prevents immediate re-triggering after restore.", cooldownBox);
        AddRow(content, "Brightness save stable time", "Only stable brightness is remembered for restore.", saveStableBox);

        AddSection(content, "Restore");
        AddRow(content, "Minimum restore brightness", "Restore brightness will never be lower than this.", minimumRestoreBox);
        AddRow(content, "Default restore brightness", "Used when no stable brightness has been learned yet.", defaultRestoreBox);
        ResizeContentChildren(content);

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
        settings.DisplayOffMode = displayModeBox.SelectedValue;
        settings.OffThreshold = thresholdBox.Value;
        settings.DebounceMs = debounceBox.Value;
        settings.CooldownMs = cooldownBox.Value;
        settings.IgnoreInputMs = ignoreInputBox.Value;
        settings.BrightnessSaveStableMs = saveStableBox.Value;
        settings.FadeToBlackMs = fadeBox.Value;
        settings.MinimumRestoreBrightness = minimumRestoreBox.Value;
        settings.DefaultRestoreBrightness = Math.Max(defaultRestoreBox.Value, settings.MinimumRestoreBrightness);

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
            Width = Math.Max(520, content.ClientSize.Width - 24),
            Height = 30,
            Margin = new Padding(0, 12, 0, 4),
            Font = new Font("Segoe UI Variable Text", 10F, FontStyle.Bold),
            ForeColor = TextPrimary,
            TextAlign = ContentAlignment.BottomLeft
        });
    }

    private static void AddRow(FlowLayoutPanel content, string title, string detail, Control editor)
    {
        var row = new SettingRow(title, detail, editor)
        {
            Width = Math.Max(520, content.ClientSize.Width - 24),
            Height = 72,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = CardBack,
            BorderColor = Border,
            Radius = 8
        };

        content.Controls.Add(row);
    }

    private static void ResizeContentChildren(FlowLayoutPanel content)
    {
        int width = Math.Max(520, content.ClientSize.Width - 24);
        content.SuspendLayout();
        foreach (Control child in content.Controls)
        {
            child.Width = width;
            if (child is SettingRow row)
            {
                row.Reflow();
            }
        }

        content.HorizontalScroll.Value = 0;
        content.HorizontalScroll.Enabled = false;
        content.HorizontalScroll.Visible = false;
        content.ResumeLayout();
    }

    private class RoundedPanel : Panel
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

    private sealed class SettingRow : RoundedPanel
    {
        private readonly Label titleLabel;
        private readonly Label detailLabel;
        private readonly Control editor;

        public SettingRow(string title, string detail, Control editor)
        {
            this.editor = editor;
            titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 9.25F),
                ForeColor = TextPrimary,
                AutoEllipsis = true
            };
            detailLabel = new Label
            {
                Text = detail,
                Font = new Font("Segoe UI", 8.25F),
                ForeColor = TextSecondary,
                AutoEllipsis = true
            };

            Controls.Add(titleLabel);
            Controls.Add(detailLabel);
            Controls.Add(editor);
            Reflow();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Reflow();
        }

        public void Reflow()
        {
            int editorWidth = editor is ToggleSwitch ? 46 : 142;
            int editorHeight = editor is ToggleSwitch ? 24 : 34;
            int editorX = Math.Max(390, Width - editorWidth - 18);
            int editorY = (Height - editorHeight) / 2;
            editor.Bounds = new Rectangle(editorX, editorY, editorWidth, editorHeight);

            int labelWidth = Math.Max(180, editorX - 36);
            titleLabel.Bounds = new Rectangle(18, 13, labelWidth, 22);
            detailLabel.Bounds = new Rectangle(18, 38, labelWidth, 20);
        }
    }

    private sealed class ChoiceField : Control
    {
        private readonly string[] values;

        public string SelectedValue { get; private set; }

        public ChoiceField(string[] values, string selectedValue)
        {
            this.values = values;
            SelectedValue = values.FirstOrDefault(value =>
                string.Equals(value, selectedValue, StringComparison.OrdinalIgnoreCase)) ?? values[0];
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Font = new Font("Segoe UI", 9F);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? CardBack);
            Rectangle rect = new(0, 0, Width - 1, Height - 1);
            using var fill = new SolidBrush(Color.FromArgb(250, 250, 250));
            using var border = new Pen(Border);
            e.Graphics.FillRoundedRectangle(fill, rect, 7);
            e.Graphics.DrawRoundedRectangle(border, rect, 7);

            TextRenderer.DrawText(
                e.Graphics,
                SelectedValue,
                Font,
                new Rectangle(12, 0, Width - 38, Height),
                TextPrimary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

            using var chevron = new Pen(TextSecondary, 1.7F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            int cx = Width - 20;
            int cy = Height / 2;
            e.Graphics.DrawLine(chevron, cx - 4, cy - 2, cx, cy + 2);
            e.Graphics.DrawLine(chevron, cx, cy + 2, cx + 4, cy - 2);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            ShowMenu();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                ShowMenu();
            }
        }

        private void ShowMenu()
        {
            var menu = new ContextMenuStrip
            {
                Renderer = new LightMenuRenderer(),
                BackColor = Color.White,
                ForeColor = TextPrimary,
                Padding = new Padding(6),
                ShowImageMargin = false
            };

            foreach (string value in values)
            {
                var item = new ToolStripMenuItem(value)
                {
                    Checked = string.Equals(value, SelectedValue, StringComparison.OrdinalIgnoreCase),
                    Padding = new Padding(10, 7, 18, 7),
                    ForeColor = TextPrimary
                };
                item.Click += (_, _) =>
                {
                    SelectedValue = value;
                    Invalidate();
                };
                menu.Items.Add(item);
            }

            menu.Show(this, new Point(0, Height + 4));
        }
    }

    private sealed class NumberField : UserControl
    {
        private readonly int minimum;
        private readonly int maximum;
        private readonly int increment;
        private readonly TextBox textBox;
        private readonly Label suffixLabel;

        public int Value { get; private set; }

        public NumberField(int value, int minimum, int maximum, int increment, string suffix)
        {
            this.minimum = minimum;
            this.maximum = maximum;
            this.increment = increment;
            Value = Math.Clamp(value, minimum, maximum);

            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = CardBack;

            textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Right,
                Text = Value.ToString(),
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.FromArgb(250, 250, 250),
                ForeColor = TextPrimary
            };
            textBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitText();
                    e.SuppressKeyPress = true;
                }
            };
            textBox.Leave += (_, _) => CommitText();

            suffixLabel = new Label
            {
                Text = suffix,
                Font = textBox.Font,
                ForeColor = TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.FromArgb(250, 250, 250)
            };

            Controls.Add(textBox);
            Controls.Add(suffixLabel);
            Cursor = Cursors.IBeam;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            int suffixWidth = suffixLabel.Text == "%" ? 22 : 30;
            int textHeight = textBox.PreferredHeight;
            int textY = (Height - textHeight) / 2;
            textBox.Bounds = new Rectangle(10, textY, Width - suffixWidth - 18, textHeight);
            suffixLabel.Bounds = new Rectangle(Width - suffixWidth - 8, 0, suffixWidth, Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? CardBack);
            Rectangle rect = new(0, 0, Width - 1, Height - 1);
            using var fill = new SolidBrush(Color.FromArgb(250, 250, 250));
            using var border = new Pen(textBox.Focused ? Accent : Border);
            e.Graphics.FillRoundedRectangle(fill, rect, 7);
            e.Graphics.DrawRoundedRectangle(border, rect, 7);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int direction = e.Delta > 0 ? 1 : -1;
            SetValue(Value + direction * increment);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            textBox.Focus();
            textBox.SelectAll();
        }

        private void CommitText()
        {
            if (int.TryParse(textBox.Text.Trim(), out int parsed))
            {
                SetValue(parsed);
            }
            else
            {
                textBox.Text = Value.ToString();
            }
        }

        private void SetValue(int value)
        {
            Value = Math.Clamp(value, minimum, maximum);
            textBox.Text = Value.ToString();
            Invalidate();
        }
    }

    private sealed class LightMenuRenderer : ToolStripProfessionalRenderer
    {
        public LightMenuRenderer()
            : base(new LightColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Rectangle bounds = new(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            using var brush = new SolidBrush(e.Item.Selected ? Color.FromArgb(243, 243, 243) : Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillRoundedRectangle(brush, bounds, 6);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawRoundedRectangle(pen, new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 8);
        }
    }

    private sealed class LightColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color MenuBorder => Border;
        public override Color MenuItemSelected => Color.FromArgb(243, 243, 243);
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
