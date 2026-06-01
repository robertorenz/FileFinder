using System.Windows.Data;
using System.Windows.Markup;

namespace FileFinder.Localization;

/// <summary>
/// XAML markup extension: <c>{l:Loc KeyName}</c> binds a property to the
/// localized string for <c>KeyName</c>, updating live on language change.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public LocExtension() { }
    public LocExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Localization.Instance,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
