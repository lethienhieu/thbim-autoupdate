using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using THBIM.AutoUpdate.Helpers;
using THBIM.AutoUpdate.ViewModels;

namespace THBIM.AutoUpdate.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            AcrylicHelper.EnableAcrylic(this, 0x99000000);
        }
        catch
        {
            // Fallback: solid dark background if acrylic is not supported
        }
    }

    private void Titlebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void Minimize_Click(object sender, MouseButtonEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, MouseButtonEventArgs e)
    {
        Hide();
    }

    private void SettingsOverlay_Close(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsSettingsOpen = false;
    }

    private void TabAddins_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SelectedTab = 0;
    }

    private void TabAccount_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SelectedTab = 1;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is System.Windows.Controls.PasswordBox pb)
            vm.AccountVM.Password = pb.Password;
    }

    private void SignUp_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://thbim.pages.dev") { UseShellExecute = true }); }
        catch { }
    }

    private void CopyMachineId_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            try { Clipboard.SetText(vm.AccountVM.MachineId); }
            catch { }
        }
    }
}
