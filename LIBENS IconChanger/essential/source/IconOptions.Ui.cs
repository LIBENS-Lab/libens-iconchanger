using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LibensIconChanger;

internal sealed partial class MainForm
{
    private bool _systemMode;
    private SystemIconTarget _systemTarget = SystemIconCatalog.Items[0];
    private readonly ActionButton _extensionMode = new() { Text = AppText.T("파일 확장자"), Height = 32, Dock = DockStyle.Fill };
    private readonly ActionButton _windowsMode = new() { Text = AppText.T("Windows 기본 아이콘"), Height = 32, Dock = DockStyle.Fill };
    private readonly ActionButton _systemChoice = new() { Dock = DockStyle.Fill, Height = 32 };
    private readonly DarkCheckBox _preserveExe = new() { Text = AppText.T("자체 아이콘이 없는 EXE에만 적용"), Checked = true, Dock = DockStyle.Fill };
    private readonly ActionButton _restartExplorerButton = new() { Text = AppText.T("캐시 갱신 · 재시작"), Dock = DockStyle.Fill };
    private Label _targetLabel = null!;
    private Label _scopeHint = null!;
    private InputFrame _extensionFrame = null!;
    private ActionButton _inspectButton = null!;

    private Control BuildModeSelector()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 0, 0, 4) };
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        _extensionMode.Margin = new Padding(0, 0, 8, 0);
        _extensionMode.Click += (_, _) => { _systemMode = false; RefreshTargetUi(); };
        _windowsMode.Click += (_, _) => { _systemMode = true; RefreshTargetUi(); };
        row.Controls.Add(_extensionMode, 0, 0);
        row.Controls.Add(_windowsMode, 1, 0);
        _systemChoice.Click += (_, _) => ShowSystemChoices();
        _preserveExe.CheckedChanged += (_, _) => RefreshTargetUi();
        return row;
    }

    private void ShowSystemChoices()
    {
        var menu = CreateSystemChoicesMenu();
        menu.Show(_systemChoice, new Point(0, _systemChoice.Height));
    }

    internal ContextMenuStrip CreateSystemChoicesMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = true, ShowCheckMargin = false,
            ImageScalingSize = new Size(32, 32), BackColor = Theme.Raised, ForeColor = Theme.Text,
            Renderer = new DarkMenuRenderer(), Font = Font };
        foreach (var target in SystemIconCatalog.Items)
        {
            var item = new ToolStripMenuItem((target == _systemTarget ? "✓  " : "    ") + target.LocalizedName + (target.RequiresAdmin ? AppText.T("   · 전체 사용자") : ""))
            {
                Image = SystemIconCatalog.CreatePreview(target, 32),
                ImageScaling = ToolStripItemImageScaling.SizeToFit,
                Padding = new Padding(2, 3, 8, 3)
            };
            item.Click += (_, _) => { _systemTarget = target; RefreshTargetUi(); };
            menu.Items.Add(item);
        }
        menu.Closed += (_, _) => BeginInvoke(() => DisposeSystemChoicesMenu(menu));
        return menu;
    }

    internal static void DisposeSystemChoicesMenu(ContextMenuStrip menu)
    {
        foreach (ToolStripItem item in menu.Items)
        {
            var image = item.Image;
            item.Image = null;
            image?.Dispose();
        }
        menu.Dispose();
    }

    private void RefreshTargetUi()
    {
        if (_targetLabel is null) return;
        _extensionMode.Primary = !_systemMode; _extensionMode.Invalidate();
        _windowsMode.Primary = _systemMode; _windowsMode.Invalidate();
        _targetLabel.Text = _systemMode ? AppText.T("01   변경할 Windows 기본 아이콘") : AppText.T("01   변경할 확장자");
        _extensionFrame.Visible = !_systemMode;
        _systemChoice.Visible = _systemMode;
        _systemChoice.Text = _systemTarget.LocalizedName + "   ▾";
        _inspectButton.Text = _systemMode ? AppText.T("범위 확인") : AppText.T("연결 확인");
        bool hasExe;
        try { hasExe = ExtensionParser.Parse(_extensions.Text).Contains(".exe"); }
        catch { hasExe = false; }
        _preserveExe.Visible = !_systemMode && hasExe;
        _preserveExe.Text = AppText.T("자체 아이콘이 없는 EXE에만 적용") + (_preserveExe.Checked ? AppText.T("  · 관리자 필요") : "");
        _scopeHint.Visible = !_preserveExe.Visible;
        _scopeHint.Text = _systemMode
            ? (_systemTarget.RequiresAdmin ? AppText.T("전체 사용자 · 적용/복원에 관리자 권한 필요") : AppText.T("현재 사용자 · 관리자 권한 불필요"))
            : AppText.T("EXE 입력 시 기존 아이콘 보존 옵션을 선택할 수 있습니다.");
        bool shortcutOverlay = _systemMode && _systemTarget.Id == "shortcut-overlay";
        _overlayScale.Enabled = shortcutOverlay;
        SetOverlayOptionVisible(shortcutOverlay);
        if (_systemMode)
            _associationInfo.Text = _systemTarget.LocalizedDescription.Split('\n')[0];
        else UpdateAssociationInfo();
        _localizedTips[_systemChoice] = _systemTarget.Description;
        _tips.SetToolTip(_systemChoice, _systemTarget.LocalizedDescription);
        SetLocalizedToolTip(_preserveExe, AppText.T("선택: 기본 대체 아이콘만 변경(전체 사용자, 관리자 필요).\n해제: 현재 사용자의 EXE 아이콘을 일괄 지정.\nEXE 파일 내용·실행 연결·전자서명은 수정하지 않습니다."));
        UpdatePreview();
    }

    private AssociationTarget[] GetTargets()
    {
        if (_systemMode) return _systemTarget.Locations;
        var extensions = ExtensionParser.Parse(_extensions.Text);
        if (extensions.Count == 0) throw new InvalidOperationException(AppText.T("변경할 확장자를 하나 이상 입력하세요."));
        return SystemIconCatalog.ExtensionTargets(extensions, _preserveExe.Checked);
    }

    private async Task RestartExplorerAsync()
    {
        if (!AppDialog.Show(this, AppText.T("아이콘 캐시 갱신 · 탐색기 다시 시작"),
            AppText.T("Windows 탐색기의 폴더 창과 작업 표시줄이 잠시 닫혔다가 자동으로 다시 열립니다.\n\n") +
            AppText.T("진행 중인 파일 복사·이동·삭제가 있다면 먼저 끝내세요. 문서 편집 프로그램은 종료하지 않습니다.\n\n") +
            AppText.T("아이콘·폴더 미리보기 캐시 파일은 삭제 후 Windows가 자동으로 다시 만듭니다. 실제 문서와 사진은 삭제하지 않습니다.\n\n") +
            AppText.T("저장된 Windows 기본 아이콘을 지금 반영할까요?"), true)) return;
        _restartExplorerButton.Enabled = false;
        SetStatus(AppText.T("아이콘 캐시를 갱신하고 탐색기를 다시 시작하고 있습니다…"), true);
        try
        {
            int cleared = await Task.Run(() => NativeShell.RestartExplorer(rebuildCaches: true));
            NativeShell.NotifyAssociationChanged();
            NativeShell.NotifyDesktopItemsChanged();
            SetStatus(AppText.F("탐색기를 다시 시작했습니다. 캐시 {0}개를 갱신했습니다.", cleared), true);
        }
        catch (Exception ex)
        {
            SetStatus(AppText.T("탐색기를 다시 시작하지 못했습니다."), false);
            AppDialog.Show(this, AppText.T("탐색기 다시 시작 오류"), ex.Message);
        }
        finally { _restartExplorerButton.Enabled = true; }
    }

    private void OpenAdministratorMode()
    {
        if (!AppDialog.Show(this, AppText.T("관리자 모드 열기"), AppText.T("Windows 기본 아이콘과 EXE 대체 아이콘 설정은 전체 사용자에게 적용됩니다.\n\n새 창을 관리자 권한으로 열까요? 승인되어 새 창이 시작되면 현재 창은 자동으로 닫힙니다. 이 동작만으로 아이콘을 적용하지는 않습니다.\n\n다른 관리자 계정으로 인증하면 '현재 사용자' 설정은 그 관리자 계정에 적용됩니다."), true)) return;
        try
        {
            var state = new EditState(_systemMode, _systemTarget.Id, _extensions.Text, _iconPath.Text,
                (int)_iconIndex.Value, (int)_cornerRadius.Value, (int)_overlayScale.Value, _preserveExe.Checked);
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
            start.ArgumentList.Add("--lang");
            start.ArgumentList.Add(AppText.Language);
            start.ArgumentList.Add("--edit-state");
            start.ArgumentList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state))));
            var elevated = Process.Start(start)
                ?? throw new InvalidOperationException(AppText.T("관리자 창의 시작 여부를 확인하지 못했습니다."));
            Close();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { SetStatus(AppText.T("관리자 실행이 취소되었습니다. 설정은 변경하지 않았습니다."), false); }
        catch (Exception ex) { AppDialog.Show(this, AppText.T("관리자 실행 오류"), ex.Message); }
    }

    internal void RestoreEditState(string encoded)
    {
        try
        {
            var state = JsonSerializer.Deserialize<EditState>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
            if (state is null) return;
            _systemMode = state.SystemMode;
            _systemTarget = SystemIconCatalog.Items.FirstOrDefault(i => i.Id == state.TargetId) ?? SystemIconCatalog.Items[0];
            _extensions.Text = state.Extensions; _iconPath.Text = state.IconPath;
            _iconIndex.Value = Math.Clamp(state.Index, 0, 9999);
            _cornerRadius.Value = Math.Clamp(state.CornerRadius, 0, 50);
            _overlayScale.Value = state.OverlayScale == 0 ? 50 : Math.Clamp(state.OverlayScale, 10, 100);
            _preserveExe.Checked = state.PreserveExe;
            RefreshTargetUi();
        }
        catch (Exception ex) { SetStatus(AppText.T("이전 창의 입력을 불러오지 못했습니다: ") + ex.Message, false); }
    }

    internal void SetSystemCheckState(bool exe = false)
    {
        _systemMode = !exe;
        if (exe) _extensions.Text = ".exe";
        RefreshTargetUi();
    }

    internal void SetShortcutOverlayCheckState()
    {
        _systemMode = true;
        _systemTarget = SystemIconCatalog.Items.Single(item => item.Id == "shortcut-overlay");
        RefreshTargetUi();
    }

    internal void SetNativeIconSourceCheckState() => _iconPath.Text = Environment.ProcessPath!;

    private sealed record EditState(bool SystemMode, string TargetId, string Extensions, string IconPath,
        int Index, int CornerRadius, int OverlayScale, bool PreserveExe);
}

internal sealed class DarkCheckBox : CheckBox
{
    private bool _hover;
    private bool _pressed;
    public DarkCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ForeColor = Theme.Text; BackColor = Theme.Surface; Margin = Padding.Empty;
    }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        int side = Math.Max(16, Font.Height - 1), y = (Height - side) / 2;
        var box = new Rectangle(2, y, side, side);
        var fillColor = !Enabled ? Theme.Raised : Checked ? (_pressed ? Theme.AccentPressed : _hover ? Theme.AccentHover : Theme.Accent) : Theme.Input;
        using var fill = new SolidBrush(fillColor); e.Graphics.FillRectangle(fill, box);
        using var border = new Pen(Focused ? Theme.Focus : _hover ? Theme.Secondary : Theme.Border, Focused ? 2 : 1);
        e.Graphics.DrawRectangle(border, box);
        if (Checked) TextRenderer.DrawText(e.Graphics, "✓", Font, box, Enabled ? Theme.OnAccent : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(side + 10, 0, Width - side - 10, Height), Enabled ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -1, -1), Theme.Focus, BackColor);
    }
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) => e.Graphics.Clear(Theme.Raised);
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    { using var brush = new SolidBrush(Theme.Surface); e.Graphics.FillRectangle(brush, e.AffectedBounds); }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    { using var pen = new Pen(Theme.Border); e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    { using var brush = new SolidBrush(e.Item.Selected ? Theme.Selection : Theme.Raised); e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size)); }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    { e.TextColor = e.Item.Enabled ? Theme.Text : Theme.Muted; base.OnRenderItemText(e); }
}

internal static class AppDialog
{
    public static bool Show(Form owner, string title, string text, bool confirm = false)
    {
        using var form = Create(owner, title, text, confirm);
        return form.ShowDialog(owner) == DialogResult.OK;
    }

    internal static Form Create(Form owner, string title, string text, bool confirm)
    {
        var form = new Form { Text = title, BackColor = Theme.Canvas, ForeColor = Theme.Text,
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false };
        form.SuspendLayout();
        form.Font = new Font("맑은 고딕", 10);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(96, 96);
        form.ClientSize = new Size(610, 350);
        form.Padding = new Padding(22);
        form.HandleCreated += (_, _) => Theme.DarkTitle(form.Handle);
        var body = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.Canvas,
            ForeColor = Theme.Text, Text = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine), Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, TabStop = true };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft };
        var ok = new ActionButton { Text = confirm ? AppText.T("계속") : AppText.T("확인"), Primary = true, DialogResult = DialogResult.OK, Width = 106 };
        var cancel = new ActionButton { Text = AppText.T("취소"), DialogResult = DialogResult.Cancel, Width = 106, Margin = new Padding(0, 0, 8, 0) };
        footer.Controls.Add(ok); if (confirm) footer.Controls.Add(cancel);
        form.Controls.Add(body); form.Controls.Add(footer);
        form.AcceptButton = confirm ? cancel : ok; form.CancelButton = confirm ? cancel : ok;
        form.Shown += (_, _) => { body.Select(0, 0); form.ActiveControl = confirm ? cancel : ok; };
        form.ResumeLayout(true);
        return form;
    }
}
