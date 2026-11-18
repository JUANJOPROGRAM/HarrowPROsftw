using System.Diagnostics;
using System.IO;
using System.Windows;

namespace HarrowStudio.Desktop;

public partial class UninstallWindow : Window
{
    public UninstallWindow()
    {
        InitializeComponent();
        InstallPathBox.Text = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        UninstallButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        UninstallProgress.Visibility = Visibility.Visible;
        UninstallProgress.IsIndeterminate = true;
        StatusText.Text = "Preparando la desinstalación…";

        try
        {
            var installPath = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            var scriptPath = Path.Combine(Path.GetTempPath(), $"HarrowStudio-uninstall-{Guid.NewGuid():N}.ps1");
            File.WriteAllText(scriptPath, CreateRemovalScript(installPath));

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            var removalProcess = Process.Start(startInfo);
            if (removalProcess is null)
                throw new InvalidOperationException("No se pudo iniciar el proceso de desinstalación.");
            removalProcess.Dispose();

            StatusText.Text = "Desinstalación iniciada. Tus perfiles se conservarán.";
            await Task.Delay(900);
            Close();
        }
        catch (Exception exception)
        {
            UninstallProgress.Visibility = Visibility.Collapsed;
            UninstallButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            StatusText.Text = $"No se pudo iniciar la desinstalación. {exception.Message}";
        }
    }

    private static string CreateRemovalScript(string installPath)
    {
        var startMenuDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Harrow Studio");
        var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Harrow Studio.lnk");
        return $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            Start-Sleep -Seconds 2
            $installDir = {{PowerShellLiteral(installPath)}}
            $startMenuDirectory = {{PowerShellLiteral(startMenuDirectory)}}
            $desktopShortcut = {{PowerShellLiteral(desktopShortcut)}}
            Remove-Item -LiteralPath $startMenuDirectory -Recurse -Force
            Remove-Item -LiteralPath $desktopShortcut -Force
            Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HarrowStudio' -Recurse -Force
            for ($attempt = 0; $attempt -lt 30 -and (Test-Path -LiteralPath $installDir); $attempt++) {
                Remove-Item -LiteralPath $installDir -Recurse -Force
                if (Test-Path -LiteralPath $installDir) { Start-Sleep -Seconds 1 }
            }
            Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force
            """;
    }

    private static string PowerShellLiteral(string value) => $"'{value.Replace("'", "''")}'";
}
