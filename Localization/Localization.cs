using System.ComponentModel;

namespace FileFinder.Localization;

/// <summary>
/// Runtime localization manager. XAML binds to the string table through the
/// indexer (via the <see cref="LocExtension"/> markup extension); changing
/// <see cref="CurrentLanguage"/> raises a change on the indexer so every bound
/// string updates live without a restart.
/// </summary>
public sealed class Localization : INotifyPropertyChanged
{
    public static Localization Instance { get; } = new();

    public static IReadOnlyList<LanguageOption> Languages { get; } = new[]
    {
        new LanguageOption("en", "English"),
        new LanguageOption("es", "Español"),
    };

    private string _lang = "en";
    private Dictionary<string, string> _table = Strings.En;

    public string CurrentLanguage
    {
        get => _lang;
        set
        {
            string code = value == "es" ? "es" : "en";
            if (_lang == code) return;
            _lang = code;
            _table = code == "es" ? Strings.Es : Strings.En;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        }
    }

    /// <summary>Looks up a key, falling back to English then the key itself.</summary>
    public string this[string key]
        => _table.TryGetValue(key, out var v) ? v
         : Strings.En.TryGetValue(key, out var e) ? e
         : key;

    /// <summary>Convenience for composite strings: localized format + args.</summary>
    public string Format(string key, params object[] args) => string.Format(this[key], args);

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>A selectable UI language. Uses real properties so WPF can bind to them.</summary>
public sealed class LanguageOption
{
    public string Code { get; }
    public string Name { get; }
    public LanguageOption(string code, string name) { Code = code; Name = name; }
}
