using System.Drawing;
using System.Windows.Forms;
using DimToOff.Native;

namespace DimToOff.Services;

internal sealed class BlackoutService : IDisposable
{
    private readonly LogService log;
    private BlackoutForm? form;
    private bool cursorHidden;
    private bool disposed;
    private DateTimeOffset shownAt;

    public event EventHandler? UserInputDetected;

    public BlackoutService(LogService log)
    {
        this.log = log;
    }

    public void Show(int fadeMs)
    {
        if (form is not null)
        {
            return;
        }

        shownAt = DateTimeOffset.Now;
        form = new BlackoutForm(SystemInformation.VirtualScreen, fadeMs);
        form.UserInputDetected += OnFormUserInputDetected;
        form.Show();
        form.ForceTopMost();
        form.BeginFadeIn();

        Cursor.Hide();
        cursorHidden = true;
        log.Info("Blackout shown");
    }

    public void Hide()
    {
        if (form is not null)
        {
            form.UserInputDetected -= OnFormUserInputDetected;
            form.Close();
            form.Dispose();
            form = null;
        }

        if (cursorHidden)
        {
            Cursor.Show();
            cursorHidden = false;
        }

        log.Info("Blackout hidden");
    }

    private void OnFormUserInputDetected(object? sender, EventArgs e)
    {
        if ((DateTimeOffset.Now - shownAt).TotalMilliseconds < 350)
        {
            return;
        }

        log.Info("User input detected by blackout overlay");
        UserInputDetected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Hide();
    }

    private sealed class BlackoutForm : Form
    {
        private readonly int fadeMs;
        private System.Windows.Forms.Timer? fadeTimer;
        private DateTime fadeStartedAt;

        public event EventHandler? UserInputDetected;

        public BlackoutForm(Rectangle bounds, int fadeMs)
        {
            this.fadeMs = fadeMs;
            BackColor = Color.Black;
            Bounds = bounds;
            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            WindowState = FormWindowState.Normal;
            Opacity = fadeMs <= 0 ? 1 : 0;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeConstants.WS_EX_TOPMOST | NativeConstants.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ForceTopMost();
            Activate();
            Focus();
        }

        public void ForceTopMost()
        {
            User32.ShowWindow(Handle, NativeConstants.SW_SHOWNORMAL);
            User32.SetWindowPos(
                Handle,
                User32.HwndTopmost,
                Bounds.X,
                Bounds.Y,
                Bounds.Width,
                Bounds.Height,
                NativeConstants.SWP_SHOWWINDOW);
            User32.SetForegroundWindow(Handle);
            BringToFront();
            Activate();
            Focus();
        }

        public void BeginFadeIn()
        {
            if (fadeMs <= 0)
            {
                Opacity = 1;
                return;
            }

            fadeStartedAt = DateTime.Now;
            fadeTimer = new System.Windows.Forms.Timer
            {
                Interval = 16
            };
            fadeTimer.Tick += (_, _) =>
            {
                double elapsed = (DateTime.Now - fadeStartedAt).TotalMilliseconds;
                double progress = Math.Clamp(elapsed / fadeMs, 0, 1);
                Opacity = progress;

                if (progress >= 1)
                {
                    fadeTimer?.Stop();
                    fadeTimer?.Dispose();
                    fadeTimer = null;
                }
            };
            fadeTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fadeTimer?.Stop();
                fadeTimer?.Dispose();
                fadeTimer = null;
            }

            base.Dispose(disposing);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            UserInputDetected?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            // Showing or activating the overlay can synthesize mouse-move messages.
            // Do not treat movement alone as an intentional wake action.
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            UserInputDetected?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            UserInputDetected?.Invoke(this, EventArgs.Empty);
        }
    }
}
