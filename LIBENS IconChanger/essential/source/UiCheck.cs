using System.Drawing.Imaging;
using System.Text.Json;

namespace LibensIconChanger;

internal static class UiCheck
{
    public static void RunNative(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var form = new MainForm(checkMode: true);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10000, -10000);
        form.MaximumSize = new Size(6000,6000);
        form.ShowInTaskbar = false;
        form.Show();
        form.SetCheckState();
        Application.DoEvents();
        AssertLanguage(form);
        AssertSpecialOptions(form, iconIndex: false, overlayScale: false);
        Save(form, Path.Combine(outputDirectory, "ui-native-dpi.png"));
        form.SetSystemCheckState(); Application.DoEvents();
        AssertSpecialOptions(form, iconIndex: false, overlayScale: false);
        Save(form, Path.Combine(outputDirectory, "ui-native-system.png"));
        var systemMenu = form.CreateSystemChoicesMenu();
        try
        {
            var menuItems = systemMenu.Items.OfType<ToolStripMenuItem>().ToArray();
            if (menuItems.Length != SystemIconCatalog.Items.Length ||
                menuItems.Any(item => item.Image is null || item.Image.Width < 32 || item.Image.Height < 32))
                throw new InvalidOperationException(AppText.T("Windows 기본 아이콘 목록의 32px 미리보기가 누락되었습니다."));
            systemMenu.AutoSize = false;
            systemMenu.Size = systemMenu.GetPreferredSize(Size.Empty);
            systemMenu.CreateControl();
            systemMenu.PerformLayout();
            using var menuImage = new Bitmap(systemMenu.Width, systemMenu.Height);
            systemMenu.DrawToBitmap(menuImage, new Rectangle(Point.Empty, systemMenu.Size));
            menuImage.Save(Path.Combine(outputDirectory, "ui-native-system-menu.png"));
        }
        finally { MainForm.DisposeSystemChoicesMenu(systemMenu); }
        form.SetShortcutOverlayCheckState(); Application.DoEvents();
        AssertSpecialOptions(form, iconIndex: false, overlayScale: true);
        Save(form, Path.Combine(outputDirectory, "ui-native-shortcut-overlay.png"));
        form.SetNativeIconSourceCheckState(); Application.DoEvents();
        AssertSpecialOptions(form, iconIndex: true, overlayScale: true);
        Save(form, Path.Combine(outputDirectory, "ui-native-shortcut-overlay-exe-source.png"));
        form.SetSystemCheckState(exe: true); Application.DoEvents();
        AssertSpecialOptions(form, iconIndex: true, overlayScale: false);
        Save(form, Path.Combine(outputDirectory, "ui-native-exe.png"));
        using (var dialog = AppDialog.Create(form, AppText.T("아이콘 적용 확인 · 검수"), SystemIconCatalog.Items[0].LocalizedDescription + AppText.T("\n\n검수용 창입니다. 실제 설정은 바꾸지 않습니다."), true))
        {
            dialog.StartPosition = FormStartPosition.Manual; dialog.Location = new Point(-10000, -10000);
            dialog.Show(); Application.DoEvents();
            AssertLanguage(dialog);
            if (AppText.Language == "en" && dialog.Controls.OfType<TextBox>().Any(control =>
                control.Text.Any(ch => ch is >= '\uAC00' and <= '\uD7A3')))
                throw new InvalidOperationException("Untranslated English confirmation dialog.");
            using var bitmap = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size));
            bitmap.Save(Path.Combine(outputDirectory, "ui-native-dialog.png"));
            dialog.Close();
        }
        File.WriteAllText(Path.Combine(outputDirectory, "native-dpi.txt"), $"Device DPI: {form.DeviceDpi}; Client: {form.ClientSize}");
        form.Close();
    }

    public static void Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var report = new List<string>();
        foreach (var factor in new[] { 1f, 1.25f, 1.5f, 2f })
        {
            using var form = new MainForm(checkMode: true);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-10000, -10000);
            form.ShowInTaskbar = false;
            form.MaximumSize = new Size(6000, 6000);
            form.Show();
            Application.DoEvents();
            // Layout scale simulation; not a claim of changing the OS monitor DPI.
            form.AutoScaleMode = AutoScaleMode.None;
            if (factor != 1)
            {
                var fonts = Descendants(form).ToDictionary(c => c, c => (c.Font.FontFamily.Name, c.Font.Size, c.Font.Style));
                form.Scale(new SizeF(factor, factor));
                foreach (var entry in fonts)
                    entry.Key.Font = new Font(entry.Value.Name, entry.Value.Size * factor, entry.Value.Style);
                form.ClientSize = new Size((int)(1080 * factor), (int)(778 * factor));
            }
            form.PerformLayout();
            Application.DoEvents();
            Save(form, Path.Combine(outputDirectory, $"ui-{factor * 100:0}-empty.png"));
            form.SetCheckState();
            form.PerformLayout();
            Application.DoEvents();
            AssertSpecialOptions(form, iconIndex: false, overlayScale: false);
            AssertLanguage(form);
            AssertLanguageSwitch(form);
            Save(form, Path.Combine(outputDirectory, $"ui-{factor * 100:0}-selected.png"));
            Inspect(form, report, factor);
            form.SetSystemCheckState();
            Application.DoEvents();
            AssertSpecialOptions(form, iconIndex: false, overlayScale: false);
            Save(form, Path.Combine(outputDirectory, $"ui-{factor * 100:0}-system.png"));
            Inspect(form, report, factor);
            form.SetShortcutOverlayCheckState();
            Application.DoEvents();
            AssertSpecialOptions(form, iconIndex: false, overlayScale: true);
            AssertLanguage(form);
            Save(form, Path.Combine(outputDirectory, $"ui-{factor * 100:0}-shortcut-overlay.png"));
            Inspect(form, report, factor);
            form.SetNativeIconSourceCheckState();
            Application.DoEvents();
            AssertSpecialOptions(form, iconIndex: true, overlayScale: true);
            Save(form, Path.Combine(outputDirectory, $"ui-{factor * 100:0}-shortcut-overlay-exe-source.png"));
            Inspect(form, report, factor);
            form.SetSystemCheckState(exe: true);
            Application.DoEvents();
            AssertSpecialOptions(form, iconIndex: true, overlayScale: false);
            Save(form, Path.Combine(outputDirectory, $"ui-{factor * 100:0}-exe.png"));
            Inspect(form, report, factor);
            form.Close();
        }
        using (var minimum = new MainForm(checkMode: true))
        {
            minimum.StartPosition = FormStartPosition.Manual;
            minimum.Location = new Point(-10000,-10000);
            minimum.ShowInTaskbar = false;
            minimum.Size = minimum.MinimumSize;
            minimum.Show();
            Application.DoEvents();
            Save(minimum, Path.Combine(outputDirectory, "ui-minimum.png"));
            Inspect(minimum, report, 0);
            minimum.Close();
        }
        File.WriteAllText(Path.Combine(outputDirectory, "layout-check.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        if (report.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, report));
    }

    private static void Save(Form form, string path)
    {
        using var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.Controls[0].DrawToBitmap(image, new Rectangle(Point.Empty, form.ClientSize));
        image.Save(path, ImageFormat.Png);
    }

    private static void AssertLanguage(Form form)
    {
        if (AppText.Language != "en") return;
        var untranslated = Descendants(form)
            .Where(control => control.Visible && control is Label or Button)
            .Select(control => control.Text)
            .Where(text => text.Any(ch => ch is >= '\uAC00' and <= '\uD7A3')).ToArray();
        if (untranslated.Length > 0)
            throw new InvalidOperationException("Untranslated English UI: " + string.Join(" | ", untranslated));
        foreach (var target in SystemIconCatalog.Items)
            if ((target.LocalizedName + target.LocalizedDescription).Any(ch => ch is >= '\uAC00' and <= '\uD7A3'))
                throw new InvalidOperationException("Untranslated system target: " + target.Id);
    }

    private static void AssertLanguageSwitch(MainForm form)
    {
        string original = AppText.Language;
        var inputs = Descendants(form).OfType<TextBox>().ToDictionary(control => control, control => control.Text);
        var numbers = Descendants(form).OfType<NumericUpDown>().ToDictionary(control => control, control => control.Value);
        form.SetUiLanguage(original == "en" ? "ko" : "en");
        Application.DoEvents();
        AssertLanguage(form);
        form.SetUiLanguage(original);
        Application.DoEvents();
        AssertLanguage(form);
        if (inputs.Any(entry => entry.Key.Text != entry.Value) || numbers.Any(entry => entry.Key.Value != entry.Value))
            throw new InvalidOperationException("Language switching changed the user's input.");
    }

    private static void Inspect(Control parent, List<string> report, float factor)
    {
        foreach (Control child in parent.Controls)
        {
            if (child.Visible && child is Button or TextBox or NumericUpDown or InputFrame)
            {
                if (child.Width <= 0 || child.Height <= 0 ||
                    child.Left < 0 || child.Top < 0 || child.Right > parent.ClientSize.Width + 2 || child.Bottom > parent.ClientSize.Height + 2)
                    report.Add($"{factor}: {child.GetType().Name} {child.Text} outside parent: {child.Bounds} / {parent.ClientSize}");
            }
            Inspect(child, report, factor);
        }
    }

    private static void AssertSpecialOptions(MainForm form, bool iconIndex, bool overlayScale)
    {
        bool iconVisible = Descendants(form).Any(control => control.AccessibleName == AppText.T("EXE 또는 DLL 아이콘 번호") && control.Visible);
        bool overlayVisible = Descendants(form).Any(control => control.AccessibleName == AppText.T("바로가기 오버레이 크기 백분율") && control.Visible);
        if (iconVisible != iconIndex || overlayVisible != overlayScale)
            throw new InvalidOperationException($"특수 옵션 표시 오류: 아이콘 번호={iconVisible}/{iconIndex}, 오버레이={overlayVisible}/{overlayScale}");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        yield return parent;
        foreach (Control child in parent.Controls)
            foreach (var item in Descendants(child)) yield return item;
    }
}
