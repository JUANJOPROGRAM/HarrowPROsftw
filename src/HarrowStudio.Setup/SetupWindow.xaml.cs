using System.IO.Compression;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

namespace HarrowStudio.Setup;

public partial class SetupWindow : Window
{
    private readonly string _defaultInstallPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "Harrow Studio");

    public SetupWindow()
    {
        InitializeComponent();
        InstallPathBox.Text = _defaultInstallPath;

        var arguments = Environment.GetCommandLineArgs();
        for (var index = 1; index + 1 < arguments.Length; index++)
        {
            if (!string.Equals(arguments[index], "--install-path", StringComparison.OrdinalIgnoreCase)) continue;
            InstallPathBox.Text = Path.GetFullPath(Environment.ExpandEnvironmentVariables(arguments[index + 1]));
            var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Harrow Studio.lnk");
            DesktopShortcutCheckBox.IsChecked = File.Exists(desktopShortcut);
            break;
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InstallPathBox.Text))
        {
            StatusText.Text = "Selecciona una carpeta de instalación.";
            return;
        }

        InstallButton.IsEnabled = false;
        InstallProgress.Visibility = Visibility.Visible;
        InstallProgress.IsIndeterminate = true;
        StatusText.Text = "Instalando Harrow Studio...";

        try
        {
            var installPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(InstallPathBox.Text.Trim()));
            var createDesktopShortcut = DesktopShortcutCheckBox.IsChecked == true;
            await Task.Run(() => InstallApplication(installPath, createDesktopShortcut));
            StatusText.Text = "Instalación completada. Harrow Studio se abrirá ahora.";
            InstallProgress.IsIndeterminate = false;
            InstallProgress.Value = 100;
            InstallButton.Content = "Listo";
            await Task.Delay(600);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.Combine(installPath, "HarrowStudio.Desktop.exe"),
                WorkingDirectory = installPath,
                UseShellExecute = true
            });
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"No se pudo completar la instalación. {ex.Message}";
            InstallProgress.Visibility = Visibility.Collapsed;
            InstallButton.IsEnabled = true;
        }
    }

    private static void InstallApplication(string installPath, bool createDesktopShortcut)
    {
        var prefix = Path.GetFullPath(installPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(installPath);

        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("HarrowStudio.Setup.payload.zip")
            ?? throw new InvalidOperationException("No se encontró el paquete de Harrow Studio dentro del instalador.");
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            var destination = Path.GetFullPath(Path.Combine(installPath, entry.FullName));
            if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El paquete contiene una ruta de archivo no válida.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }

        CreateStartMenuShortcut(installPath);
        var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Harrow Studio.lnk");
        if (createDesktopShortcut)
            CreateShortcut(desktopShortcut, installPath);
        else if (File.Exists(desktopShortcut))
            File.Delete(desktopShortcut);
        RegisterUninstaller(installPath);
    }

    private static void CreateStartMenuShortcut(string installPath)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var shortcutDirectory = Path.Combine(programs, "Harrow Studio");
        CreateShortcut(Path.Combine(shortcutDirectory, "Harrow Studio.lnk"), installPath);
    }

    private static void CreateShortcut(string shortcutPath, string installPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows no pudo crear el acceso directo.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = Path.Combine(installPath, "HarrowStudio.Desktop.exe");
            shortcut.WorkingDirectory = installPath;
            shortcut.Description = "Perfiles y macros para Redragon Harrow Pro G808";
            shortcut.Save();
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void RegisterUninstaller(string installPath)
    {
        var legacyScript = Path.Combine(installPath, "Uninstall-HarrowStudio.ps1");
        if (File.Exists(legacyScript)) File.Delete(legacyScript);

        using var uninstallKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\HarrowStudio");
        uninstallKey.SetValue("DisplayName", "Harrow Studio");
        uninstallKey.SetValue("DisplayVersion", "0.1.2");
        uninstallKey.SetValue("Publisher", "Harrow Studio");
        uninstallKey.SetValue("InstallLocation", installPath);
        uninstallKey.SetValue("DisplayIcon", Path.Combine(installPath, "HarrowStudio.Desktop.exe"));
        uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
        uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        var appPath = Path.Combine(installPath, "HarrowStudio.Desktop.exe");
        uninstallKey.SetValue("UninstallString", $"\"{appPath}\" --uninstall");
    }
}
