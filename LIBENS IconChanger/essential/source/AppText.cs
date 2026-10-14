using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace LibensIconChanger;

internal static class AppText
{
    private static readonly Dictionary<string, string> English = LoadEnglish();
    private static readonly Dictionary<string, string> Korean = English
        .GroupBy(entry => entry.Value, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    public static string Language { get; private set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? "ko" : "en";

    public static void SetLanguage(string language)
    {
        if (language is not ("ko" or "en")) throw new ArgumentException("Supported languages: ko, en.", nameof(language));
        Language = language;
    }

    public static string T(string text) => Language == "en" && English.TryGetValue(text, out var translated) ? translated : text;
    public static string Original(string text) => Korean.TryGetValue(text, out var original) ? original : text;
    public static string F(string template, params object[] values) => string.Format(CultureInfo.CurrentCulture, T(template), values);
    public static string CatalogText(string text) => English.ContainsKey(text) ? T(text) : string.Join("\n", text.Split('\n').Select(T));

    // Translate only app-owned labels, buttons, headers and metadata. User input,
    // file paths, registry identities and backup JSON are never transformed.
    public static void Apply(Control control)
    {
        string TranslateCurrent(string text) => T(Original(text));
        if (control is Label or Button or Form) control.Text = TranslateCurrent(control.Text);
        if (control.AccessibleName is { } name) control.AccessibleName = TranslateCurrent(name);
        if (control is TextBox textBox) textBox.PlaceholderText = TranslateCurrent(textBox.PlaceholderText);
        if (control is ListView list)
            foreach (ColumnHeader column in list.Columns) column.Text = TranslateCurrent(column.Text);
        foreach (Control child in control.Controls) Apply(child);
    }

    private static Dictionary<string, string> LoadEnglish()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LibensIconChanger.Translations.en.json")
            ?? throw new InvalidOperationException("English translations are missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException("English translations are empty.");
    }
}
