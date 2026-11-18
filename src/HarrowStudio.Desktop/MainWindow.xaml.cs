using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Automation;
using System.Windows.Threading;
using HarrowStudio.Application;
using HarrowStudio.Domain;
using HarrowStudio.Infrastructure;

namespace HarrowStudio.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IUpdateService _updateService;
    private readonly DispatcherTimer _controllerTimer;
    private AppRelease? _availableUpdate;
    private bool _updateInProgress;
    private bool? _compactNavigation;
    private bool? _stackedContent;
    private bool? _stackedAssignments;
    private bool? _stackedHeader;
    private bool _sidebarCollapsed;
    private bool _capturingControllerButton;
    private DateTime _captureDeadline;
    private HashSet<string> _previousPressedButtons = new(StringComparer.OrdinalIgnoreCase);
    private FrameworkElement? _currentPage;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(new ProfileService(new JsonProfileRepository()), new XInputControllerInput());
        _updateService = new GitHubReleaseUpdateService();
        DataContext = _viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SelectedBinding) or nameof(MainViewModel.Controller))
                RefreshAssignmentHotspots();
        };
        _currentPage = PanelView;
        UpdateNavigationSelection("Panel");
        _controllerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _controllerTimer.Tick += (_, _) =>
        {
            _viewModel.PollController();
            ReadCapturedButton();
            RefreshAssignmentHotspots();
        };
        Closed += (_, _) => _controllerTimer.Stop();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();
            BuildAssignmentHotspots();
            _viewModel.PollController();
            _controllerTimer.Start();
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            AppRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(460)) { EasingFunction = ease });
            AppRoot.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(520)) { EasingFunction = ease });
            ApplyResponsiveLayout();
            _ = CheckForUpdatesAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"No se pudo cargar Harrow Studio.\n\n{exception.Message}",
                "Error al cargar Harrow Studio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout(bool animateSidebar = false)
    {
        var compactNavigation = ActualWidth < 820;
        var stackedContent = ActualWidth < 1080;
        var stackedAssignments = ActualWidth < 1080;
        var stackedHeader = ActualWidth < 760;
        UpdateSidebarLayout(compactNavigation, animateSidebar);

        if (_stackedContent != stackedContent)
        {
            _stackedContent = stackedContent;
            ProductColumn.Width = stackedContent ? new GridLength(1, GridUnitType.Star) : new GridLength(1.35, GridUnitType.Star);
            DetailsColumn.Width = stackedContent ? new GridLength(1, GridUnitType.Star) : new GridLength(.95, GridUnitType.Star);
            ProductCard.SetValue(Grid.RowProperty, 0);
            DetailsPanel.SetValue(Grid.RowProperty, stackedContent ? 1 : 0);
            ProductCard.SetValue(Grid.ColumnSpanProperty, stackedContent ? 2 : 1);
            DetailsPanel.SetValue(Grid.ColumnProperty, stackedContent ? 0 : 1);
            DetailsPanel.SetValue(Grid.ColumnSpanProperty, stackedContent ? 2 : 1);
            ProductCard.Margin = stackedContent ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 7, 0);
            DetailsPanel.Margin = stackedContent ? new Thickness(0) : new Thickness(7, 0, 0, 0);

        }

        if (_stackedAssignments != stackedAssignments)
        {
            _stackedAssignments = stackedAssignments;
            AssignmentImageColumn.Width = stackedAssignments ? new GridLength(1, GridUnitType.Star) : new GridLength(2.05, GridUnitType.Star);
            AssignmentEditorColumn.Width = stackedAssignments ? new GridLength(1, GridUnitType.Star) : new GridLength(.95, GridUnitType.Star);
            ControllerAssignmentCard.SetValue(Grid.RowProperty, 0);
            ControllerAssignmentCard.SetValue(Grid.ColumnSpanProperty, stackedAssignments ? 2 : 1);
            AssignmentEditorPanel.SetValue(Grid.RowProperty, stackedAssignments ? 1 : 0);
            AssignmentEditorPanel.SetValue(Grid.ColumnProperty, stackedAssignments ? 0 : 1);
            AssignmentEditorPanel.SetValue(Grid.ColumnSpanProperty, stackedAssignments ? 2 : 1);
            ControllerAssignmentCard.Margin = stackedAssignments ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 0);
            AssignmentEditorPanel.Margin = stackedAssignments ? new Thickness(0) : new Thickness(0);
        }

        if (_stackedHeader != stackedHeader)
        {
            _stackedHeader = stackedHeader;
            HeaderActions.SetValue(Grid.RowProperty, stackedHeader ? 1 : 0);
            HeaderActions.SetValue(Grid.ColumnProperty, stackedHeader ? 0 : 1);
            HeaderActions.HorizontalAlignment = stackedHeader ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            HeaderActions.Margin = stackedHeader ? new Thickness(0, 12, 0, 0) : new Thickness(0);
            HeaderSubtitle.Visibility = ActualWidth < 700 ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            HeaderSubtitle.Visibility = ActualWidth < 700 ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await _viewModel.SaveAsync();

    private async Task CheckForUpdatesAsync()
    {
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 1);
        var update = await _updateService.CheckForUpdateAsync(currentVersion);
        if (!IsLoaded || update is null) return;

        _availableUpdate = update;
        UpdateAvailableButton.Content = $"Actualizar · v{update.Version}";
        UpdateAvailableButton.Visibility = Visibility.Visible;
    }

    private async void UpdateAvailable_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is null || _updateInProgress) return;
        var notes = string.IsNullOrWhiteSpace(_availableUpdate.ReleaseNotes)
            ? "Esta versión incluye mejoras y correcciones."
            : _availableUpdate.ReleaseNotes.Trim();
        if (notes.Length > 700) notes = notes[..700] + "…";

        var answer = MessageBox.Show(
            this,
            $"Harrow Studio v{_availableUpdate.Version} está disponible.\n\n{notes}\n\n¿Descargar e instalar ahora?",
            "Actualización disponible",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (answer != MessageBoxResult.Yes) return;

        _updateInProgress = true;
        UpdateAvailableButton.IsEnabled = false;
        UpdateAvailableButton.Content = "Descargando…";
        var installerPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"HarrowStudioSetup-{_availableUpdate.Version}.exe");
        var updaterScript = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"HarrowStudioUpdate-{Guid.NewGuid():N}.ps1");
        try
        {
            await _updateService.DownloadInstallerAsync(_availableUpdate, installerPath);
            System.IO.File.WriteAllText(updaterScript, CreateUpdateLauncherScript(installerPath, System.IO.Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), Environment.ProcessId));

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
            startInfo.ArgumentList.Add(updaterScript);
            var updater = Process.Start(startInfo);
            if (updater is null) throw new InvalidOperationException("No se pudo iniciar el actualizador.");
            updater.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            _updateInProgress = false;
            UpdateAvailableButton.IsEnabled = true;
            UpdateAvailableButton.Content = $"Actualizar · v{_availableUpdate.Version}";
            MessageBox.Show(this, $"No se pudo descargar la actualización.\n\n{exception.Message}", "Error de actualización", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string CreateUpdateLauncherScript(string installerPath, string installPath, int processId) => $$"""
        $ErrorActionPreference = 'Stop'
        $targetProcessId = {{processId}}
        while (Get-Process -Id $targetProcessId -ErrorAction SilentlyContinue) { Start-Sleep -Milliseconds 250 }
        $installerPath = {{PowerShellLiteral(installerPath)}}
        $installPath = {{PowerShellLiteral(installPath)}}
        Start-Process -FilePath $installerPath -ArgumentList ('--install-path "' + $installPath + '"')
        Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force
        """;

    private static string PowerShellLiteral(string value) => $"'{value.Replace("'", "''")}'";

    private async void NewMacro_Click(object sender, RoutedEventArgs e)
    {
        var editor = new MacroEditorWindow
        {
            Owner = this,
            Width = Math.Min(580, Math.Max(420, ActualWidth - 60)),
            Height = Math.Min(610, Math.Max(440, ActualHeight - 60))
        };
        BeginAnimation(OpacityProperty, new DoubleAnimation(.62, TimeSpan.FromMilliseconds(150)));
        bool? result;
        try { result = editor.ShowDialog(); }
        finally { BeginAnimation(OpacityProperty, null); Opacity = 1; }
        if (result == true && editor.Result is not null)
            await _viewModel.AddMacroAsync(editor.Result);
    }

    private async void DeleteMacro_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is MacroDefinition macro)
            await _viewModel.DeleteMacroAsync(macro);
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string target) return;
        _viewModel.SelectedNavigation = target;
        UpdateNavigationSelection(target);
        ShowPage(target);
    }

    private void UpdateSidebarLayout(bool compactNavigation, bool animate)
    {
        var compactChanged = _compactNavigation != compactNavigation;
        _compactNavigation = compactNavigation;
        var iconOnly = compactNavigation || _sidebarCollapsed;
        if (compactChanged || animate)
        {
            BrandWordmark.Visibility = ConfigHeading.Visibility = DeviceCard.Visibility = SidebarFooter.Visibility = iconOnly ? Visibility.Collapsed : Visibility.Visible;
            PanelNavLabel.Visibility = AssignmentsNavLabel.Visibility = MacrosNavLabel.Visibility = ProfilesNavLabel.Visibility = iconOnly ? Visibility.Collapsed : Visibility.Visible;
            MacroBadge.Visibility = iconOnly ? Visibility.Collapsed : Visibility.Visible;
            foreach (var nav in new[] { PanelNav, AssignmentsNav, MacrosNav, ProfilesNav })
            {
                nav.HorizontalContentAlignment = iconOnly ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                nav.Padding = iconOnly ? new Thickness(2, 10, 2, 10) : new Thickness(13, 11, 13, 11);
                nav.ToolTip = iconOnly ? nav.Tag?.ToString() : null;
            }
        }

        var targetWidth = compactNavigation ? (_sidebarCollapsed ? 0 : 68) : (_sidebarCollapsed ? 68 : 205);
        var currentWidth = SidebarColumn.ActualWidth;
        SidebarColumn.Width = new GridLength(targetWidth);
        if (animate && Math.Abs(currentWidth - targetWidth) > .5)
        {
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, new GridLengthAnimation
            {
                From = new GridLength(currentWidth),
                To = new GridLength(targetWidth),
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });
        }

        SidebarToggleButton.ToolTip = _sidebarCollapsed ? "Desplegar navegación" : "Plegar navegación";
        SidebarToggleButton.Content = _sidebarCollapsed ? "☰" : "‹";
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        ApplyResponsiveLayout(animateSidebar: true);
    }

    private void UpdateNavigationSelection(string target)
    {
        foreach (var button in new[] { PanelNav, AssignmentsNav, MacrosNav, ProfilesNav })
        {
            var selected = string.Equals(button.Tag?.ToString(), target, StringComparison.Ordinal);
            button.Background = selected ? new SolidColorBrush(Color.FromRgb(25, 26, 31)) : Brushes.Transparent;
            button.Foreground = selected ? (Brush)FindResource("TextPrimary") : (Brush)FindResource("TextSecondary");
        }
    }

    private void ShowPage(string target)
    {
        var next = target switch
        {
            "Asignaciones" => AssignmentsView,
            "Macros" => MacrosView,
            "Perfiles" => ProfilesView,
            _ => PanelView
        };
        if (ReferenceEquals(next, _currentPage)) return;

        var previous = _currentPage;
        _currentPage = next;
        next.Visibility = Visibility.Visible;
        next.Opacity = 0;
        next.RenderTransform = new TranslateTransform(0, 8);

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        next.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        ((TranslateTransform)next.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });

        if (previous is not null)
        {
            var fadeOut = new DoubleAnimation(previous.Opacity, 0, TimeSpan.FromMilliseconds(130));
            fadeOut.Completed += (_, _) =>
            {
                if (!ReferenceEquals(previous, _currentPage))
                {
                    previous.Visibility = Visibility.Collapsed;
                    previous.BeginAnimation(OpacityProperty, null);
                    previous.Opacity = 1;
                }
            };
            previous.BeginAnimation(OpacityProperty, fadeOut);
        }
        MainScrollViewer.ScrollToTop();
        RefreshAssignmentHotspots();
    }

    private void BuildAssignmentHotspots()
    {
        var positions = new (string Button, double X, double Y)[]
        {
            ("A", 464, 230), ("B", 495, 199), ("X", 433, 199), ("Y", 465, 168),
            ("LB", 256, 137), ("RB", 407, 137), ("LT", 111, 124), ("RT", 548, 124),
            ("L3", 197, 201), ("R3", 404, 271), ("Back", 295, 196), ("Start", 374, 196),
            ("D-pad ↑", 270, 242), ("D-pad ↓", 270, 313), ("D-pad ←", 234, 278), ("D-pad →", 306, 278)
        };

        const double imageX = 50;
        const double imageY = 12;
        const double imageScale = 720d / 660d;
        var leftLabels = new (string Button, double Y)[]
        {
            ("LT", 64), ("LB", 126), ("L3", 188), ("D-pad ↑", 250),
            ("D-pad ←", 312), ("D-pad →", 374), ("D-pad ↓", 436), ("Back", 498)
        };
        var rightLabels = new (string Button, double Y)[]
        {
            ("RT", 64), ("RB", 126), ("Y", 188), ("X", 250),
            ("B", 312), ("A", 374), ("R3", 436), ("Start", 498)
        };

        foreach (var (source, labelY) in leftLabels.Concat(rightLabels))
        {
            var (buttonName, sourceX, sourceY) = positions.First(item => item.Button == source);
            var isLeft = leftLabels.Any(item => item.Button == source);
            var start = new Point(imageX + sourceX * imageScale, imageY + sourceY * imageScale);
            var lineEnd = new Point(isLeft ? 8 + source.Length * 9 + 16 : 716, labelY);
            var binding = _viewModel.Bindings.FirstOrDefault(item => item.SourceButton == buttonName);
            if (binding is null) continue;

            var curve = new PathFigure { StartPoint = start, IsClosed = false };
            var side = isLeft ? -1 : 1;
            curve.Segments.Add(new BezierSegment(
                new Point(start.X + side * 92, start.Y),
                new Point(lineEnd.X - side * 75, lineEnd.Y),
                lineEnd,
                true));
            var geometry = new PathGeometry();
            geometry.Figures.Add(curve);
            var line = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Tag = binding.SourceButton,
                Stroke = new SolidColorBrush(Color.FromArgb(188, 247, 248, 250)),
                StrokeThickness = 1.5,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };
            ControllerCanvas.Children.Add(line);

            var hitTarget = new Button
            {
                Width = 42,
                Height = 42,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Tag = binding.SourceButton,
                Cursor = Cursors.Hand,
                Focusable = false,
                ToolTip = $"Configurar {binding.SourceButton}"
            };
            AutomationProperties.SetName(hitTarget, binding.SourceButton);
            hitTarget.Click += AssignmentHotspot_Click;
            Canvas.SetLeft(hitTarget, start.X - 21);
            Canvas.SetTop(hitTarget, start.Y - 21);
            ControllerCanvas.Children.Add(hitTarget);

            var label = new TextBlock
            {
                Text = binding.SourceButton,
                Tag = binding.SourceButton,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(242, 8, 9, 11)),
                Padding = new Thickness(8, 5, 8, 5),
                Cursor = Cursors.Hand,
                ToolTip = $"Seleccionar {binding.SourceButton}",
                TextTrimming = TextTrimming.None
            };
            AutomationProperties.SetName(label, binding.SourceButton);
            label.MouseLeftButtonUp += (_, _) => AssignmentHotspot_Select(label.Tag?.ToString());
            Canvas.SetLeft(label, isLeft ? 8 : 716);
            Canvas.SetTop(label, labelY - 15);
            ControllerCanvas.Children.Add(label);

            var dot = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromArgb(235, 250, 250, 252)),
                Stroke = new SolidColorBrush(Color.FromRgb(12, 13, 15)),
                StrokeThickness = 2,
                Tag = binding.SourceButton,
                Cursor = Cursors.Hand,
                ToolTip = binding.SourceButton
            };
            dot.MouseLeftButtonUp += (_, _) => AssignmentHotspot_Select(dot.Tag?.ToString());
            Canvas.SetLeft(dot, start.X - 6);
            Canvas.SetTop(dot, start.Y - 6);
            ControllerCanvas.Children.Add(dot);
        }

        var controllerImage = ControllerCanvas.Children.OfType<Image>().First();
        var leaders = ControllerCanvas.Children.OfType<System.Windows.Shapes.Path>().Cast<UIElement>().ToArray();
        var hitTargets = ControllerCanvas.Children.OfType<Button>().Cast<UIElement>().ToArray();
        var labels = ControllerCanvas.Children.OfType<TextBlock>().Cast<UIElement>().ToArray();
        var markers = ControllerCanvas.Children.OfType<Ellipse>().Cast<UIElement>().ToArray();
        ControllerCanvas.Children.Clear();
        ControllerCanvas.Children.Add(controllerImage);
        foreach (var element in leaders.Concat(hitTargets).Concat(labels).Concat(markers))
            ControllerCanvas.Children.Add(element);
        RefreshAssignmentHotspots();
    }

    private void AssignmentHotspot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string source })
            AssignmentHotspot_Select(source);
    }

    private void AssignmentHotspot_Select(string? source)
    {
        if (source is null) return;
        CancelButtonCapture();
        _viewModel.SelectedBinding = _viewModel.Bindings.FirstOrDefault(binding => binding.SourceButton == source);
        AssignmentBindingsList.ScrollIntoView(_viewModel.SelectedBinding);
    }

    private void AssignmentBindingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_capturingControllerButton)
            CancelButtonCapture("Control seleccionado desde la lista.");
        RefreshAssignmentHotspots();
    }

    private void RefreshAssignmentHotspots()
    {
        if (ControllerCanvas is null || _viewModel is null) return;
        foreach (var dot in ControllerCanvas.Children.OfType<Ellipse>())
        {
            var source = dot.Tag?.ToString();
            var selected = _viewModel.SelectedBinding?.SourceButton == source;
            var pressed = source is not null && _viewModel.Controller.PressedButtons.Contains(source);
            dot.Fill = new SolidColorBrush(selected ? Color.FromRgb(242, 75, 90) : pressed ? Color.FromRgb(97, 216, 133) : Color.FromRgb(250, 250, 252));
            dot.Stroke = new SolidColorBrush(selected ? Color.FromRgb(255, 206, 211) : pressed ? Color.FromRgb(184, 255, 202) : Color.FromRgb(12, 13, 15));
            dot.Opacity = selected || pressed ? 1 : .88;
        }

        foreach (var line in ControllerCanvas.Children.OfType<System.Windows.Shapes.Path>())
        {
            var selected = _viewModel.SelectedBinding?.SourceButton == line.Tag?.ToString();
            var pressed = line.Tag is string source && _viewModel.Controller.PressedButtons.Contains(source);
            line.Stroke = selected ? (Brush)FindResource("Accent") : pressed ? (Brush)FindResource("Positive") : new SolidColorBrush(Color.FromArgb(188, 247, 248, 250));
            line.StrokeThickness = selected ? 2.4 : pressed ? 2.0 : 1.5;
        }

        foreach (var label in ControllerCanvas.Children.OfType<TextBlock>())
        {
            var selected = _viewModel.SelectedBinding?.SourceButton == label.Tag?.ToString();
            label.Foreground = selected ? (Brush)FindResource("Accent") : Brushes.White;
        }
    }

    private void CaptureControllerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_capturingControllerButton)
        {
            CancelButtonCapture();
            return;
        }

        if (!_viewModel.Controller.IsConnected)
        {
            CaptureStatusLabel.Text = "Conecta el mando para detectar sus botones.";
            CaptureStatusLabel.Foreground = (Brush)FindResource("Accent");
            return;
        }

        _previousPressedButtons = new HashSet<string>(_viewModel.Controller.PressedButtons, StringComparer.OrdinalIgnoreCase);
        _captureDeadline = DateTime.UtcNow.AddSeconds(12);
        _capturingControllerButton = true;
        CaptureButton.Content = "Cancelar captura";
        CaptureStatusLabel.Text = "Pulsa ahora el botón físico que quieres configurar…";
        CaptureStatusLabel.Foreground = (Brush)FindResource("Accent");
    }

    private void ReadCapturedButton()
    {
        if (!_capturingControllerButton) return;
        if (DateTime.UtcNow >= _captureDeadline)
        {
            CancelButtonCapture("Tiempo agotado. Inténtalo de nuevo.");
            return;
        }

        var pressed = _viewModel.Controller.PressedButtons;
        var newlyPressed = pressed.FirstOrDefault(button => !_previousPressedButtons.Contains(button));
        _previousPressedButtons = new HashSet<string>(pressed, StringComparer.OrdinalIgnoreCase);
        if (newlyPressed is null) return;

        CancelButtonCapture($"Detectado: {newlyPressed}. Ahora elige su acción y destino.");
        var binding = _viewModel.Bindings.FirstOrDefault(item => string.Equals(item.SourceButton, newlyPressed, StringComparison.OrdinalIgnoreCase));
        if (binding is null)
        {
            CaptureStatusLabel.Text = $"Se detectó {newlyPressed}, pero no existe en este perfil.";
            CaptureStatusLabel.Foreground = (Brush)FindResource("Accent");
            return;
        }
        _viewModel.SelectedBinding = binding;
        AssignmentBindingsList.ScrollIntoView(binding);
        SelectedActionBox.Focus();
        _ = SaveCapturedSelectionAsync(newlyPressed);
    }

    private async Task SaveCapturedSelectionAsync(string buttonName)
    {
        await _viewModel.SaveAsync();
        var saved = _viewModel.SaveLabel == "Guardado automáticamente";
        CaptureStatusLabel.Text = saved
            ? $"{buttonName} seleccionado. Perfil guardado; elige una acción y pulsa Guardar asignación."
            : "No se pudo guardar el perfil. Revisa los permisos de escritura.";
        CaptureStatusLabel.Foreground = (Brush)FindResource(saved ? "Positive" : "Accent");
    }

    private async void SaveAssignment_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
        var saved = _viewModel.SaveLabel == "Guardado automáticamente";
        CaptureStatusLabel.Text = saved ? $"Asignación de {_viewModel.SelectedButtonName} guardada en el perfil." : "No se pudo guardar el perfil.";
        CaptureStatusLabel.Foreground = (Brush)FindResource(saved ? "Positive" : "Accent");
    }

    private void CancelButtonCapture(string? status = null)
    {
        _capturingControllerButton = false;
        if (CaptureButton is not null) CaptureButton.Content = "Detectar con el mando";
        if (status is not null && CaptureStatusLabel is not null)
        {
            CaptureStatusLabel.Text = status;
            CaptureStatusLabel.Foreground = (Brush)FindResource("TextSecondary");
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        if (e.ClickCount == 2) { ToggleMaximize(); return; }
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
