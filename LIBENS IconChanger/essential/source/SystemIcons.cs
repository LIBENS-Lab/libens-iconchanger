using System.Security.Principal;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LibensIconChanger;

internal sealed record SystemIconTarget(string Id, string Name, string Description, AssociationTarget[] Locations)
{
    public bool RequiresAdmin => Locations.Any(t => t.Machine);
    public string LocalizedName => AppText.T(Name);
    public string LocalizedDescription => AppText.CatalogText(Description);
}

internal static class SystemIconCatalog
{
    public const string ShellIconsPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
    private const string DesktopPrefix = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\";
    public static readonly string[] MachineValueNames = ["0", "1", "2", "3", "4", "7", "8", "9", "10", "11", "16", "29"];
    public static readonly string[] DesktopPaths = [
        DesktopPrefix + @"{20D04FE0-3AEA-1069-A2D8-08002B30309D}\DefaultIcon",
        DesktopPrefix + @"{645FF040-5081-101B-9F08-00AA002F954E}\DefaultIcon",
        DesktopPrefix + @"{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}\DefaultIcon",
        DesktopPrefix + @"{59031a47-3f72-44a7-89c5-5595fe6b30ee}\DefaultIcon"];

    public static readonly SystemIconTarget[] Items = [
        Stock("folders", "기본 폴더 · 닫힘+열림 모두", "일반 폴더의 닫힘/열림 기본 아이콘을 같은 이미지로 함께 바꿉니다. 개별 지정 아이콘, 특수 폴더, 내용 미리보기(썸네일)는 별도입니다.", "3", "4"),
        StockGrouped("folder-closed", "기본 폴더 · 닫힘만", "일반 폴더의 닫힌 상태 기본 아이콘만 바꿉니다. 내용 미리보기는 고정 아이콘이 아니라 Explorer가 만드는 썸네일이므로 이 대상에 포함되지 않습니다.", "system:folders", "3"),
        StockGrouped("folder-open", "기본 폴더 · 열림만", "일반 폴더의 열린 상태 기본 아이콘만 바꿉니다. Windows 버전과 보기 방식에 따라 열린 상태가 따로 보이지 않을 수 있습니다.", "system:folders", "4"),
        Stock("shortcut-overlay", "바로가기 화살표 오버레이", "모든 바로가기에 공통으로 겹쳐 표시되는 화살표 오버레이를 바꿉니다. 선택 이미지를 지정 비율로 축소해 좌측 하단에 놓고 나머지 캔버스는 투명하게 만듭니다. 바로가기 대상·기능과 개별 아이콘은 변경하지 않습니다.", "29"),
        Desktop("computer", "내 PC", 0, ""),
        Desktop("recycle-all", "휴지통 · 두 상태 모두", 1, "*"),
        Desktop("recycle-empty", "휴지통 · 비어 있음", 1, "empty"),
        Desktop("recycle-full", "휴지통 · 가득 참", 1, "full"),
        Desktop("network", "네트워크", 2, ""),
        Desktop("user-files", "사용자 파일", 3, ""),
        Stock("unknown-file", "연결 프로그램 없는 파일", "알 수 없는 파일 형식에 표시되는 기본 아이콘입니다. 파일 형식이나 프로그램이 지정한 아이콘은 유지됩니다.", "0"),
        Stock("generic-document", "일반 문서", "별도 아이콘이 없는 연결된 문서의 기본 아이콘입니다. 모든 문서 확장자를 덮어쓰지는 않습니다.", "1"),
        Stock("drive-fixed", "로컬 드라이브", "기본 드라이브 아이콘입니다. 제조사·사용자가 지정한 아이콘과 특수 오버레이는 별도입니다.", "8"),
        Stock("drive-removable", "이동식 드라이브", "Windows가 이동식으로 표시하는 드라이브의 기본 아이콘입니다.", "7"),
        StockGrouped("drive-network-all", "네트워크 드라이브 · 두 상태 모두", "연결됨/연결 끊김 상태의 기본 아이콘을 같은 이미지로 함께 바꿉니다.", "system:drive-network", "9", "10"),
        Stock("drive-network", "네트워크 드라이브 · 연결됨만", "연결된 네트워크 드라이브의 기본 아이콘입니다. 연결 끊김 상태는 유지합니다.", "9"),
        StockGrouped("drive-network-disconnected", "네트워크 드라이브 · 연결 끊김만", "연결이 끊긴 네트워크 드라이브의 기본 아이콘만 바꿉니다.", "system:drive-network", "10"),
        Stock("drive-optical", "CD 드라이브", "기본 CD 드라이브 아이콘입니다. DVD·BD·삽입한 미디어의 별도 아이콘은 변경하지 않습니다.", "11"),
        Stock("printer", "일반 프린터", "Windows 셸의 일반 프린터 기본 아이콘입니다. 설정 앱과 제조사 전용 아이콘은 별도입니다.", "16")
    ];

    public static Bitmap CreatePreview(SystemIconTarget target, int size)
    {
        try
        {
            var location = target.Locations[0];
            if (location.Machine && int.TryParse(location.ValueName, out var stockId))
                return NativeShell.ExtractStockIcon(stockId, size) ?? FallbackPreview(size);

            int open = location.UserRegistryPath.IndexOf('{');
            int close = location.UserRegistryPath.IndexOf('}', open + 1);
            if (open >= 0 && close > open)
            {
                var clsid = location.UserRegistryPath[open..(close + 1)];
                using var key = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}\DefaultIcon");
                var valueName = location.ValueName == "*" ? "empty" : location.ValueName;
                var iconValue = key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                var fromLocation = NativeShell.ExtractIconLocation(iconValue, size);
                if (fromLocation is not null) return fromLocation;
                var fromNamespace = NativeShell.ExtractShellImage("::" + clsid, size);
                if (fromNamespace is not null) return fromNamespace;
            }
        }
        catch { }
        return FallbackPreview(size);
    }

    private static Bitmap FallbackPreview(int size)
    {
        using var icon = (Icon)SystemIcons.Application.Clone();
        return NativeShell.RenderIcon(icon, size);
    }

    private static SystemIconTarget Stock(string id, string name, string description, params string[] values) => new(
        id, name, description + "\n전체 사용자에게 적용되며 관리자 권한이 필요합니다. Windows 버전·보기 방식·아이콘 캐시에 따라 적용 범위가 다를 수 있습니다.",
        values.Select(v => new AssociationTarget(name, "Windows 기본 · 전체 사용자", ShellIconsPath, null, true, v, "system:" + id)).ToArray());

    private static SystemIconTarget StockGrouped(string id, string name, string description, string groupId, params string[] values) => new(
        id, name, description + "\n전체 사용자에게 적용되며 관리자 권한이 필요합니다. 같은 계열의 상태는 변경 기록에서 함께 복원될 수 있습니다.",
        values.Select(v => new AssociationTarget(name, "Windows 기본 · 전체 사용자", ShellIconsPath, null, true, v, groupId)).ToArray());

    private static SystemIconTarget Desktop(string id, string name, int path, string value) => new(id, name,
        "현재 사용자의 바탕 화면 시스템 아이콘 설정입니다. 탐색기의 일부 위치에는 별도 아이콘이 표시될 수 있습니다. 숨겨진 바탕 화면 아이콘을 표시하도록 바꾸지는 않습니다." +
        (path == 1 ? "\n선택한 휴지통 상태만 바꾸고 다른 상태의 아이콘은 유지합니다. 복원은 두 상태와 현재 표시값을 함께 되돌립니다." : ""),
        [new(name, "바탕 화면 · 현재 사용자", DesktopPaths[path], null, false, value, "system:" + id)]);

    [StructLayout(LayoutKind.Sequential)]
    private struct RecycleInfo { public uint Size; public long Bytes; public long Items; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? root, ref RecycleInfo info);

    public static IconWrite[] RecycleWrites(AssociationTarget selected, string icon, bool? emptyForTest = null)
    {
        bool empty;
        if (emptyForTest.HasValue) empty = emptyForTest.Value;
        else
        {
            var info = new RecycleInfo { Size = (uint)Marshal.SizeOf<RecycleInfo>() };
            if (SHQueryRecycleBin(null, ref info) < 0)
                throw new InvalidOperationException(AppText.T("휴지통 상태를 확인하지 못했습니다. 내용을 변경하지 않았습니다. 잠시 후 다시 시도해 주세요."));
            empty = info.Items == 0;
        }
        using var user = RegistryWriter.OpenRoot(false);
        using var current = user.OpenSubKey(DesktopPaths[1]);
        using var original = Registry.ClassesRoot.OpenSubKey(@"CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\DefaultIcon");
        string StateIcon(string state) => selected.ValueName is "*" || selected.ValueName == state ? icon
            : current?.GetValue(state) as string ?? original?.GetValue(state) as string
                ?? throw new InvalidOperationException(AppText.T("반대 상태의 휴지통 아이콘 설정을 읽지 못했습니다. 기존 아이콘을 보호하기 위해 중단합니다."));
        string emptyIcon = StateIcon("empty"), fullIcon = StateIcon("full");
        var target = selected with { GroupId = "system:recycle", Extension = "휴지통 · 두 상태" };
        return [new(target with { ValueName = "empty" }, emptyIcon), new(target with { ValueName = "full" }, fullIcon),
            new(target with { ValueName = "" }, empty ? emptyIcon : fullIcon)];
    }

    public static bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static AssociationTarget[] ExtensionTargets(IReadOnlyList<string> extensions, bool preserveExe)
    {
        var targets = new List<AssociationTarget>();
        foreach (var extension in extensions)
        {
            var target = AssociationResolver.Resolve(extension);
            if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)) { targets.Add(target); continue; }
            if (!string.Equals(target.ProgId, "exefile", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(AppText.T("EXE 연결 형식이 표준 exefile이 아닙니다. 실행 연결을 보호하기 위해 EXE 변경을 중단합니다."));
            using var handler = Registry.ClassesRoot.OpenSubKey(@"exefile\shellex\IconHandler");
            if (handler?.GetValue("") is string s && !string.IsNullOrWhiteSpace(s))
                throw new InvalidOperationException(AppText.T("EXE에 다른 아이콘 처리기가 등록되어 있습니다. 기존 처리를 보호하기 위해 EXE 변경을 중단합니다."));
            targets.Add(target with { GroupId = "extension:exe" });
            if (preserveExe)
                targets.Add(new(".exe", "자체 아이콘 없는 EXE · 전체 사용자", ShellIconsPath, null, true, "2", "extension:exe"));
        }
        return targets.ToArray();
    }
}

internal sealed record IconWrite(AssociationTarget Target, string Value);

internal sealed class IconChangeService(BackupStore user, BackupStore machine, Func<bool>? administratorCheck = null)
{
    public BackupStore Store(bool system) => system ? machine : user;
    public IEnumerable<BackupEntry> Entries => user.Entries.Concat(machine.Entries);

    public void Reload() { user.Reload(); machine.Reload(); }

    public void Apply(IReadOnlyList<IconWrite> changes)
    {
        WithLocks(changes.Any(c => c.Target.Machine), () =>
        {
            var before = changes.Select(c => BackupStore.Capture(c.Target, c.Value)).ToArray();
            foreach (var change in changes)
            {
                var backup = Store(change.Target.Machine).GetOrCreate(change.Target, change.Value);
                backup.AppliedValue = change.Value;
                backup.AppliedAtUtc = DateTime.UtcNow;
            }
            // Persist every original BEFORE changing either hive. Existing originals never rotate.
            Save(changes.Any(c => c.Target.Machine));
            try { foreach (var change in changes) RegistryWriter.SetIcon(change.Target, change.Value); }
            catch (Exception ex) { RollBack(before, ex); }
        });
    }

    public void Restore(IEnumerable<BackupEntry> selected)
    {
        var identities = selected.Select(e => e.Identity).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool includesMachine = selected.Any(e => e.Machine);
        WithLocks(includesMachine, () =>
        {
            // Reloaded entries, not stale UI snapshots, are authoritative.
            var entries = Entries.Where(e => identities.Contains(e.Identity)).ToArray();
            if (entries.Length != identities.Count) throw new InvalidOperationException(AppText.T("다른 창에서 백업이 변경되었습니다. 목록을 새로 확인해 주세요."));
            var before = entries.Select(e => BackupStore.Capture(new(e.Extensions.FirstOrDefault() ?? "", e.ProgId,
                e.UserRegistryPath, null, e.Machine, e.ValueName, e.GroupId), e.AppliedValue)).ToArray();
            try { foreach (var entry in entries.Reverse()) RegistryWriter.Restore(entry); }
            catch (Exception ex) { RollBack(before, ex); }
            foreach (var entry in entries) Store(entry.Machine).Remove(entry);
            Save(includesMachine);
        });
    }

    private void WithLocks(bool includesMachine, Action action)
    {
        if (includesMachine && !(administratorCheck?.Invoke() ?? SystemIconCatalog.IsAdministrator))
            throw new UnauthorizedAccessException(AppText.T("전체 사용자 설정을 바꾸거나 복원하려면 '관리자 모드 열기'로 실행한 뒤 다시 시도해 주세요."));
        using var machineLock = includesMachine ? machine.AcquireLock() : null;
        using var userLock = user.AcquireLock();
        user.Reload(); user.EnsureWritable();
        if (includesMachine) { machine.Reload(); machine.EnsureWritable(); }
        action();
    }

    private void Save(bool includesMachine) { user.Save(); if (includesMachine) machine.Save(); }

    private static void RollBack(IEnumerable<BackupEntry> before, Exception cause)
    {
        var failures = new List<string>();
        foreach (var entry in before.Reverse())
        {
            try { RegistryWriter.Restore(entry); }
            catch (Exception ex) { failures.Add(ex.Message); }
        }
        throw new InvalidOperationException(cause.Message + (failures.Count == 0
            ? AppText.T("\n이번 작업의 설정 변경은 되돌렸습니다. 최초 원본 백업은 보존됩니다.")
            : AppText.T("\n일부 되돌리기에 실패했습니다. 백업 목록에서 다시 복원해 주세요.\n") + string.Join("\n", failures)), cause);
    }
}
