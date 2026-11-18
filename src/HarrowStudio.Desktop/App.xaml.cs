using System.Configuration;
using System.Data;
using System.Windows;

namespace HarrowStudio.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            Window window = e.Args.Any(argument => string.Equals(argument, "--uninstall", StringComparison.OrdinalIgnoreCase))
                ? new UninstallWindow()
                : new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Harrow Studio no pudo iniciar.\n\n{exception.Message}",
                "Error al iniciar Harrow Studio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}

