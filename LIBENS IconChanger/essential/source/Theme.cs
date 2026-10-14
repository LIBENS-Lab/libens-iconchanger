using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LibensIconChanger;

internal static class Theme
{
    public static void ConfigureNativeColorMode()
    {
        // .NET 10 propagates the mode to native child controls and dialogs too.
        if (!SystemInformation.HighContrast) Application.SetColorMode(SystemColorMode.Dark);
    }
    public static Color Hex(string value) => ColorTranslator.FromHtml(value);
    public static Color Canvas => SystemInformation.HighContrast ? SystemColors.Window : Hex("#0E1014");
    public static Color Surface => SystemInformation.HighContrast ? SystemColors.Window : Hex("#16191F");
    public static Color Raised => SystemInformation.HighContrast ? SystemColors.Control : Hex("#1D2129");
    public static Color Hover => SystemInformation.HighContrast ? SystemColors.Highlight : Hex("#252A34");
    public static Color Input => SystemInformation.HighContrast ? SystemColors.Window : Hex("#11141A");
    public static Color Border => SystemInformation.HighContrast ? SystemColors.WindowText : Hex("#697585");
    public static Color Divider => SystemInformation.HighContrast ? SystemColors.WindowText : Hex("#343A46");
    public static Color Text => SystemInformation.HighContrast ? SystemColors.WindowText : Hex("#F1F3F5");
    public static Color Secondary => SystemInformation.HighContrast ? SystemColors.WindowText : Hex("#BBC1CB");
    public static Color Muted => SystemInformation.HighContrast ? SystemColors.GrayText : Hex("#9299A5");
    public static Color Accent => SystemInformation.HighContrast ? SystemColors.Highlight : Hex("#54D994");
    public static Color AccentHover => SystemInformation.HighContrast ? SystemColors.Highlight : Hex("#7CE8AE");
    public static Color AccentPressed => SystemInformation.HighContrast ? SystemColors.Highlight : Hex("#32BB77");
    public static Color Focus => SystemInformation.HighContrast ? SystemColors.Highlight : Hex("#ADF3CE");
    public static Color Selection => SystemInformation.HighContrast ? SystemColors.Highlight : Hex("#183E2D");
    public static Color OnAccent => SystemInformation.HighContrast ? SystemColors.HighlightText : Hex("#101216");
    public static Color Danger => SystemInformation.HighContrast ? SystemColors.WindowText : Hex("#FF8EA1");

    public static GraphicsPath Round(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    public static void DarkTitle(IntPtr handle)
    {
        if (SystemInformation.HighContrast) return;
        int enabled = 1;
        DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
    }
}

internal sealed class SurfacePanel : Panel
{
    public SurfacePanel()
    {
        DoubleBuffered = true;
        // Dynamic option rows resize this panel. Repaint the whole surface so the
        // previous bottom border cannot remain in the padded area as a short line.
        ResizeRedraw = true;
        BackColor = Theme.Surface;
        Padding = new Padding(22);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Divider);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

internal sealed class InputFrame : Panel
{
    private readonly Control _editor;

    public InputFrame(Control editor)
    {
        _editor = editor;
        BackColor = Theme.Input;
        Padding = new Padding(10, 3, 10, 3);
        Height = 32;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        editor.Dock = DockStyle.None;
        editor.BackColor = Theme.Input;
        editor.ForeColor = Theme.Text;
        if (editor is TextBox text) text.BorderStyle = BorderStyle.None;
        if (editor is NumericUpDown number) number.BorderStyle = BorderStyle.None;
        Controls.Add(editor);
        editor.Enter += (_, _) => Invalidate();
        editor.Leave += (_, _) => Invalidate();
        editor.EnabledChanged += (_, _) => Invalidate();
        DoubleBuffered = true;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (_editor is null) return;
        var width = Math.Max(1, ClientSize.Width - Padding.Horizontal);
        var height = _editor.GetPreferredSize(new Size(width, 0)).Height;
        // Native single-line editors have a fixed preferred height. Center that height
        // instead of docking it over the frame's lower stroke at fractional DPI.
        _editor.SetBounds(Padding.Left, Math.Max(Padding.Top, (ClientSize.Height - height) / 2), width, height);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(ContainsFocus ? Theme.Focus : Theme.Border, ContainsFocus ? 2 : 1);
        e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
    }
}

internal sealed class ActionButton : Button
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }
    private bool hovering;
    private bool pressing;
    public ActionButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Height = 44;
        Width = 144;
        Margin = new Padding(0);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovering = pressing = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressing = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressing = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) pressing = true; Invalidate(); base.OnKeyDown(e); }
    protected override void OnKeyUp(KeyEventArgs e) { pressing = false; Invalidate(); base.OnKeyUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Canvas);
        var background = !Enabled ? Theme.Raised : Primary
            ? pressing ? Theme.AccentPressed : hovering ? Theme.AccentHover : Theme.Accent
            : pressing ? Theme.Selection : hovering ? Theme.Hover : Theme.Raised;
        var foreground = Enabled && Primary ? Theme.OnAccent : Enabled ? Theme.Text : Theme.Muted;
        using var shape = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 7);
        using var fill = new SolidBrush(background);
        e.Graphics.FillPath(fill, shape);
        using var pen = new Pen(Focused ? Theme.Focus : hovering ? Theme.Secondary : Primary && Enabled ? Theme.Accent : Theme.Border,
            Focused || hovering ? 2 : 1);
        e.Graphics.DrawPath(pen, shape);
        var bounds = ClientRectangle;
        if (pressing) bounds.Offset(0, 1);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -6, -6), foreground, background);
    }
}
