using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace LibensIconChanger;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        int languageIndex = Array.IndexOf(args, "--lang");
        if (languageIndex >= 0)
        {
            if (languageIndex + 1 >= args.Length) throw new ArgumentException("Use --lang ko or --lang en.");
            AppText.SetLanguage(args[languageIndex + 1]);
            args = args.Take(languageIndex).Concat(args.Skip(languageIndex + 2)).ToArray();
        }
        if (args.Length == 2 && args[0] == "--ui-check")
        {
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.SetCompatibleTextRenderingDefault(false);
            Application.EnableVisualStyles();
            Theme.ConfigureNativeColorMode();
            UiCheck.Run(args[1]);
            return;
        }
        if (args.Length == 1 && args[0] == "--refresh-shell")
        {
            NativeShell.NotifyAssociationChanged();
            return;
        }
        if (args.Length == 1 && args[0] == "--refresh-folders")
        {
            NativeShell.NotifyStockIconsChanged([3, 4]);
            NativeShell.NotifyDesktopItemsChanged();
            return;
        }
        ApplicationConfiguration.Initialize();
        Theme.ConfigureNativeColorMode();
        if (args.Length == 2 && args[0] == "--ui-check-native")
        {
            UiCheck.RunNative(args[1]);
            return;
        }
        using var main = new MainForm();
        if (args.Length == 2 && args[0] == "--edit-state") main.RestoreEditState(args[1]);
        Application.Run(main);
    }
}

internal sealed partial class MainForm : Form
{
    private readonly TextBox _extensions = new() { PlaceholderText = ".txt, .md, .py" };
    private readonly TextBox _iconPath = new() { PlaceholderText = AppText.T("PNG, JPG, ICO, EXE, DLL 또는 다른 이미지 파일") };
    private readonly NumericUpDown _iconIndex = new() { Minimum = 0, Maximum = 9999, Width = 90 };
    private readonly NumericUpDown _cornerRadius = new() { Minimum = 0, Maximum = 50, Value = 16, Width = 90 };
    private readonly NumericUpDown _overlayScale = new() { Minimum = 10, Maximum = 100, Value = 50, Width = 90 };
    private readonly PictureBox _preview = new() { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Input };
    private readonly Label _associationInfo = new() { AutoEllipsis = true };
    private readonly Label _status = new() { AutoEllipsis = true };
    private readonly DarkListView _backups = new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = true,
        HideSelection = false
    };

    private readonly BackupStore _backupStore;
    private readonly BackupStore _machineBackupStore;
    private readonly IconChangeService _changes;

    public MainForm(bool checkMode = false)
    {
        SuspendLayout();
        _backupStore = new BackupStore(checkMode);
        _machineBackupStore = new BackupStore(checkMode, machine: true);
        _changes = new IconChangeService(_backupStore, _machineBackupStore);
        Text = AppIdentity.DisplayName;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 660);
        ClientSize = new Size(1080, 778);
        Font = new Font("맑은 고딕", 10F);
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "assets", "app.ico"));
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);

        BuildUi();
        AppText.Apply(this);
        LoadBackups();
        _extensions.TextChanged += (_, _) => RefreshTargetUi();
        _iconPath.TextChanged += (_, _) => UpdatePreview();
        _iconIndex.ValueChanged += (_, _) => UpdatePreview();
        _cornerRadius.ValueChanged += (_, _) => UpdatePreview();
        _overlayScale.ValueChanged += (_, _) => UpdatePreview();
        _backups.SelectedIndexChanged += (_, _) => UpdateRestoreButton();
        Shown += (_, _) => _extensions.Focus();
        Activated += (_, _) => { if (!checkMode) { _changes.Reload(); LoadBackups(); } };
        ResumeLayout(true);
    }

    private Button _restoreButton = null!;

    private void BrowseIcon(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = AppText.T("아이콘 파일 선택"),
            Filter = AppText.T("지원 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.svg;*.ico;*.exe;*.dll|이미지 파일|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.svg|아이콘 리소스|*.ico;*.exe;*.dll|모든 파일 (*.*)|*.*"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _iconPath.Text = dialog.FileName;
    }

    private void UpdatePreview()
    {
        _preview.Image?.Dispose();
        _preview.Image = null;
        _preview.Visible = false;
        _previewEmpty.Visible = true;
        var path = _iconPath.Text.Trim().Trim('"');
        bool supportsIconIndex = File.Exists(path) && new[] { ".exe", ".dll" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        _iconIndex.Enabled = supportsIconIndex;
        SetIconIndexOptionVisible(supportsIconIndex);
        if (!File.Exists(path)) return;
        try
        {
            bool shortcutOverlay = _systemMode && _systemTarget.Id == "shortcut-overlay";
            _preview.Image = IconSource.CreatePreview(path, (int)_iconIndex.Value, (int)_cornerRadius.Value,
                out var rounded, shortcutOverlay, (int)_overlayScale.Value);
            _previewEmpty.Visible = _preview.Image is null;
            _preview.Visible = _preview.Image is not null;
            _previewCaption.Text = shortcutOverlay
                ? AppText.F("오버레이 {0}% · 좌측 하단", (int)_overlayScale.Value)
                : (int)_cornerRadius.Value == 0
                ? AppText.T("모서리 원본 유지")
                : rounded ? AppText.F("모서리 둥글기 {0}% 적용", (int)_cornerRadius.Value) : AppText.T("투명 모서리 감지 · 원본 유지");
        }
        catch
        {
            // 입력 중의 미리보기 오류는 적용 시 자세히 안내한다.
        }
    }

    private void UpdateAssociationInfo(bool showErrors = false)
    {
        try
        {
            var extensions = ExtensionParser.Parse(_extensions.Text);
            if (extensions.Count == 0)
            {
                _associationInfo.Text = AppText.T("쉼표 또는 세미콜론으로 여러 확장자를 입력할 수 있습니다.");
                return;
            }

            var descriptions = extensions.Select(extension =>
            {
                var target = AssociationResolver.Resolve(extension);
                return $"{extension} → {(target.ProgId ?? AppText.T("확장자 자체 설정"))}";
            });
            _associationInfo.Text = string.Join("   |   ", descriptions);
        }
        catch (Exception ex)
        {
            _associationInfo.Text = ex.Message;
            if (showErrors)
                AppDialog.Show(this, AppText.T("확장자 확인"), ex.Message);
        }
    }

    private void ApplyIcon(object? sender, EventArgs e)
    {
        try
        {
            var targets = GetTargets();
            bool machine = targets.Any(t => t.Machine);
            if (machine && !SystemIconCatalog.IsAdministrator)
                throw new UnauthorizedAccessException(AppText.T("선택한 설정은 전체 사용자에게 적용됩니다. 오른쪽 위 '관리자 모드 열기'로 실행한 뒤 적용해 주세요."));

            var iconFile = Path.GetFullPath(_iconPath.Text.Trim().Trim('"'));
            if (!File.Exists(iconFile))
                throw new FileNotFoundException(AppText.T("선택한 아이콘 파일을 찾을 수 없습니다."), iconFile);

            var names = string.Join(", ", targets.Select(t => t.Extension).Distinct().Select(AppText.T));
            bool exe = targets.Any(t => t.GroupId == "extension:exe");
            var details = _systemMode ? _systemTarget.LocalizedDescription : exe
                ? (_preserveExe.Checked ? AppText.T("자체 아이콘은 유지하고 아이콘이 없는 EXE의 기본 대체 아이콘만 바꿉니다.\nEXE 기본 아이콘 참조(%1)는 현재 사용자에 복원합니다. 대체 아이콘은 전체 사용자에게 적용됩니다.")
                    : AppText.T("현재 사용자의 EXE 아이콘을 일괄 지정합니다. 자체 아이콘도 표시가 바뀔 수 있습니다.\n이전에 설정한 전체 사용자용 대체 아이콘은 남습니다. 완전히 되돌리려면 백업 목록에서 복원해 주세요."))
                : AppText.T("현재 사용자에게 적용합니다. 같은 연결 형식(ProgID)을 공유하는 확장자는 함께 바뀔 수 있습니다.");
            if (!AppDialog.Show(this, AppText.T("아이콘 적용 확인"), names + "\n\n" + details +
                AppText.T("\n\n최초 원본을 백업하고 적용합니다. 파일 내용·실행 명령은 바꾸지 않습니다. 반영이 늦으면 작업을 저장한 뒤 직접 로그아웃/로그인해 주세요."), true)) return;
            var storage = _changes.Store(machine);
            storage.EnsureWritable();
            bool shortcutOverlay = _systemMode && _systemTarget.Id == "shortcut-overlay";
            var iconValue = IconSource.PrepareStoredIcon(iconFile, (int)_iconIndex.Value,
                storage.StorageDirectory, (int)_cornerRadius.Value, shortcutOverlay, (int)_overlayScale.Value);
            var writes = targets.Select(t => new IconWrite(t,
                exe && _preserveExe.Checked && t.GroupId == "extension:exe" && !t.Machine ? "%1" : iconValue)).ToArray();
            if (_systemMode && _systemTarget.Id.StartsWith("recycle-", StringComparison.Ordinal))
                writes = SystemIconCatalog.RecycleWrites(targets[0], iconValue);
            _changes.Apply(writes);
            NativeShell.NotifyAssociationChanged();
            NativeShell.NotifyStockIconsChanged(writes.Where(write => write.Target.Machine)
                .Select(write => int.TryParse(write.Target.ValueName, out var id) ? id : -1)
                .Where(id => id >= 0));
            NativeShell.NotifyDesktopItemsChanged();
            LoadBackups();
            SetStatus(AppText.F("설정 저장 완료: {0}. Windows 기본 아이콘은 탐색기 재시작 후 반영됩니다.", names), success: true);
            if (_systemMode || targets.Any(t => t.Machine))
                BeginInvoke(async () => await RestartExplorerAsync());
        }
        catch (Exception ex)
        {
            SetStatus(AppText.T("적용하지 못했습니다."), success: false);
            AppDialog.Show(this, AppText.T("아이콘 적용 오류"), ex.Message);
        }
        finally { _changes.Reload(); LoadBackups(); }
    }

    private void RestoreSelected(object? sender, EventArgs e)
    {
        if (_backups.SelectedItems.Count == 0)
            return;

        var selected = _backups.SelectedItems.Cast<ListViewItem>()
            .SelectMany(item => (BackupEntry[])item.Tag!)
            .ToList();
        var names = string.Join(", ", selected.SelectMany(item => item.Extensions).Distinct(StringComparer.OrdinalIgnoreCase).Select(AppText.T));
        if (!AppDialog.Show(this, AppText.T("아이콘 원복"), AppText.F("{0}의 아이콘 설정을 최초 변경 전 상태로 되돌릴까요?\n\n전체 사용자 설정이 포함되어 있으면 관리자 권한이 필요합니다.", names), true))
            return;

        try
        {
            _changes.Restore(selected);
            NativeShell.NotifyAssociationChanged();
            NativeShell.NotifyStockIconsChanged(selected.Where(item => item.Machine)
                .Select(item => int.TryParse(item.ValueName, out var id) ? id : -1)
                .Where(id => id >= 0));
            NativeShell.NotifyDesktopItemsChanged();
            LoadBackups();
            SetStatus(AppText.F("원복 완료: {0}", names), success: true);
            if (selected.Any(item => item.Machine || item.GroupId.StartsWith("system:", StringComparison.Ordinal)))
                BeginInvoke(async () => await RestartExplorerAsync());
        }
        catch (Exception ex)
        {
            SetStatus(AppText.T("원복하지 못했습니다."), success: false);
            AppDialog.Show(this, AppText.T("원복 오류"), ex.Message);
        }
        finally { _changes.Reload(); LoadBackups(); }
    }

    private void OpenBackupFolder()
    {
        var store = _systemMode && _systemTarget.RequiresAdmin ? _machineBackupStore : _backupStore;
        try
        {
            Directory.CreateDirectory(store.StorageDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", store.StorageDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) { AppDialog.Show(this, AppText.T("백업 폴더 열기"), ex.Message); }
    }

    private void LoadBackups()
    {
        _backups.BeginUpdate();
        _backups.Items.Clear();
        foreach (var group in _changes.Entries.GroupBy(e => e.GroupKey).OrderByDescending(g => g.Max(e => e.AppliedAtUtc)))
        {
            var backup = group.OrderByDescending(e => e.AppliedAtUtc).First();
            var item = new ListViewItem(string.Join(", ", group.SelectMany(e => e.Extensions).Distinct(StringComparer.OrdinalIgnoreCase).Select(AppText.T)));
            item.SubItems.Add((group.Any(e => e.Machine) ? AppText.T("전체 사용자 · ") : AppText.T("현재 사용자 · ")) + AppText.T(backup.ProgId ?? "확장자"));
            item.SubItems.Add(backup.AppliedValue);
            item.SubItems.Add(backup.AppliedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
            item.Tag = group.ToArray();
            item.ToolTipText = string.Join("\n", group.Select(e => $"{(e.Machine ? "HKLM" : "HKCU")}\\{e.UserRegistryPath} [{e.ValueName}]: {e.AppliedValue}"));
            _backups.Items.Add(item);
        }
        _backups.EndUpdate();
        _emptyHistory.Visible = _backups.Items.Count == 0;
        _backups.Visible = _backups.Items.Count > 0;
        _historyCount.Text = AppText.F("{0}개 항목", _backups.Items.Count);
        UpdateRestoreButton();
        if (_backupStore.LoadError is not null || _machineBackupStore.LoadError is not null)
            SetStatus(AppText.T("백업 읽기 오류가 있습니다. 원본 파일을 보존하며 해당 범위의 변경을 차단합니다."), false);
    }

    private void UpdateRestoreButton() => _restoreButton.Enabled = _backups.SelectedItems.Count > 0;

    private void SetStatus(string message, bool success)
    {
        _status.Text = (success ? "✓  " : "!  ") + message;
        _status.ForeColor = success ? Theme.Accent : Theme.Danger;
    }
}

internal static class ExtensionParser
{
    private static readonly char[] Separators = [',', ';', '\r', '\n', '\t', ' '];
    private static readonly char[] InvalidCharacters = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    public static IReadOnlyList<string> Parse(string input)
    {
        var result = new List<string>();
        foreach (var raw in input.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var extension = raw.StartsWith('.') ? raw : $".{raw}";
            if (extension.Length < 2 || extension.IndexOfAny(InvalidCharacters) >= 0)
                throw new InvalidOperationException(AppText.F("올바르지 않은 확장자입니다: {0}", raw));
            if (!result.Contains(extension, StringComparer.OrdinalIgnoreCase))
                result.Add(extension.ToLowerInvariant());
        }
        return result;
    }
}

internal sealed record AssociationTarget(string Extension, string? ProgId, string UserRegistryPath, string? EffectiveIcon,
    bool Machine = false, string ValueName = "", string GroupId = "");

internal static class AssociationResolver
{
    public static AssociationTarget Resolve(string extension)
    {
        var userChoicePath = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice";
        var progId = ReadDefaultOrNamed(Registry.CurrentUser, userChoicePath, "ProgId")
                     ?? ReadDefaultOrNamed(Registry.CurrentUser, $@"Software\Classes\{extension}", null)
                     ?? ReadDefaultOrNamed(Registry.LocalMachine, $@"Software\Classes\{extension}", null);

        if (!string.IsNullOrWhiteSpace(progId) && !progId.Contains('\\'))
        {
            var relativePath = $@"Software\Classes\{progId}\DefaultIcon";
            var effective = ReadDefaultOrNamed(Registry.CurrentUser, relativePath, null)
                            ?? ReadDefaultOrNamed(Registry.LocalMachine, relativePath, null);
            return new AssociationTarget(extension, progId, relativePath, effective);
        }

        var extensionPath = $@"Software\Classes\{extension}\DefaultIcon";
        var extensionIcon = ReadDefaultOrNamed(Registry.CurrentUser, extensionPath, null)
                            ?? ReadDefaultOrNamed(Registry.LocalMachine, extensionPath, null);
        return new AssociationTarget(extension, null, extensionPath, extensionIcon);
    }

    private static string? ReadDefaultOrNamed(RegistryKey hive, string path, string? valueName)
    {
        using var key = hive.OpenSubKey(path, writable: false);
        return key?.GetValue(valueName ?? string.Empty, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }
}

internal static class RegistryWriter
{
    public static RegistryKey OpenRoot(bool machine) => RegistryKey.OpenBaseKey(
        machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);

    public static void SetIcon(AssociationTarget target, string iconValue)
    {
        Validate(target.Machine, target.UserRegistryPath, target.ValueName);
        using var root = OpenRoot(target.Machine);
        using var key = root.CreateSubKey(target.UserRegistryPath, writable: true)
                        ?? throw new UnauthorizedAccessException(AppText.F("레지스트리 키를 만들 수 없습니다: {0}", target.UserRegistryPath));
        key.SetValue(target.ValueName, iconValue, RegistryValueKind.String);
    }

    public static void Validate(bool machine, string path, string name)
    {
        if (machine)
        {
            if (!path.Equals(SystemIconCatalog.ShellIconsPath, StringComparison.OrdinalIgnoreCase) ||
                !SystemIconCatalog.MachineValueNames.Contains(name))
                throw new InvalidOperationException(AppText.T("허용되지 않은 시스템 아이콘 레지스트리 대상입니다."));
            return;
        }
        var prefix = @"Software\Classes\";
        if (name.Length == 0 && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var parts = path[prefix.Length..].Split('\\');
            if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Equals("DefaultIcon", StringComparison.OrdinalIgnoreCase)) return;
        }
        if (SystemIconCatalog.DesktopPaths.Contains(path, StringComparer.OrdinalIgnoreCase) &&
            new[] { "", "empty", "full" }.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
        throw new InvalidOperationException(AppText.T("허용되지 않은 아이콘 레지스트리 대상입니다. 백업을 확인해 주세요."));
    }

    public static void Restore(BackupEntry backup)
    {
        Validate(backup.Machine, backup.UserRegistryPath, backup.ValueName);
        using var root = OpenRoot(backup.Machine);
        if (backup.HadDefaultValue)
        {
            using var key = root.CreateSubKey(backup.UserRegistryPath, writable: true)
                            ?? throw new UnauthorizedAccessException(AppText.F("레지스트리 키를 열 수 없습니다: HKCU\\{0}", backup.UserRegistryPath));
            if (backup.OriginalValueKind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
                throw new InvalidOperationException(AppText.T("문자열이 아닌 원본 레지스트리 값은 자동 복원하지 않습니다."));
            key.SetValue(backup.ValueName, backup.OriginalValue ?? string.Empty, backup.OriginalValueKind);
            return;
        }

        using (var key = root.OpenSubKey(backup.UserRegistryPath, writable: true))
            key?.DeleteValue(backup.ValueName, throwOnMissingValue: false);

        if (!backup.HadKey)
            DeleteKeyOnlyWhenEmpty(root, backup.UserRegistryPath);
    }

    private static void DeleteKeyOnlyWhenEmpty(RegistryKey root, string path)
    {
        using var key = root.OpenSubKey(path, writable: false);
        if (key is null || key.ValueCount != 0 || key.SubKeyCount != 0)
            return;

        var separator = path.LastIndexOf('\\');
        if (separator <= 0)
            return;
        var parentPath = path[..separator];
        var childName = path[(separator + 1)..];
        key.Close();
        using var parent = root.OpenSubKey(parentPath, writable: true);
        parent?.DeleteSubKey(childName, throwOnMissingSubKey: false);
    }
}

internal sealed class BackupEntry
{
    public bool Machine { get; set; }
    public string ValueName { get; set; } = "";
    public string GroupId { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore]
    public string Identity => $"{Machine}|{UserRegistryPath}|{ValueName}";
    [System.Text.Json.Serialization.JsonIgnore]
    public string GroupKey => GroupId.Length > 0 ? GroupId : Identity;
    public List<string> Extensions { get; set; } = [];
    public string? ProgId { get; set; }
    public string UserRegistryPath { get; set; } = string.Empty;
    public bool HadKey { get; set; }
    public bool HadDefaultValue { get; set; }
    public string? OriginalValue { get; set; }
    public RegistryValueKind OriginalValueKind { get; set; } = RegistryValueKind.String;
    public string AppliedValue { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }

    public void AddExtension(string extension)
    {
        if (!Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            Extensions.Add(extension);
    }
}

internal sealed class BackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private List<BackupEntry> _entries = [];
    private readonly bool _previewOnly;
    public bool Machine { get; }
    public string? LoadError { get; private set; }

    public string StorageDirectory { get; }

    private string StoragePath => Path.Combine(StorageDirectory, "backups.json");
    public IReadOnlyList<BackupEntry> Entries => _entries;

    public BackupStore(bool previewOnly = false, bool machine = false, string? testDirectory = null)
    {
        _previewOnly = previewOnly;
        Machine = machine;
        StorageDirectory = testDirectory ?? Path.Combine(Environment.GetFolderPath(machine
            ? Environment.SpecialFolder.CommonApplicationData : Environment.SpecialFolder.LocalApplicationData), AppIdentity.DataDirectoryName);
        Reload();
    }

    public void Reload()
    {
        if (_previewOnly) return;
        LoadError = null;
        try
        {
            _entries = File.Exists(StoragePath)
                ? JsonSerializer.Deserialize<List<BackupEntry>>(File.ReadAllText(StoragePath), JsonOptions)
                    ?? throw new InvalidDataException(AppText.T("백업 내용이 비어 있습니다."))
                : [];
            foreach (var entry in _entries)
            {
                if (entry.Machine != Machine || entry.UserRegistryPath is null || entry.ValueName is null ||
                    entry.GroupId is null || entry.Extensions is null) throw new InvalidDataException(AppText.T("백업 형식이 올바르지 않습니다."));
                RegistryWriter.Validate(entry.Machine, entry.UserRegistryPath, entry.ValueName);
            }
            if (_entries.Select(e => e.Identity).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _entries.Count)
                throw new InvalidDataException(AppText.T("백업에 중복된 레지스트리 항목이 있습니다."));
        }
        catch (Exception ex)
        {
            _entries = [];
            LoadError = AppText.F("백업을 읽지 못했습니다. 원본을 보존하기 위해 변경을 중단합니다.\n{0}\n{1}", StoragePath, ex.Message);
        }
    }

    public void EnsureWritable()
    {
        if (_previewOnly) throw new InvalidOperationException(AppText.T("UI 검수 모드에서는 설정을 변경할 수 없습니다."));
        if (LoadError is not null) throw new InvalidDataException(LoadError);
    }

    public FileStream AcquireLock()
    {
        EnsureWritable();
        Directory.CreateDirectory(StorageDirectory);
        return new FileStream(Path.Combine(StorageDirectory, "changes.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public BackupEntry GetOrCreate(AssociationTarget target, string appliedValue)
    {
        var existing = _entries.FirstOrDefault(item =>
            item.Machine == target.Machine && item.ValueName.Equals(target.ValueName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.UserRegistryPath, target.UserRegistryPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.AddExtension(target.Extension);
            if (target.GroupId.Length > 0) existing.GroupId = target.GroupId;
            return existing;
        }

        var entry = Capture(target, appliedValue);
        _entries.Add(entry);
        return entry;
    }

    public static BackupEntry Capture(AssociationTarget target, string appliedValue)
    {
        RegistryWriter.Validate(target.Machine, target.UserRegistryPath, target.ValueName);
        using var root = RegistryWriter.OpenRoot(target.Machine);
        using var key = root.OpenSubKey(target.UserRegistryPath, writable: false);
        var valueNames = key?.GetValueNames() ?? [];
        var hadDefault = valueNames.Contains(target.ValueName, StringComparer.OrdinalIgnoreCase);
        var originalValue = hadDefault
            ? key!.GetValue(target.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string
            : null;
        var valueKind = hadDefault ? key!.GetValueKind(target.ValueName) : RegistryValueKind.String;
        if (valueKind is not (RegistryValueKind.String or RegistryValueKind.ExpandString))
            throw new InvalidDataException(AppText.T("아이콘 설정에 문자열이 아닌 값이 있습니다. 안전을 위해 변경을 중단합니다."));

        var entry = new BackupEntry
        {
            Machine = target.Machine,
            ValueName = target.ValueName,
            GroupId = target.GroupId,
            Extensions = [target.Extension],
            ProgId = target.ProgId,
            UserRegistryPath = target.UserRegistryPath,
            HadKey = key is not null,
            HadDefaultValue = hadDefault,
            OriginalValue = originalValue,
            OriginalValueKind = valueKind,
            AppliedValue = appliedValue,
            AppliedAtUtc = DateTime.UtcNow
        };
        return entry;
    }

    public void Remove(BackupEntry entry) =>
        _entries.RemoveAll(item => string.Equals(item.Identity, entry.Identity, StringComparison.OrdinalIgnoreCase));

    public void Save()
    {
        EnsureWritable();
        Directory.CreateDirectory(StorageDirectory);
        var tempPath = StoragePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(_entries, JsonOptions));
        File.Move(tempPath, StoragePath, overwrite: true);
    }
}

internal static class NativeShell
{
    private const uint ShcneAssocChanged = 0x08000000;
    private const uint ShcneUpdateImage = 0x00008000;
    private const uint ShcneUpdateDirectory = 0x00001000;
    private const uint ShcneUpdateItem = 0x00002000;
    private const uint ShcnfIdList = 0x0000;
    private const uint ShcnfDword = 0x0003;
    private const uint ShcnfPathW = 0x0005;
    private const uint ShcnfFlush = 0x1000;
    private const uint ShgsiIcon = 0x00000100;
    private const uint ShgsiSystemIconIndex = 0x00004000;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("shell32.dll")]
    private static extern int SHGetStockIconInfo(uint stockIconId, uint flags, ref StockIconInfo info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string szFileName, int nIconIndex, IntPtr[]? phiconLarge,
        IntPtr[]? phiconSmall, uint nIcons);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "PrivateExtractIconsW")]
    private static extern uint PrivateExtractIcons(string file, int index, int width, int height,
        IntPtr[] icons, uint[]? ids, uint count, uint flags);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    public static void NotifyAssociationChanged() =>
        // Wait until all registered Shell clients have received the cache-invalidating
        // notification. Returning early can leave an already-running Explorer on its old image list.
        SHChangeNotify(ShcneAssocChanged, ShcnfIdList | ShcnfFlush, IntPtr.Zero, IntPtr.Zero);

    public static void NotifyStockIconsChanged(IEnumerable<int> stockIconIds)
    {
        foreach (var stockIconId in stockIconIds.Distinct())
        {
            var info = new StockIconInfo { Size = (uint)Marshal.SizeOf<StockIconInfo>() };
            if (SHGetStockIconInfo((uint)stockIconId, ShgsiSystemIconIndex, ref info) == 0 && info.SystemImageIndex >= 0)
                SHChangeNotify(ShcneUpdateImage, ShcnfDword | ShcnfFlush, IntPtr.Zero, new IntPtr(info.SystemImageIndex));
        }
    }

    public static void NotifyDesktopItemsChanged()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDesktopPath(paths, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddDesktopPath(paths, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
        if (paths.Count == 0)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
            AddDesktopPath(paths, Environment.ExpandEnvironmentVariables(key?.GetValue("Desktop", "") as string ?? ""));
        }

        foreach (var directory in paths)
        {
            NotifyPath(ShcneUpdateDirectory, directory);
            try
            {
                foreach (var item in Directory.EnumerateFileSystemEntries(directory)) NotifyPath(ShcneUpdateItem, item);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
    }

    private static void AddDesktopPath(HashSet<string> paths, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) paths.Add(Path.GetFullPath(path));
    }

    private static void NotifyPath(uint eventId, string path)
    {
        var pointer = Marshal.StringToHGlobalUni(path);
        try { SHChangeNotify(eventId, ShcnfPathW | ShcnfFlush, pointer, IntPtr.Zero); }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    public static int RestartExplorer(bool rebuildCaches = false)
    {
        using (var winlogon = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"))
        {
            var setting = winlogon?.GetValue("AutoRestartShell", 1);
            if (setting is int enabled && enabled == 0)
                throw new InvalidOperationException(AppText.T("Windows의 탐색기 자동 재시작 설정이 꺼져 있어 안전하게 진행할 수 없습니다. 로그아웃 후 다시 로그인해 주세요."));
        }

        var shellWindow = GetShellWindow();
        if (shellWindow == IntPtr.Zero || GetWindowThreadProcessId(shellWindow, out var processId) == 0 || processId == 0)
            throw new InvalidOperationException(AppText.T("현재 Windows 탐색기 셸 프로세스를 찾지 못했습니다. 로그아웃 후 다시 로그인해 주세요."));

        using var shell = Process.GetProcessById((int)processId);
        if (!shell.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(AppText.T("확인된 셸이 Windows 탐색기가 아니므로 안전을 위해 중단했습니다."));

        shell.Kill(entireProcessTree: false);
        if (!shell.WaitForExit(10_000))
            throw new InvalidOperationException(AppText.T("기존 탐색기가 제한 시간 안에 종료되지 않았습니다. 로그아웃 후 다시 로그인해 주세요."));

        int cleared = rebuildCaches ? ClearExplorerCaches() : 0;
        var deadline = Environment.TickCount64 + 15_000;
        while (Environment.TickCount64 < deadline)
        {
            Thread.Sleep(250);
            if (GetShellWindow() != IntPtr.Zero) return cleared;
        }
        throw new InvalidOperationException(AppText.T("Windows가 탐색기를 자동으로 다시 열지 못했습니다. Ctrl+Shift+Esc로 작업 관리자를 열고 '새 작업 실행'에서 explorer.exe를 실행하세요."));
    }

    private static int ClearExplorerCaches()
    {
        var local = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        if (string.IsNullOrWhiteSpace(local) || !Directory.Exists(local)) return 0;
        var explorerCache = Path.GetFullPath(Path.Combine(local, "Microsoft", "Windows", "Explorer"));
        var localPrefix = local.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!explorerCache.StartsWith(localPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(AppText.T("아이콘 캐시 경로가 사용자 LocalAppData 밖으로 확인되어 캐시 갱신을 중단했습니다."));

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var legacy = Path.Combine(local, "IconCache.db");
        if (File.Exists(legacy)) files.Add(legacy);
        if (Directory.Exists(explorerCache))
        {
            foreach (var pattern in new[] { "iconcache_*.db", "thumbcache_*.db" })
                foreach (var file in Directory.EnumerateFiles(explorerCache, pattern, SearchOption.TopDirectoryOnly))
                    files.Add(Path.GetFullPath(file));
        }

        int cleared = 0;
        foreach (var file in files)
        {
            var fullPath = Path.GetFullPath(file);
            bool allowed = fullPath.Equals(legacy, StringComparison.OrdinalIgnoreCase) ||
                           fullPath.StartsWith(explorerCache.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                               StringComparison.OrdinalIgnoreCase);
            if (!allowed) continue;
            try { File.Delete(fullPath); cleared++; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return cleared;
    }

    public static Icon? ExtractIcon(string path, int index)
    {
        if (string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase))
            return new Icon(path, new Size(256, 256));

        var large = new IntPtr[1];
        if (PrivateExtractIcons(path, index, 256, 256, large, null, 1, 0) != 1 || large[0] == IntPtr.Zero)
            return null;

        try
        {
            using var source = Icon.FromHandle(large[0]);
            return (Icon)source.Clone();
        }
        finally
        {
            DestroyIcon(large[0]);
        }
    }

    public static Bitmap? ExtractStockIcon(int stockIconId, int size)
    {
        var info = new StockIconInfo { Size = (uint)Marshal.SizeOf<StockIconInfo>() };
        if (SHGetStockIconInfo((uint)stockIconId, ShgsiIcon, ref info) != 0 || info.Icon == IntPtr.Zero)
            return null;
        try
        {
            using var source = Icon.FromHandle(info.Icon);
            using var clone = (Icon)source.Clone();
            return RenderIcon(clone, size);
        }
        finally { DestroyIcon(info.Icon); }
    }

    public static Bitmap? ExtractIconLocation(string? iconValue, int size)
    {
        if (string.IsNullOrWhiteSpace(iconValue)) return null;
        var value = iconValue.Trim();
        int index = 0;
        int comma = value.LastIndexOf(',');
        if (comma > 0 && int.TryParse(value[(comma + 1)..].Trim(), out var parsedIndex))
        {
            index = parsedIndex;
            value = value[..comma];
        }
        var path = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        if (!File.Exists(path)) return null;
        using var icon = ExtractIcon(path, index);
        return icon is null ? null : RenderIcon(icon, size);
    }

    public static Bitmap RenderIcon(Icon icon, int size)
    {
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawIcon(icon, new Rectangle(0, 0, size, size));
        return bitmap;
    }

    public static Bitmap? ExtractShellImage(string path, int size)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        var result = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        if (result != 0) return null;
        try
        {
            result = factory.GetImage(new NativeSize(size, size), ShellImageFlags.BiggerSizeOk, out var bitmapHandle);
            if (result != 0 || bitmapHandle == IntPtr.Zero) return null;
            try
            {
                using var source = Image.FromHbitmap(bitmapHandle);
                return new Bitmap(source);
            }
            finally { DeleteObject(bitmapHandle); }
        }
        finally { Marshal.FinalReleaseComObject(factory); }
    }

    public static Bitmap? ExtractShellThumbnail(string path, int size)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        var result = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory);
        if (result != 0)
            return null;

        try
        {
            result = factory.GetImage(new NativeSize(size, size),
                ShellImageFlags.ThumbnailOnly | ShellImageFlags.BiggerSizeOk, out var bitmapHandle);
            if (result != 0 || bitmapHandle == IntPtr.Zero)
                return null;
            try
            {
                using var source = Image.FromHbitmap(bitmapHandle);
                return new Bitmap(source);
            }
            finally
            {
                DeleteObject(bitmapHandle);
            }
        }
        finally
        {
            Marshal.FinalReleaseComObject(factory);
        }
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct StockIconInfo
{
    public uint Size;
    public IntPtr Icon;
    public int SystemImageIndex;
    public int ResourceIndex;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string Path;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativeSize(int width, int height)
{
    public readonly int Width = width;
    public readonly int Height = height;
}

[Flags]
internal enum ShellImageFlags
{
    BiggerSizeOk = 0x1,
    ThumbnailOnly = 0x8
}

[ComImport]
[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
    [PreserveSig]
    int GetImage(NativeSize size, ShellImageFlags flags, out IntPtr bitmapHandle);
}

internal static class IconSource
{
    private static readonly string[] NativeIconExtensions = [".ico", ".exe", ".dll"];
    private static readonly int[] IconSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    public static string PrepareStoredIcon(string sourcePath, int index, string storageDirectory,
        int cornerRadiusPercent = 0, bool shortcutOverlay = false, int overlayScalePercent = 100)
    {
        // All targets use a stable snapshot, including ICO/EXE/DLL. Machine targets
        // live under ProgramData so other users do not depend on this user's profile.
        using var preview = CreatePreview(sourcePath, index, cornerRadiusPercent, out var rounded,
            shortcutOverlay, overlayScalePercent)
            ?? throw new InvalidOperationException(AppText.T("선택한 파일/아이콘 번호에서 이미지를 읽을 수 없습니다."));
        var directory = Path.Combine(storageDirectory, "generated-icons");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            if (Path.GetExtension(sourcePath).Equals(".ico", StringComparison.OrdinalIgnoreCase) && !rounded &&
                (!shortcutOverlay || Math.Clamp(overlayScalePercent, 10, 100) == 100))
                File.Copy(sourcePath, temporary);
            else SaveMultiSizeIcon(preview, temporary);
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temporary))).ToLowerInvariant();
            var destination = Path.Combine(directory, hash + ".ico");
            try { File.Move(temporary, destination); }
            catch (IOException) when (File.Exists(destination)) { }
            return $"\"{destination}\",0";
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string PrepareRegistryValue(string sourcePath, int index, string storageDirectory)
    {
        var extension = Path.GetExtension(sourcePath);
        if (NativeIconExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            using var testIcon = NativeShell.ExtractIcon(sourcePath, index)
                                 ?? throw new InvalidOperationException(AppText.T("선택한 파일에서 아이콘을 읽을 수 없습니다."));
            return $"\"{sourcePath}\",{index}";
        }

        using var image = LoadImage(sourcePath)
                          ?? throw new InvalidOperationException(
                              AppText.T("선택한 파일을 이미지로 읽을 수 없습니다. PNG, JPG, JPEG, BMP, GIF, TIFF 또는 Windows 탐색기에서 미리 보기가 가능한 이미지 파일을 선택하세요."));

        var generatedDirectory = Path.Combine(storageDirectory, "generated-icons");
        Directory.CreateDirectory(generatedDirectory);
        var identity = $"{sourcePath}|{File.GetLastWriteTimeUtc(sourcePath).Ticks}|{new FileInfo(sourcePath).Length}";
        var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant() + ".ico";
        var generatedPath = Path.Combine(generatedDirectory, fileName);
        if (!File.Exists(generatedPath))
            SaveMultiSizeIcon(image, generatedPath);

        return $"\"{generatedPath}\",0";
    }

    public static Image? CreatePreview(string sourcePath, int index, int cornerRadiusPercent, out bool rounded,
        bool shortcutOverlay = false, int overlayScalePercent = 100)
    {
        rounded = false;
        if (NativeIconExtensions.Contains(Path.GetExtension(sourcePath), StringComparer.OrdinalIgnoreCase))
        {
            using var icon = NativeShell.ExtractIcon(sourcePath, index);
            using var bitmap = icon?.ToBitmap();
            if (bitmap is null) return null;
            using var nativePrepared = ApplyRoundedCorners(bitmap, cornerRadiusPercent, out rounded);
            return shortcutOverlay ? CreateOverlayCanvas(nativePrepared, 256, overlayScalePercent) : ResizeToSquare(nativePrepared, 256);
        }

        using var image = LoadImage(sourcePath);
        if (image is null) return null;
        using var imagePrepared = ApplyRoundedCorners(image, cornerRadiusPercent, out rounded);
        return shortcutOverlay ? CreateOverlayCanvas(imagePrepared, 256, overlayScalePercent) : ResizeToSquare(imagePrepared, 256);
    }

    internal static Bitmap CreateOverlayCanvas(Image source, int size, int scalePercent)
    {
        int percent = Math.Clamp(scalePercent, 10, 100);
        int box = Math.Max(1, (int)Math.Round(size * percent / 100f));
        float scale = Math.Min((float)box / source.Width, (float)box / source.Height);
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var result = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        result.SetResolution(96, 96);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, size - height, width, height));
        return result;
    }

    internal static Bitmap ApplyRoundedCorners(Image source, int cornerRadiusPercent, out bool rounded)
    {
        rounded = false;
        var normalized = new Bitmap(source);
        int percent = Math.Clamp(cornerRadiusPercent, 0, 50);
        if (percent == 0 || normalized.Width < 2 || normalized.Height < 2 || !HasOpaqueCorners(normalized))
            return normalized;

        float radius = Math.Min(normalized.Width, normalized.Height) * percent / 100f;
        if (radius < 0.5f) return normalized;

        var result = new Bitmap(normalized.Width, normalized.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        result.SetResolution(normalized.HorizontalResolution > 0 ? normalized.HorizontalResolution : 96,
            normalized.VerticalResolution > 0 ? normalized.VerticalResolution : 96);
        using (var graphics = Graphics.FromImage(result))
        using (var path = RoundedRectanglePath(normalized.Width, normalized.Height, radius))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.SetClip(path);
            graphics.DrawImage(normalized, new Rectangle(0, 0, normalized.Width, normalized.Height));
        }
        normalized.Dispose();
        rounded = true;
        return result;
    }

    private static bool HasOpaqueCorners(Bitmap image)
    {
        int right = image.Width - 1, bottom = image.Height - 1;
        return image.GetPixel(0, 0).A >= 250 && image.GetPixel(right, 0).A >= 250 &&
               image.GetPixel(0, bottom).A >= 250 && image.GetPixel(right, bottom).A >= 250;
    }

    private static GraphicsPath RoundedRectanglePath(int width, int height, float radius)
    {
        float diameter = Math.Min(radius * 2, Math.Min(width, height));
        float right = width - diameter, bottom = height - diameter;
        var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(right, 0, diameter, diameter, 270, 90);
        path.AddArc(right, bottom, diameter, diameter, 0, 90);
        path.AddArc(0, bottom, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Bitmap? LoadImage(string sourcePath)
    {
        try
        {
            using var source = Image.FromFile(sourcePath, useEmbeddedColorManagement: true);
            return new Bitmap(source);
        }
        catch
        {
            return NativeShell.ExtractShellThumbnail(sourcePath, 512);
        }
    }

    private static void SaveMultiSizeIcon(Image source, string destinationPath)
    {
        var payloads = new List<byte[]>();
        foreach (var size in IconSizes)
        {
            using var bitmap = ResizeToSquare(source, size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            payloads.Add(stream.ToArray());
        }

        using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: false);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)payloads.Count);

        var offset = 6 + (16 * payloads.Count);
        for (var i = 0; i < payloads.Count; i++)
        {
            var size = IconSizes[i];
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)payloads[i].Length);
            writer.Write((uint)offset);
            offset += payloads[i].Length;
        }

        foreach (var payload in payloads)
            writer.Write(payload);
    }

    private static Bitmap ResizeToSquare(Image source, int size)
    {
        var result = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        result.SetResolution(96, 96);
        using var graphics = Graphics.FromImage(result);
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        var scale = Math.Min((float)size / source.Width, (float)size / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var x = (size - width) / 2;
        var y = (size - height) / 2;
        graphics.DrawImage(source, new Rectangle(x, y, width, height));
        return result;
    }
}
