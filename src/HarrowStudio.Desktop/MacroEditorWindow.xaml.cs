using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HarrowStudio.Domain;

namespace HarrowStudio.Desktop;

public partial class MacroEditorWindow : Window
{
    private readonly ObservableCollection<MacroStep> _steps = [];
    public MacroDefinition? Result { get; private set; }

    public MacroEditorWindow()
    {
        InitializeComponent();
        ButtonBox.ItemsSource = new[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "D-pad ↑", "D-pad ↓", "D-pad ←", "D-pad →" };
        ButtonBox.SelectedIndex = 0;
        StepList.ItemsSource = _steps;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!e.Handled && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        DialogRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        if (DialogRoot.RenderTransform is ScaleTransform scale)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.985, 1, TimeSpan.FromMilliseconds(250)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.985, 1, TimeSpan.FromMilliseconds(250)) { EasingFunction = ease });
        }
    }

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DurationBox.Text, out var duration) || !int.TryParse(DelayBox.Text, out var delay) || duration < 1 || delay < 0)
        { ErrorLabel.Text = "Usa una duración > 0 y una espera ≥ 0."; return; }
        ErrorLabel.Text = string.Empty;
        _steps.Add(new MacroStep { Button = ButtonBox.SelectedItem?.ToString() ?? "A", DurationMs = Math.Min(duration, 10_000), DelayMs = Math.Min(delay, 10_000) });
        StepCount.Text = $"{_steps.Count} pasos";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text)) { ErrorLabel.Text = "Escribe un nombre para la macro."; return; }
        if (_steps.Count == 0) { ErrorLabel.Text = "Añade al menos un paso."; return; }
        Result = new MacroDefinition { Name = NameBox.Text.Trim(), Steps = _steps.ToList() };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
