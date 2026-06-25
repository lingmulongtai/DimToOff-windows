using System.Drawing;
using System.Windows.Forms;

namespace DimToOff.Services;

internal sealed class BlackoutService : IDisposable
{
    private readonly LogService log;
    private readonly List<BlackoutForm> forms = [];
    private bool cursorHidden;
    private bool disposed;

    public event EventHandler? UserInputDetected;

    public BlackoutService(LogService log)
    {
        this.log = log;
    }

    public void Show()
    {
        if (forms.Count > 0)
        {
            return;
        }

        foreach (Screen screen in Screen.AllScreens)
        {
            var form = new BlackoutForm(screen.Bounds);
            form.UserInputDetected += OnFormUserInputDetected;
            forms.Add(form);
            form.Show();
        }

        if (forms.Count > 0)
        {
            forms[0].Activate();
        }

        Cursor.Hide();
        cursorHidden = true;
        log.Info($"Blackout shown on {forms.Count} screen(s)");
    }

    public void Hide()
    {
        foreach (BlackoutForm form in forms.ToArray())
        {
            form.UserInputDetected -= OnFormUserInputDetected;
            form.Close();
            form.Dispose();
        }

        forms.Clear();

        if (cursorHidden)
        {
            Cursor.Show();
            cursorHidden = false;
        }

        log.Info("Blackout hidden");
    }

    private void OnFormUserInputDetected(object? sender, EventArgs e)
    {
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
        public event EventHandler? UserInputDetected;

        public BlackoutForm(Rectangle bounds)
        {
            BackColor = Color.Black;
            Bounds = bounds;
            FormBorderStyle = FormBorderStyle.None;
            KeyPreview = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            WindowState = FormWindowState.Normal;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            UserInputDetected?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UserInputDetected?.Invoke(this, EventArgs.Empty);
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
