using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HarrowStudio.Application;
using HarrowStudio.Domain;

namespace HarrowStudio.Desktop;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ProfileService _profiles;
    private readonly IControllerInput _controllerInput;
    private ControllerProfile _profile = ControllerProfile.CreateDefault();
    private ButtonBinding? _selectedBinding;
    private ControllerState _controller = new();
    private string _saveLabel = "Guardado automáticamente";
    private string _selectedNavigation = "Panel";
    private bool _loading;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public MainViewModel(ProfileService profiles, IControllerInput controllerInput)
    {
        _profiles = profiles;
        _controllerInput = controllerInput;
        Bindings.CollectionChanged += (_, _) => { if (!_loading) _ = SaveAsync(); };
        Macros.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(MacroCount)); OnPropertyChanged(nameof(AssignmentOptions)); if (!_loading) _ = SaveAsync(); };
    }

    public ObservableCollection<ButtonBinding> Bindings { get; } = [];
    public ObservableCollection<MacroDefinition> Macros { get; } = [];
    public IReadOnlyList<string> ButtonOptions { get; } = ["A", "B", "X", "Y", "LB", "RB", "LT", "RT", "L3", "R3", "Back", "Start", "D-pad ↑", "D-pad ↓", "D-pad ←", "D-pad →", "Sin asignar"];
    public IReadOnlyList<string> ActionOptions { get; } = ["Botón", "Macro", "Turbo", "Desactivar"];
    public IReadOnlyList<string> AssignmentOptions => ButtonOptions.Concat(Macros.Select(x => x.Name)).Distinct().ToArray();
    public string ProfileName { get => _profile.Name; set { _profile.Name = value; OnPropertyChanged(); _ = SaveAsync(); } }
    public int MacroCount => Macros.Count;
    public ButtonBinding? SelectedBinding { get => _selectedBinding; set { _selectedBinding = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedButtonName)); } }
    public string SelectedButtonName => SelectedBinding?.SourceButton ?? "—";
    public ControllerState Controller { get => _controller; private set { _controller = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectionLabel)); OnPropertyChanged(nameof(ConnectionSubLabel)); OnPropertyChanged(nameof(ActiveButtonSummary)); OnPropertyChanged(nameof(LeftStickLabel)); OnPropertyChanged(nameof(RightStickLabel)); } }
    public string ConnectionLabel => Controller.IsConnected ? "Mando conectado" : "Esperando mando";
    public string ConnectionSubLabel => Controller.IsConnected ? $"Redragon Harrow Pro · {Controller.InputMode}" : "Conéctalo por USB o receptor 2.4 GHz";
    public string SaveLabel { get => _saveLabel; private set { _saveLabel = value; OnPropertyChanged(); } }
    public string SelectedNavigation { get => _selectedNavigation; set { _selectedNavigation = value; OnPropertyChanged(); } }
    public string ActiveButtonSummary => Controller.PressedButtons.Count == 0 ? "Pulsa un botón para verlo aquí" : string.Join("   ·   ", Controller.PressedButtons);
    public string LeftStickLabel => $"X {Controller.LeftX:+0.00;-0.00;0.00}   Y {Controller.LeftY:+0.00;-0.00;0.00}";
    public string RightStickLabel => $"X {Controller.RightX:+0.00;-0.00;0.00}   Y {Controller.RightY:+0.00;-0.00;0.00}";

    public async Task LoadAsync()
    {
        _loading = true;
        _profile = await _profiles.LoadAsync();
        Bindings.Clear();
        foreach (var binding in _profile.Bindings) { Bindings.Add(binding); binding.PropertyChanged += BindingChanged; }
        Macros.Clear();
        foreach (var macro in _profile.Macros) Macros.Add(macro);
        _loading = false;
        OnPropertyChanged(nameof(ProfileName)); OnPropertyChanged(nameof(MacroCount)); OnPropertyChanged(nameof(AssignmentOptions));
        SelectedBinding = Bindings.FirstOrDefault();
    }

    public void PollController()
    {
        try { Controller = _controllerInput.Read(); }
        catch (DllNotFoundException) { Controller = new ControllerState(); }
        catch (EntryPointNotFoundException) { Controller = new ControllerState(); }
    }

    public async Task AddMacroAsync(MacroDefinition macro) { Macros.Add(macro); await SaveAsync(); }
    public async Task DeleteMacroAsync(MacroDefinition macro) { Macros.Remove(macro); await SaveAsync(); }
    public async Task SaveAsync()
    {
        await _saveGate.WaitAsync();
        try
        {
            _profile.Bindings = Bindings.ToList(); _profile.Macros = Macros.ToList();
            await _profiles.SaveAsync(_profile);
            SaveLabel = "Guardado automáticamente";
        }
        catch (IOException) { SaveLabel = "No se pudo guardar el perfil"; }
        catch (UnauthorizedAccessException) { SaveLabel = "No se pudo guardar el perfil"; }
        finally { _saveGate.Release(); }
    }

    private void BindingChanged(object? sender, PropertyChangedEventArgs e) => _ = SaveAsync();
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));
}
