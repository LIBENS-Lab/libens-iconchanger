using System.Runtime.InteropServices;

namespace LibensIconChanger;

// OwnerDraw only paints populated header cells. The native header still paints
// its unused trailing area with the Windows light theme unless handled separately.
internal sealed class DarkListView : ListView
{
    private HeaderWindow? _header;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var headerHandle = SendMessage(Handle, 0x101F, IntPtr.Zero, IntPtr.Zero); // LVM_GETHEADER
        if (headerHandle != IntPtr.Zero) _header = new HeaderWindow(headerHandle);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _header?.ReleaseHandle();
        _header = null;
        base.OnHandleDestroyed(e);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr GetItemRect(IntPtr hwnd, int message, IntPtr index, ref NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    private sealed class HeaderWindow : NativeWindow
    {
        public HeaderWindow(IntPtr handle) => AssignHandle(handle);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg is not (0x000F or 0x0317 or 0x0318)) return; // PAINT, PRINT, PRINTCLIENT
            if (!GetClientRect(Handle, out var client)) return;
            int count = (int)SendMessage(Handle, 0x1200, IntPtr.Zero, IntPtr.Zero); // HDM_GETITEMCOUNT
            int rightEdge = 0;
            for (int index = 0; index < count; index++)
            {
                var rect = new NativeRect();
                if (GetItemRect(Handle, 0x1207, (IntPtr)index, ref rect) != IntPtr.Zero)
                    rightEdge = Math.Max(rightEdge, rect.Right);
            }
            if (rightEdge >= client.Right) return;
            bool ownDc = m.Msg == 0x000F;
            var dc = ownDc ? GetDC(Handle) : m.WParam;
            if (dc == IntPtr.Zero) return;
            try
            {
                using var graphics = Graphics.FromHdc(dc);
                using var background = new SolidBrush(Theme.Raised);
                graphics.FillRectangle(background, rightEdge, client.Top, client.Right - rightEdge, client.Bottom - client.Top);
            }
            finally { if (ownDc) ReleaseDC(Handle, dc); }
        }
    }
}
