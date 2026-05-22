using System.Runtime.Versioning;

namespace DesktopIdle;

[SupportedOSPlatform("windows10.0.19041.0")]
internal sealed class CursorMaskManager : IDisposable
{
    private readonly List<CursorMaskForm> _forms = new();
    private bool _visible;

    public void Show()
    {
        if (_visible) return;

        try
        {
            foreach (var screen in Screen.AllScreens)
            {
                var form = new CursorMaskForm(screen.Bounds);
                _forms.Add(form);
                form.Show();
            }

            _visible = true;
        }
        catch
        {
            Hide();
        }
    }

    public void Hide()
    {
        if (!_visible && _forms.Count == 0) return;

        foreach (var form in _forms.ToArray())
        {
            try
            {
                if (!form.IsDisposed)
                {
                    form.Close();
                    form.Dispose();
                }
            }
            catch
            {
                // Best effort only. Cursor hiding should never break ambient exit.
            }
        }

        _forms.Clear();
        _visible = false;
    }

    public void Dispose() => Hide();
}

[SupportedOSPlatform("windows10.0.19041.0")]
internal sealed class CursorMaskForm : Form
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private static readonly Lazy<Cursor> BlankCursor = new(CreateBlankCursor);

    public CursorMaskForm(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;

        // Almost fully transparent, but still hit-testable, so Windows uses this
        // form's blank cursor instead of the cursor from whatever app is behind it.
        // This is deliberately not a dark fade or visual overlay.
        Opacity = 0.01;
        Cursor = BlankCursor.Value;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_SETCURSOR)
        {
            _ = Win32.SetCursor(BlankCursor.Value.Handle);
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);
    }

    private static Cursor CreateBlankCursor()
    {
        // 32x32 monochrome cursor. For each pixel, AND=1 and XOR=0 means transparent.
        var andMask = Enumerable.Repeat((byte)0xFF, 32 * 32 / 8).ToArray();
        var xorMask = new byte[32 * 32 / 8];

        var handle = Win32.CreateCursor(IntPtr.Zero, 0, 0, 32, 32, andMask, xorMask);
        return handle == IntPtr.Zero ? Cursors.Default : new Cursor(handle);
    }
}
