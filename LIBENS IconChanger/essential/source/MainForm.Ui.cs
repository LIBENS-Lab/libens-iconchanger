namespace LibensIconChanger;

internal sealed partial class MainForm
{
    private Label _emptyHistory = null!;
    private Label _historyCount = null!;
    private Label _previewEmpty = null!;
    private Label _previewCaption = null!;
    private TableLayoutPanel _rootLayout = null!;
    private TableLayoutPanel _fieldsLayout = null!;
    private TableLayoutPanel _iconOptionsRow = null!;
    private TableLayoutPanel _overlayOptionsRow = null!;
    private Control _iconIndexLabel = null!;
    private Control _iconIndexFrame = null!;
    private Control _iconIndexHint = null!;
    private readonly ToolTip _tips = new() { AutoPopDelay = 12000 };
    private readonly Dictionary<Control, string> _localizedTips = new();

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitle(Handle);
    }

    private static Label Caption(string text, float size = 10, bool bold = false, Color? color = null) => new()
    {
        Text = text, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = color ?? Theme.Text, Margin = Padding.Empty,
        Font = new Font("맑은 고딕", size, bold ? FontStyle.Bold : FontStyle.Regular)
    };

    private static TableLayoutPanel Rows(params int[] heights)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 1, RowCount = heights.Length };
        foreach (var height in heights)
            table.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
        return table;
    }

    private void BuildUi()
    {
        SuspendLayout();
        var root = Rows(84, 348, 16, 0, 64, 26);
        _rootLayout = root;
        var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Canvas };
        root.Dock = DockStyle.Top;
        root.Height = 778;
        root.MinimumSize = new Size(0, 778);
        viewport.Controls.Add(root);
        viewport.Resize += (_, _) => root.Height = Math.Max(root.MinimumSize.Height, viewport.ClientSize.Height);
        root.Padding = new Padding(28, 14, 28, 18);
        Controls.Add(viewport);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
        var brand = new PictureBox { Image = new Bitmap(Path.Combine(AppContext.BaseDirectory, "assets", "app.png")),
            SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(50, 50), Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        header.Controls.Add(brand, 0, 0);
        var titles = Rows(34, 28);
        titles.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        titles.Height = 62;
        titles.Controls.Add(Caption(AppIdentity.DisplayName, 21, true), 0, 0);
        titles.Controls.Add(Caption(AppText.T("파일 확장자부터 Windows 기본 아이콘까지, 원하는 모습으로."), 9, color: Theme.Secondary), 0, 1);
        header.Controls.Add(titles, 1, 0);
        if (SystemIconCatalog.IsAdministrator)
            header.Controls.Add(Caption(AppText.T("●  관리자 모드"), 9, true, Theme.Accent), 2, 0);
        else
        {
            var admin = new ActionButton { Text = AppText.T("관리자 모드 열기"), Height = 32, Width = 154, Anchor = AnchorStyles.Right };
            admin.Click += (_, _) => OpenAdministratorMode();
            header.Controls.Add(admin, 2, 0);
        }
        root.Controls.Add(header, 0, 0);
        var language = new ActionButton { Text = "KO / EN ▾", Height = 32, Width = 98, Anchor = AnchorStyles.Right };
        language.AccessibleName = "Language / 언어";
        language.Click += (_, _) => ShowLanguageChoices(language);
        header.Controls.Add(language, 3, 0);

        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        var settings = new SurfacePanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 0) };
        var fields = Rows(36, 24, 32, 26, 32, 8, 24, 32, 22, 32, 32);
        _fieldsLayout = fields;
        fields.Controls.Add(BuildModeSelector(), 0, 0);
        _targetLabel = Caption(AppText.T("01   변경할 확장자"), 9, true, Theme.Secondary);
        fields.Controls.Add(_targetLabel, 0, 1);
        var extensionRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        extensionRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        extensionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        extensionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        _extensions.AccessibleName = AppText.T("변경할 확장자");
        _extensions.PlaceholderText = AppText.T("예: .txt, .md, .py");
        var targetHost = new Panel { Dock = DockStyle.Fill, Height = 32, Margin = Padding.Empty, BackColor = Theme.Surface };
        _extensionFrame = new InputFrame(_extensions);
        targetHost.Controls.Add(_extensionFrame);
        targetHost.Controls.Add(_systemChoice);
        extensionRow.Controls.Add(targetHost, 0, 0);
        var inspect = new ActionButton { Text = AppText.T("연결 확인"), Height = 32, Dock = DockStyle.Fill, Margin = new Padding(10, 0, 0, 0) };
        _inspectButton = inspect;
        inspect.Click += (_, _) =>
        {
            if (_systemMode) AppDialog.Show(this, _systemTarget.LocalizedName, _systemTarget.LocalizedDescription);
            else UpdateAssociationInfo(true);
        };
        extensionRow.Controls.Add(inspect, 1, 0);
        fields.Controls.Add(extensionRow, 0, 2);
        _associationInfo.Text = AppText.T("쉼표로 구분하면 여러 확장자를 한 번에 변경합니다.");
        _associationInfo.ForeColor = Theme.Muted;
        _associationInfo.Dock = DockStyle.Fill;
        _associationInfo.TextAlign = ContentAlignment.MiddleLeft;
        _associationInfo.Font = new Font(Font.FontFamily, 9);
        _associationInfo.Margin = Padding.Empty;
        fields.Controls.Add(_associationInfo, 0, 3);
        var optionHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = Theme.Surface };
        _preserveExe.Font = new Font(Font.FontFamily, 9);
        _scopeHint = Caption("", 8.5f, color: Theme.Muted);
        optionHost.Controls.Add(_preserveExe);
        optionHost.Controls.Add(_scopeHint);
        fields.Controls.Add(optionHost, 0, 4);
        fields.Controls.Add(Caption(AppText.T("02   사용할 이미지 또는 아이콘"), 9, true, Theme.Secondary), 0, 6);
        var sourceRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sourceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        _iconPath.AccessibleName = AppText.T("아이콘 원본 파일 경로");
        _iconPath.PlaceholderText = AppText.T("이미지 파일을 선택하세요");
        sourceRow.Controls.Add(new InputFrame(_iconPath), 0, 0);
        var browse = new ActionButton { Text = AppText.T("파일 선택…"), Height = 32, Dock = DockStyle.Fill, Margin = new Padding(10, 0, 0, 0) };
        browse.Click += BrowseIcon;
        sourceRow.Controls.Add(browse, 1, 0);
        fields.Controls.Add(sourceRow, 0, 7);
        fields.Controls.Add(Caption("PNG · JPG · GIF · BMP · TIFF · WEBP · SVG · ICO · EXE · DLL", 8.5f, color: Theme.Muted), 0, 8);
        var indexRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, Margin = Padding.Empty };
        _iconOptionsRow = indexRow;
        indexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        indexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        indexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        indexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 12));
        indexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        indexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        indexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _iconIndexLabel = Caption(AppText.T("아이콘 번호"), 9, color: Theme.Secondary);
        indexRow.Controls.Add(_iconIndexLabel, 0, 0);
        _iconIndex.AccessibleName = AppText.T("EXE 또는 DLL 아이콘 번호");
        _iconIndex.Enabled = false;
        _iconIndexFrame = new InputFrame(_iconIndex);
        indexRow.Controls.Add(_iconIndexFrame, 1, 0);
        _iconIndexHint = Caption("   EXE/DLL", 8.5f, color: Theme.Muted);
        indexRow.Controls.Add(_iconIndexHint, 2, 0);
        indexRow.Controls.Add(Caption(AppText.T("모서리 둥글기"), 9, color: Theme.Secondary), 4, 0);
        _cornerRadius.AccessibleName = AppText.T("꽉 찬 이미지의 모서리 둥글기 백분율");
        indexRow.Controls.Add(new InputFrame(_cornerRadius), 5, 0);
        indexRow.Controls.Add(Caption(AppText.T("   % · 0은 원본"), 8.5f, color: Theme.Muted), 6, 0);
        SetLocalizedToolTip(_cornerRadius, AppText.T("0~50%. 네 모서리가 불투명한 정사각형·직사각형 원본에만 적용합니다.\n이미 투명하거나 둥근 모서리가 있는 이미지는 그대로 유지합니다."));
        fields.Controls.Add(indexRow, 0, 9);
        var overlayRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        _overlayOptionsRow = overlayRow;
        overlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        overlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
        overlayRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        overlayRow.Controls.Add(Caption(AppText.T("오버레이 크기"), 9, color: Theme.Secondary), 0, 0);
        _overlayScale.AccessibleName = AppText.T("바로가기 오버레이 크기 백분율");
        overlayRow.Controls.Add(new InputFrame(_overlayScale), 1, 0);
        overlayRow.Controls.Add(Caption(AppText.T("   % · 축소 후 좌측 하단 배치 · 나머지는 투명"), 8.5f, color: Theme.Muted), 2, 0);
        SetLocalizedToolTip(_overlayScale, AppText.T("10~100%. 선택한 이미지 전체를 이 비율로 축소해 좌측 하단에 놓고, 나머지 캔버스는 투명하게 만듭니다.\n'바로가기 화살표 오버레이'를 선택했을 때만 적용합니다."));
        fields.Controls.Add(overlayRow, 0, 10);
        settings.Controls.Add(fields);
        editor.Controls.Add(settings, 0, 0);

        var previewCard = new SurfacePanel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var previewRows = Rows(32, 24, 0, 34, 32);
        previewRows.Controls.Add(Caption(AppText.T("미리보기"), 13, true), 0, 0);
        previewRows.Controls.Add(Caption(AppText.T("적용할 아이콘을 확인하세요"), 9, color: Theme.Muted), 0, 1);
        var previewWell = new Panel { BackColor = Theme.Input, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 8) };
        _preview.Dock = DockStyle.Fill;
        _preview.Visible = false;
        _preview.Padding = new Padding(24);
        _preview.AccessibleName = AppText.T("선택한 아이콘 미리보기");
        _previewEmpty = Caption(AppText.T("이미지를 선택하면\n여기에 표시됩니다"), 10, color: Theme.Muted);
        _previewEmpty.TextAlign = ContentAlignment.MiddleCenter;
        _previewEmpty.BackColor = Theme.Input;
        previewWell.Controls.Add(_preview);
        previewWell.Controls.Add(_previewEmpty);
        _previewEmpty.BringToFront();
        previewRows.Controls.Add(previewWell, 0, 2);
        _previewCaption = Caption(AppText.T("원본 비율 · 투명 배경 유지"), 9, color: Theme.Secondary);
        _previewCaption.TextAlign = ContentAlignment.MiddleCenter;
        previewRows.Controls.Add(_previewCaption, 0, 3);
        var previewHint = Caption(AppText.T("일반 이미지는 ICO로 자동 변환"), 8.5f, color: Theme.Muted);
        previewHint.TextAlign = ContentAlignment.MiddleCenter;
        previewRows.Controls.Add(previewHint, 0, 4);
        previewCard.Controls.Add(previewRows);
        editor.Controls.Add(previewCard, 1, 0);
        root.Controls.Add(editor, 0, 1);

        var historyCard = new SurfacePanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(20, 14, 20, 12) };
        var history = Rows(30, 26, 0);
        var historyHeading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        historyHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        historyHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        historyHeading.Controls.Add(Caption(AppText.T("변경 기록 및 복원"), 12, true), 0, 0);
        _historyCount = Caption(AppText.T("0개 항목"), 9, color: Theme.Accent);
        _historyCount.TextAlign = ContentAlignment.MiddleRight;
        historyHeading.Controls.Add(_historyCount, 1, 0);
        history.Controls.Add(historyHeading, 0, 0);
        history.Controls.Add(Caption(AppText.T("최초 변경 전 설정을 보관합니다. 항목을 선택하면 원래 아이콘으로 복원할 수 있습니다."), 9, color: Theme.Muted), 0, 1);
        var listHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0), BackColor = Theme.Surface };
        _backups.Dock = DockStyle.Fill;
        _backups.BackColor = Theme.Surface;
        _backups.ForeColor = Theme.Text;
        _backups.BorderStyle = BorderStyle.None;
        _backups.OwnerDraw = true;
        _backups.ShowItemToolTips = true;
        _backups.AccessibleName = AppText.T("변경한 아이콘 백업 목록");
        _backups.Columns.Add(AppText.T("변경 대상"), 125);
        _backups.Columns.Add(AppText.T("적용 범위 / 연결 형식"), 210);
        _backups.Columns.Add(AppText.T("적용한 아이콘"), 340);
        _backups.Columns.Add(AppText.T("변경 시각"), 150);
        _backups.DrawColumnHeader += (_, e) =>
        {
            using var brush = new SolidBrush(Theme.Raised);
            e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text, _backups.Font,
                Rectangle.Inflate(e.Bounds, -8, 0), Theme.Secondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        _backups.DrawItem += (_, _) => { };
        _backups.DrawSubItem += (_, e) =>
        {
            using var brush = new SolidBrush(e.Item!.Selected ? Theme.Selection : Theme.Surface);
            e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text, _backups.Font, Rectangle.Inflate(e.Bounds, -8, 0),
                Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (e.Item.Selected && e.ColumnIndex == 0)
            {
                using var mark = new SolidBrush(Theme.Accent);
                e.Graphics.FillRectangle(mark, e.Bounds.X, e.Bounds.Y + 2, 3, e.Bounds.Height - 4);
            }
            if (e.Item.Focused && _backups.Focused) e.DrawFocusRectangle(e.Bounds);
        };
        _backups.Resize += (_, _) => FitHistoryColumns();
        listHost.Controls.Add(_backups);
        _emptyHistory = Caption(AppText.T("아직 변경한 아이콘이 없습니다\n위에서 변경 대상과 이미지를 선택해 시작하세요."), 10, color: Theme.Muted);
        _emptyHistory.TextAlign = ContentAlignment.MiddleCenter;
        _emptyHistory.BackColor = Theme.Surface;
        listHost.Controls.Add(_emptyHistory);
        _emptyHistory.BringToFront();
        history.Controls.Add(listHost, 0, 2);
        historyCard.Controls.Add(history);
        root.Controls.Add(historyCard, 0, 3);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Margin = Padding.Empty, Padding = new Padding(0, 14, 0, 6) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 156));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        _restoreButton = new ActionButton { Text = AppText.T("선택 항목 복원"), Dock = DockStyle.Fill, Enabled = false, Margin = new Padding(0, 0, 10, 0) };
        _restoreButton.Click += RestoreSelected;
        var backupFolder = new ActionButton { Text = AppText.T("백업 폴더 열기"), Dock = DockStyle.Fill };
        backupFolder.Click += (_, _) => OpenBackupFolder();
        _restartExplorerButton.Margin = new Padding(10, 0, 0, 0);
        _restartExplorerButton.Click += async (_, _) => await RestartExplorerAsync();
        var apply = new ActionButton { Text = AppText.T("아이콘 적용  →"), Primary = true, Dock = DockStyle.Fill };
        apply.Click += ApplyIcon;
        actions.Controls.Add(_restoreButton, 0, 0);
        actions.Controls.Add(backupFolder, 1, 0);
        actions.Controls.Add(_restartExplorerButton, 2, 0);
        actions.Controls.Add(apply, 4, 0);
        root.Controls.Add(actions, 0, 4);
        _status.Text = AppText.T("●  준비됨  ·  적용 전에 현재 아이콘 설정을 자동으로 백업합니다.");
        _status.ForeColor = Theme.Secondary;
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Font = new Font(Font.FontFamily, 9);
        root.Controls.Add(_status, 0, 5);
        AcceptButton = apply;
        SetLocalizedToolTip(_extensions, AppText.T("확장자는 쉼표, 세미콜론 또는 공백으로 구분합니다."));
        SetLocalizedToolTip(_associationInfo, AppText.T("같은 연결 형식을 공유하는 확장자는 아이콘이 함께 바뀔 수 있습니다."));
        SetLocalizedToolTip(_iconPath, AppText.T("PNG 등 일반 이미지 또는 ICO / EXE / DLL을 선택하세요."));
        SetLocalizedToolTip(_restoreButton, AppText.T("선택한 항목을 최초 변경 전 아이콘으로 복원합니다."));
        SetLocalizedToolTip(_restartExplorerButton, AppText.T("아이콘·폴더 미리보기 캐시를 다시 만든 뒤 탐색기를 재시작합니다. 실행 전 확인합니다."));
        RefreshTargetUi();
        ResumeLayout(true);
    }

    private void SetIconIndexOptionVisible(bool visible)
    {
        _iconIndexLabel.Visible = visible;
        _iconIndexFrame.Visible = visible;
        _iconIndexHint.Visible = visible;
        float scale = Math.Max(1f, (_cornerRadius.Parent?.Height ?? 32) / 32f);
        _iconOptionsRow.ColumnStyles[0].Width = visible ? (float)Math.Round(76 * scale) : 0;
        _iconOptionsRow.ColumnStyles[1].Width = visible ? (float)Math.Round(72 * scale) : 0;
        _iconOptionsRow.ColumnStyles[2].Width = visible ? (float)Math.Round(80 * scale) : 0;
        _iconOptionsRow.ColumnStyles[3].Width = visible ? (float)Math.Round(12 * scale) : 0;
        RepaintDynamicOptions();
    }

    private void SetOverlayOptionVisible(bool visible)
    {
        float oldHeight = _fieldsLayout.RowStyles[10].Height;
        float newHeight = visible ? Math.Max(32, _overlayScale.Parent?.Height ?? 32) : 0;
        _overlayOptionsRow.Visible = visible;
        _fieldsLayout.RowStyles[10].Height = newHeight;
        _rootLayout.RowStyles[1].Height = Math.Max(1, _rootLayout.RowStyles[1].Height - oldHeight + newHeight);
        RepaintDynamicOptions();
    }

    private void RepaintDynamicOptions()
    {
        _rootLayout.PerformLayout();
        _fieldsLayout.PerformLayout();
        _fieldsLayout.Parent?.Invalidate(true);
        _rootLayout.Invalidate(true);
    }

    private void FitHistoryColumns()
    {
        if (_backups.Columns.Count != 4) return;
        int available = _backups.ClientSize.Width;
        _backups.Columns[0].Width = (int)(available * .14);
        _backups.Columns[1].Width = (int)(available * .25);
        _backups.Columns[2].Width = (int)(available * .39);
        _backups.Columns[3].Width = Math.Max(80, available - _backups.Columns[0].Width - _backups.Columns[1].Width - _backups.Columns[2].Width - 4);
    }

    private void SetLocalizedToolTip(Control control, string text)
    {
        _localizedTips[control] = AppText.Original(text);
        _tips.SetToolTip(control, text);
    }

    private void ShowLanguageChoices(Control anchor)
    {
        var menu = new ContextMenuStrip { BackColor = Theme.Raised, ForeColor = Theme.Text,
            Renderer = new DarkMenuRenderer(), ShowImageMargin = false, Font = Font };
        foreach (var (id, name) in new[] { ("ko", "한국어"), ("en", "English") })
        {
            var item = new ToolStripMenuItem(name) { Checked = AppText.Language == id };
            item.Click += (_, _) => SetUiLanguage(id);
            menu.Items.Add(item);
        }
        menu.Closed += (_, _) => BeginInvoke(() => menu.Dispose());
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    internal void SetUiLanguage(string language)
    {
        AppText.SetLanguage(language);
        SuspendLayout();
        AppText.Apply(this);
        foreach (var tip in _localizedTips) _tips.SetToolTip(tip.Key, AppText.CatalogText(tip.Value));
        foreach (ListViewItem item in _backups.Items)
        {
            if (item.Tag is not BackupEntry[] entries) continue;
            item.Text = string.Join(", ", entries.SelectMany(entry => entry.Extensions)
                .Distinct(StringComparer.OrdinalIgnoreCase).Select(AppText.T));
            var latest = entries.OrderByDescending(entry => entry.AppliedAtUtc).First();
            item.SubItems[1].Text = AppText.T(entries.Any(entry => entry.Machine) ? "전체 사용자 · " : "현재 사용자 · ")
                + AppText.T(latest.ProgId ?? "확장자");
        }
        _historyCount.Text = AppText.F("{0}개 항목", _backups.Items.Count);
        _status.Text = AppText.T("●  준비됨  ·  적용 전에 현재 아이콘 설정을 자동으로 백업합니다.");
        _status.ForeColor = Theme.Secondary;
        RefreshTargetUi();
        ResumeLayout(true);
        Invalidate(true);
    }

    internal void SetCheckState()
    {
        _extensions.Text = ".txt, .md";
        _iconPath.Text = Path.Combine(AppContext.BaseDirectory, "assets", "app.png");
        var item = new ListViewItem(".example");
        item.SubItems.Add(AppText.T("검수용 예시 · 실제 변경 없음"));
        item.SubItems.Add(@"C:\Example\Long path\Icon assets\Sample icon.png");
        item.SubItems.Add("2026-09-06 10:00");
        _backups.Items.Add(item);
        item.Selected = true;
        _emptyHistory.Visible = false;
        _backups.Visible = true;
        _historyCount.Text = AppText.T("검수용 예시");
    }
}
