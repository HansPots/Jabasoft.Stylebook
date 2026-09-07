using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Stylebook.Components.Theming;

namespace Stylebook.Playground;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Theme _currentTheme = Theme.Dark;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme == Theme.Dark ? Theme.Light : Theme.Dark;
        ThemeManager.Apply(_currentTheme);
    }
}