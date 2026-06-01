using System.Windows;

namespace FileFinder.ViewModels;

/// <summary>
/// Freezable proxy that carries the DataContext into places that aren't in the
/// visual tree (e.g. DataGridColumn.Visibility), so columns can bind to the VM.
/// </summary>
public sealed class BindingProxy : Freezable
{
    protected override Freezable CreateInstanceCore() => new BindingProxy();

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));
}
