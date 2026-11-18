using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace HarrowStudio.Domain;

public sealed class ControllerProfile
{
    public string Name { get; set; } = "Perfil principal";
    public List<ButtonBinding> Bindings { get; set; } = [];
    public List<MacroDefinition> Macros { get; set; } = [];

    public static ControllerProfile CreateDefault() => new()
    {
        Bindings = [
            new("A", "A"), new("B", "B"), new("X", "X"), new("Y", "Y"),
            new("LB", "LB"), new("RB", "RB"), new("LT", "LT"), new("RT", "RT"),
            new("L3", "L3"), new("R3", "R3"), new("Back", "Back"), new("Start", "Start"), new("D-pad ↑", "D-pad ↑"),
            new("D-pad ↓", "D-pad ↓"), new("D-pad ←", "D-pad ←"), new("D-pad →", "D-pad →")
        ],
        Macros = [
            new() { Name = "Esquiva", Steps = [new() { Button = "B", DurationMs = 70, DelayMs = 40 }, new() { Button = "A", DurationMs = 70, DelayMs = 0 }] },
            new() { Name = "Recarga rápida", Steps = [new() { Button = "X", DurationMs = 90, DelayMs = 0 }] }
        ]
    };
}

public sealed class ButtonBinding(string source, string target) : INotifyPropertyChanged
{
    [JsonConstructor]
    public ButtonBinding() : this(string.Empty, string.Empty) { }

    private string _targetButton = target;
    public string SourceButton { get; set; } = source;
    public string DisplayName => SourceButton.StartsWith("D-pad", StringComparison.Ordinal) ? SourceButton : $"Botón {SourceButton}";
    public string Glyph => SourceButton switch
    {
        "D-pad ↑" => "↑", "D-pad ↓" => "↓", "D-pad ←" => "←", "D-pad →" => "→",
        "Back" => "B", "Start" => "S", "LT" => "L", "RT" => "R", "LB" => "L", "RB" => "R",
        "L3" => "L", "R3" => "R", _ => SourceButton
    };
    public string TargetButton
    {
        get => _targetButton;
        set { if (_targetButton == value) return; _targetButton = value; OnPropertyChanged(); }
    }
    private string _action = "Botón";
    public string Action
    {
        get => _action;
        set { if (_action == value) return; _action = value; OnPropertyChanged(); }
    }
    public string? MacroName { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class MacroDefinition
{
    public string Name { get; set; } = "Nueva macro";
    public List<MacroStep> Steps { get; set; } = [];
}

public sealed class MacroStep
{
    public string Button { get; set; } = "A";
    public int DurationMs { get; set; } = 80;
    public int DelayMs { get; set; } = 40;
}

public sealed class ControllerState
{
    public bool IsConnected { get; set; }
    public string InputMode { get; set; } = "—";
    public HashSet<string> PressedButtons { get; set; } = [];
    public double LeftX { get; set; }
    public double LeftY { get; set; }
    public double RightX { get; set; }
    public double RightY { get; set; }
    public double LeftTrigger { get; set; }
    public double RightTrigger { get; set; }
}
