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
        Loaded += (_, _) => SearchInput.Focus();
    }

    private void ResultsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsGrid.SelectedItem is FileRow row &&
            DataContext is MainViewModel vm)
        {
            vm.OpenFileCommand.Execute(row);
        }
    }
}
