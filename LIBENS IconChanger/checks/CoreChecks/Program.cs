using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace LibensIconChanger;

internal static class CoreChecks
{
    [DllImport("advapi32.dll")]
    private static extern int RegOverridePredefKey(IntPtr key, IntPtr replacement);
    private static readonly IntPtr Hkcu = new(unchecked((int)0x80000001));
    private static readonly IntPtr Hklm = new(unchecked((int)0x80000002));
    private static readonly List<string> Passed = [];
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception(name); Passed.Add(name); }

    [STAThread]
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var registryPath = @"Software\FileIconManager-CoreChecks-" + Guid.NewGuid().ToString("N");
        using var realUser = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var isolatedUser = realUser.CreateSubKey(registryPath + @"\User");
        using var isolatedMachine = realUser.CreateSubKey(registryPath + @"\Machine");
        try
        {
            if (RegOverridePredefKey(Hkcu, isolatedUser.Handle.DangerousGetHandle()) != 0 ||
                RegOverridePredefKey(Hklm, isolatedMachine.Handle.DangerousGetHandle()) != 0) throw new Exception("Registry isolation failed");
            Run(output, args[1]);
            File.WriteAllText(Path.Combine(output, "core-results.json"), JsonSerializer.Serialize(Passed, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS: {Passed.Count} checks; isolated registry only");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            RegOverridePredefKey(Hkcu, IntPtr.Zero); RegOverridePredefKey(Hklm, IntPtr.Zero);
            // Only this run's literal, GUID-scoped test key is removed.
            isolatedUser.Close(); isolatedMachine.Close(); realUser.DeleteSubKeyTree(registryPath);
        }
    }

    private static void Run(string output, string sourceIcon)
    {
        var user = new BackupStore(testDirectory: Path.Combine(output, "user"));
        var machine = new BackupStore(machine: true, testDirectory: Path.Combine(output, "machine"));
        var service = new IconChangeService(user, machine, () => true);
        var ext = new AssociationTarget(".fixture", "fixture", @"Software\Classes\fixture\DefaultIcon", null);
        using (var root = RegistryWriter.OpenRoot(false))
        using (var key = root.CreateSubKey(ext.UserRegistryPath))
        { key.SetValue("", @"%SystemRoot%\original.ico,0", RegistryValueKind.ExpandString); key.SetValue("Keep", "untouched"); }
        service.Apply([new(ext, "first.ico,0")]);
        service.Apply([new(ext, "second.ico,0")]);
        Check(user.Entries.Count == 1 && user.Entries[0].OriginalValue == @"%SystemRoot%\original.ico,0", "Repeated apply preserves first original, no growing duplicate backups");
        user.Reload(); service.Restore(user.Entries.ToArray());
        using (var root = RegistryWriter.OpenRoot(false))
        using (var key = root.OpenSubKey(ext.UserRegistryPath))
        {
            Check((string?)key!.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames) == @"%SystemRoot%\original.ico,0" && key.GetValueKind("") == RegistryValueKind.ExpandString, "Restores original value and REG_EXPAND_SZ kind");
            Check((string?)key.GetValue("Keep") == "untouched", "Restore preserves unrelated registry values");
        }
        var folders = SystemIconCatalog.Items.Single(t => t.Id == "folders").Locations;
        using (var root = RegistryWriter.OpenRoot(true))
        using (var key = root.CreateSubKey(SystemIconCatalog.ShellIconsPath))
        { key.SetValue("4", "old-open.ico,0"); key.SetValue("Keep", "keep-machine"); }
        service.Apply(folders.Select(t => new IconWrite(t, "folder.ico,0")).ToArray());
        Check(machine.Entries.Count == 2 && machine.Entries.Select(e => e.GroupKey).Distinct().Count() == 1, "Folder open/closed named values form one restore group");
        service.Restore(machine.Entries.ToArray());
        using (var root = RegistryWriter.OpenRoot(true))
        using (var key = root.OpenSubKey(SystemIconCatalog.ShellIconsPath))
            Check(key!.GetValue("3") is null && (string?)key.GetValue("4") == "old-open.ico,0" && (string?)key.GetValue("Keep") == "keep-machine", "Restores absent/existing machine values without deleting neighbors");
        var closedFolder = SystemIconCatalog.Items.Single(t => t.Id == "folder-closed").Locations.Single();
        var openFolder = SystemIconCatalog.Items.Single(t => t.Id == "folder-open").Locations.Single();
        Check(closedFolder.ValueName == "3" && openFolder.ValueName == "4" && closedFolder.GroupId == openFolder.GroupId,
            "Folder closed/open variants can be selected individually and retain one restore family");
        var networkAll = SystemIconCatalog.Items.Single(t => t.Id == "drive-network-all").Locations;
        Check(networkAll.Select(t => t.ValueName).SequenceEqual(["9", "10"]) && networkAll.Select(t => t.GroupId).Distinct().Count() == 1,
            "Network drive connected/disconnected variants support all-state selection");
        var shortcutOverlay = SystemIconCatalog.Items.Single(t => t.Id == "shortcut-overlay").Locations.Single();
        service.Apply([new(shortcutOverlay, "overlay.ico,0")]);
        using (var root = RegistryWriter.OpenRoot(true))
        using (var key = root.OpenSubKey(SystemIconCatalog.ShellIconsPath))
            Check((string?)key!.GetValue("29") == "overlay.ico,0", "Shortcut overlay uses the allowlisted global Shell Icons slot 29");
        service.Restore(machine.Entries.ToArray());
        var exeUser = new AssociationTarget(".exe", "exefile", @"Software\Classes\exefile\DefaultIcon", null, GroupId: "extension:exe");
        var exeMachine = new AssociationTarget(".exe", "EXE fallback", SystemIconCatalog.ShellIconsPath, null, true, "2", "extension:exe");
        service.Apply([new(exeUser, "%1"), new(exeMachine, "fallback.ico,0")]);
        Check(service.Entries.Select(e => e.GroupKey).Distinct().Count() == 1, "EXE current-user reference and machine fallback group together");
        Check(user.Entries[0].AppliedValue == "%1", "Conditional EXE mode keeps per-file icon lookup");
        service.Restore(service.Entries.ToArray());
        using (var root = RegistryWriter.OpenRoot(false))
            Check(root.OpenSubKey(exeUser.UserRegistryPath) is null, "New empty DefaultIcon key removed on restore");
        bool rejected = false;
        try { service.Apply([new(ext, "temporary.ico,0"), new(exeUser, null!)]); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Mid-apply failure reported");
        using (var root = RegistryWriter.OpenRoot(false))
        using (var key = root.OpenSubKey(ext.UserRegistryPath))
            Check((string?)key!.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames) == @"%SystemRoot%\original.ico,0", "Mid-apply failure rolls back earlier writes");
        service.Restore(service.Entries.ToArray());
        using (var fileLock = user.AcquireLock())
        {
            rejected = false; try { service.Apply([new(ext, "busy.ico,0")]); } catch (IOException) { rejected = true; }
            Check(rejected, "Concurrent writes blocked by exclusive store lock");
        }
        var legacyDirectory = Path.Combine(output, "legacy"); Directory.CreateDirectory(legacyDirectory);
        File.WriteAllText(Path.Combine(legacyDirectory, "backups.json"), "[{\"Extensions\":[\".old\"],\"UserRegistryPath\":\"Software\\\\Classes\\\\old\\\\DefaultIcon\",\"HadKey\":false,\"HadDefaultValue\":false}]");
        var legacy = new BackupStore(testDirectory: legacyDirectory);
        Check(legacy.LoadError is null && legacy.Entries.Count == 1 && !legacy.Entries[0].Machine && legacy.Entries[0].ValueName == "", "Legacy backups load without schema migration or lost defaults");
        var corrupt = Path.Combine(legacyDirectory, "backups.json"); File.WriteAllText(corrupt, "BROKEN"); legacy.Reload();
        rejected = false; try { legacy.Save(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected && File.ReadAllText(corrupt) == "BROKEN", "Corrupt backup blocks writes and preserves source file");
        rejected = false; try { RegistryWriter.Validate(true, @"Software\Classes\exefile\shell\open\command", ""); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "System registry allowlist rejects execution-command mutation");
        var assets = Path.Combine(output, "assets");
        string snapshot = IconSource.PrepareStoredIcon(sourceIcon, 0, assets);
        Check(snapshot == IconSource.PrepareStoredIcon(sourceIcon, 0, assets) && Directory.GetFiles(Path.Combine(assets, "generated-icons")).Length == 1, "Content-addressed ICO snapshots deduplicate and clean temporary files");
        using var invalid = NativeShell.ExtractIcon(Environment.ProcessPath!, 9999);
        Check(invalid is null, "Invalid EXE icon index does not silently use associated generic icon");
        var unprivileged = new IconChangeService(user, machine, () => false);
        rejected = false; try { unprivileged.Apply([new(exeMachine, "forbidden.ico,0")]); } catch (UnauthorizedAccessException) { rejected = true; }
        Check(rejected, "Machine changes require explicit administrator mode");
        var recycle = SystemIconCatalog.Items.Single(t => t.Id == "recycle-empty").Locations[0];
        using (var root = RegistryWriter.OpenRoot(false))
        using (var key = root.CreateSubKey(recycle.UserRegistryPath))
        { key.SetValue("empty", "original-empty.ico,0"); key.SetValue("full", "original-full.ico,0"); key.SetValue("", "original-empty.ico,0"); }
        var recyclePlan = SystemIconCatalog.RecycleWrites(recycle, "new-empty.ico,0", emptyForTest: true);
        Check(recyclePlan.Single(w => w.Target.ValueName == "full").Value == "original-full.ico,0" &&
            recyclePlan.Single(w => w.Target.ValueName == "").Value == "new-empty.ico,0", "Recycle state-specific edit preserves other state and updates current display");
        service.Apply(recyclePlan);
        Check(user.Entries.Count == 3 && user.Entries.Select(e => e.GroupKey).Distinct().Count() == 1, "Recycle empty/full/current display restore atomically as one group");
        service.Restore(user.Entries.ToArray());
        var recycleAll = SystemIconCatalog.Items.Single(t => t.Id == "recycle-all").Locations.Single();
        var recycleAllPlan = SystemIconCatalog.RecycleWrites(recycleAll, "all-recycle.ico,0", emptyForTest: false);
        Check(recycleAllPlan.Where(w => w.Target.ValueName is "empty" or "full").All(w => w.Value == "all-recycle.ico,0") &&
            recycleAllPlan.Single(w => w.Target.ValueName == "").Value == "all-recycle.ico,0", "Recycle all-state selection changes both states and current display");

        var imageDirectory = Path.Combine(output, "rounded-corners"); Directory.CreateDirectory(imageDirectory);
        var sharpPath = Path.Combine(imageDirectory, "sharp.png");
        using (var sharp = new Bitmap(96, 64, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(sharp))
        { graphics.Clear(Color.FromArgb(255, 20, 120, 220)); sharp.Save(sharpPath, ImageFormat.Png); }
        using var sharpSource = new Bitmap(sharpPath);
        using var unchanged = IconSource.ApplyRoundedCorners(sharpSource, 0, out var zeroRounded);
        using var subtle = IconSource.ApplyRoundedCorners(sharpSource, 10, out var subtleRounded);
        using var strong = IconSource.ApplyRoundedCorners(sharpSource, 40, out var strongRounded);
        Check(!zeroRounded && unchanged.GetPixel(0, 0).A == 255, "Corner radius zero preserves a full rectangular source");
        Check(subtleRounded && strongRounded && subtle.GetPixel(0, 0).A == 0 && strong.GetPixel(0, 0).A == 0,
            "Opaque rectangular sources receive transparent rounded corners");
        Check(CountOpaque(strong) < CountOpaque(subtle), "Larger corner percentage produces visibly rounder corners");
        using var alreadyTransparent = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(alreadyTransparent))
        { graphics.Clear(Color.Transparent); graphics.FillRectangle(Brushes.Green, 6, 6, 52, 52); }
        using var preserved = IconSource.ApplyRoundedCorners(alreadyTransparent, 40, out var transparentRounded);
        Check(!transparentRounded && preserved.GetPixel(6, 6).A == 255, "Existing transparent corners are preserved without a second crop");
        string roundedSnapshot = IconSource.PrepareStoredIcon(sharpPath, 0, assets, 24);
        Check(File.Exists(roundedSnapshot.Split(',')[0].Trim('"')), "Rounded image is stored as a reusable multi-size ICO snapshot");
        using var overlay50 = IconSource.CreateOverlayCanvas(sharpSource, 256, 50);
        using var overlay25 = IconSource.CreateOverlayCanvas(sharpSource, 256, 25);
        Check(overlay50.GetPixel(10, 245).A >= 250 && overlay50.GetPixel(200, 245).A == 0 && overlay50.GetPixel(10, 20).A == 0,
            "Shortcut overlay scales into the lower-left corner with transparent top/right canvas");
        Check(CountOpaque(overlay25) < CountOpaque(overlay50), "Shortcut overlay percentage changes occupied canvas area");
        string overlaySnapshot = IconSource.PrepareStoredIcon(sharpPath, 0, assets, 0, shortcutOverlay: true, overlayScalePercent: 50);
        using var storedOverlayIcon = new Icon(overlaySnapshot.Split(',')[0].Trim('"'), new Size(256, 256));
        using var storedOverlay = storedOverlayIcon.ToBitmap();
        int storedY = Math.Max(0, storedOverlay.Height - 2);
        Check(storedOverlay.GetPixel((int)(storedOverlay.Width * .8), storedY).A == 0 &&
              storedOverlay.GetPixel(Math.Max(1, storedOverlay.Width / 20), storedY).A > 0,
            "Stored multi-size shortcut ICO preserves transparent padding and lower-left placement");
    }

    private static int CountOpaque(Bitmap bitmap)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).A >= 250) count++;
        return count;
    }
}
