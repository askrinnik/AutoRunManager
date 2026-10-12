using System.Windows;
using System.Windows.Input;

namespace AutoRunManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnTitleMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OnCloseMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Close();
    }
}
