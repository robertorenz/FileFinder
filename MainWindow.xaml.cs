using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileFinder.Models;
using FileFinder.ViewModels;

namespace FileFinder;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            SearchInput.Focus();
            if (DataContext is MainViewModel vm)
                await vm.InitializeAsync();
        };
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is FileRow row &&
            DataContext is MainViewModel vm)
        {
            vm.OpenFileCommand.Execute(row);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
