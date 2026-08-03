using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvidiaColorProfileManager.Models;
using NvidiaColorProfileManager.Services;
using System.Collections.ObjectModel;

namespace NvidiaColorProfileManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INvapiService _nvapiService;
    private readonly IMonitorDetectionService _monitorService;
    private readonly IProfileRepository _profileRepository;
    private readonly IProfileApplierService _profileApplier;
    private readonly WindowsGammaService _gammaService;
    private readonly StartupService _startupService;

    private bool _isEditing;
    private int _editingProfileId;
    private ColorSettings? _originalSettings;
    private bool _suppressPreview;

    // ── App State ──
    [ObservableProperty] private ObservableCollection<ColorProfile> _profiles = new();
    [ObservableProperty] private ObservableCollection<DisplayInfo> _displays = new();
    [ObservableProperty] private ColorProfile? _selectedProfile;
    [ObservableProperty] private bool _isNvapiAvailable;
    [ObservableProperty] private string _statusMessage = "Ready.";
    [ObservableProperty] private string _nvapiStatusText = "Checking NVAPI...";
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _editorTitle = "Select or create a profile";

    // ── Editor Fields ──
    [ObservableProperty] private string _profileName = string.Empty;
    [ObservableProperty] private ObservableCollection<DisplayInfo> _availableDisplays = new();
    [ObservableProperty] private DisplayInfo? _selectedTargetDisplay;
    [ObservableProperty] private int _brightness = 50;
    [ObservableProperty] private int _contrast = 50;
    [ObservableProperty] private double _gamma = 1.0;
    [ObservableProperty] private int _digitalVibrance = 50;
    [ObservableProperty] private int _colorTemperature = 0;
    [ObservableProperty] private string? _hotkey;
    [ObservableProperty] private string _hotkeyDisplay = "Click to assign shortcut...";
    [ObservableProperty] private bool _hasChanges;

    public event Action? ProfilesChanged;

    // ── Live Preview ──
    partial void OnBrightnessChanged(int value) => ApplyLivePreview();
    partial void OnContrastChanged(int value) => ApplyLivePreview();
    partial void OnGammaChanged(double value) => ApplyLivePreview();
    partial void OnDigitalVibranceChanged(int value) => ApplyLivePreview();
    partial void OnColorTemperatureChanged(int value) => ApplyLivePreview();

    partial void OnSelectedProfileChanged(ColorProfile? value)
    {
        if (value != null) LoadProfileIntoEditor(value);
    }

    public MainViewModel(
        INvapiService nvapiService,
        IMonitorDetectionService monitorService,
        IProfileRepository profileRepository,
        IProfileApplierService profileApplier,
        WindowsGammaService gammaService,
        StartupService startupService)
    {
        _nvapiService = nvapiService;
        _monitorService = monitorService;
        _profileRepository = profileRepository;
        _profileApplier = profileApplier;
        _gammaService = gammaService;
        _startupService = startupService;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _startupService.SetStartup(value);
        StatusMessage = value ? "Start with Windows enabled." : "Start with Windows disabled.";
    }

    private void ApplyLivePreview()
    {
        if (_suppressPreview || !IsNvapiAvailable) return;
        try
        {
            HasChanges = true;

            int targetId = SelectedTargetDisplay?.Id ?? -1;
            if (targetId < 0)
            {
                _gammaService.ApplyToAllMonitors(Gamma, Brightness, Contrast, ColorTemperature);
                if (_nvapiService.IsAvailable)
                    foreach (var d in Displays) _nvapiService.SetDigitalVibrance(d.Id, DigitalVibrance);
            }
            else
            {
                _gammaService.ApplyToMonitor(targetId, Gamma, Brightness, Contrast, ColorTemperature);
                if (_nvapiService.IsAvailable && targetId < Displays.Count)
                    _nvapiService.SetDigitalVibrance(targetId, DigitalVibrance);
            }

            if (!_gammaService.LastApplySucceeded)
                StatusMessage = $"Warning: gamma ramp rejected by driver (code {_gammaService.LastErrorCode}). Try less extreme values.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Preview error: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Initialize()
    {
        StartWithWindows = _startupService.IsStartupEnabled();
        IsNvapiAvailable = _nvapiService.Initialize();
        NvapiStatusText = IsNvapiAvailable ? "NVAPI: Connected" : "NVAPI: Not available";

        if (IsNvapiAvailable)
        {
            Displays = new ObservableCollection<DisplayInfo>(_monitorService.DetectDisplays());
            StatusMessage = $"{Displays.Count} NVIDIA monitor(s) detected.";
        }

        LoadProfiles();
        LoadDisplaysForEditor();
    }

    private void LoadProfiles()
    {
        Profiles = new ObservableCollection<ColorProfile>(_profileRepository.GetAll());
    }

    private void LoadDisplaysForEditor()
    {
        var list = _monitorService.DetectDisplays();
        AvailableDisplays = new ObservableCollection<DisplayInfo>(list);
        AvailableDisplays.Insert(0, new DisplayInfo { Id = -1, Name = "All monitors" });
    }

    // ── Editor ──
    [RelayCommand]
    public void NewProfile()
    {
        SaveOriginalState();
        _isEditing = false;
        _editingProfileId = 0;
        EditorTitle = "New Profile";
        _suppressPreview = true;
        ProfileName = string.Empty;
        Brightness = 50; Contrast = 50; Gamma = 1.0; DigitalVibrance = 50; ColorTemperature = 0;
        SelectedTargetDisplay = null;
        Hotkey = null;
        HotkeyDisplay = "Click to assign shortcut...";
        _suppressPreview = false;
        HasChanges = false;
        SelectedProfile = null;
    }

    private void LoadProfileIntoEditor(ColorProfile profile)
    {
        SaveOriginalState();
        _isEditing = true;
        _editingProfileId = profile.Id;
        EditorTitle = $"Editing: {profile.Name}";

        _suppressPreview = true;
        ProfileName = profile.Name;
        Brightness = profile.Settings.Brightness;
        Contrast = profile.Settings.Contrast;
        Gamma = profile.Settings.Gamma;
        DigitalVibrance = profile.Settings.DigitalVibrance;
        ColorTemperature = profile.Settings.ColorTemperature;
        Hotkey = profile.Hotkey;
        HotkeyDisplay = profile.Hotkey ?? "Click to assign shortcut...";
        _suppressPreview = false;

        var target = AvailableDisplays.FirstOrDefault(d => d.Id == profile.DisplayId);
        SelectedTargetDisplay = target ?? AvailableDisplays.FirstOrDefault(d => d.Id < 0);
        HasChanges = false;

        ApplyLivePreview();
    }

    private void SaveOriginalState()
    {
        if (IsNvapiAvailable)
            _originalSettings = _nvapiService.ReadCurrentSettings(0);
        else
            _originalSettings = new ColorSettings();
    }

    [RelayCommand]
    public void ReadFromMonitor()
    {
        if (!IsNvapiAvailable) return;
        int id = SelectedTargetDisplay?.Id ?? 0;
        if (id < 0) id = 0;
        var s = _nvapiService.ReadCurrentSettings(id);
        if (s == null) return;

        _suppressPreview = true;
        Brightness = s.Brightness; Contrast = s.Contrast; Gamma = s.Gamma;
        DigitalVibrance = s.DigitalVibrance; ColorTemperature = s.ColorTemperature;
        _suppressPreview = false;
        ApplyLivePreview();
    }

    [RelayCommand]
    public void SaveProfile()
    {
        if (string.IsNullOrWhiteSpace(ProfileName)) return;

        try
        {
            var profile = new ColorProfile
            {
                Id = _editingProfileId,
                Name = ProfileName.Trim(),
                DisplayId = SelectedTargetDisplay?.Id ?? -1,
                DisplayName = SelectedTargetDisplay?.Name ?? "All monitors",
                Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? null : Hotkey.Trim(),
                Settings = new ColorSettings
                {
                    Brightness = Brightness, Contrast = Contrast, Gamma = Gamma,
                    DigitalVibrance = DigitalVibrance, ColorTemperature = ColorTemperature,
                }
            };

            if (_isEditing) _profileRepository.Update(profile);
            else _profileRepository.Add(profile);

            _isEditing = true;
            _editingProfileId = profile.Id;
            HasChanges = false;
            EditorTitle = $"Editing: {profile.Name}";
            StatusMessage = "Profile saved successfully.";

            LoadProfiles();
            ProfilesChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand]
    public void CancelEdit()
    {
        if (_originalSettings != null)
        {
            _suppressPreview = true;
            Brightness = _originalSettings.Brightness;
            Contrast = _originalSettings.Contrast;
            Gamma = _originalSettings.Gamma;
            DigitalVibrance = _originalSettings.DigitalVibrance;
            ColorTemperature = _originalSettings.ColorTemperature;
            _suppressPreview = false;

            ApplyLivePreview();
        }
        HasChanges = false;
        NewProfileCommand.Execute(null);
    }

    // ── Perfiles ──
    [RelayCommand]
    public void ApplyProfile(ColorProfile? profile)
    {
        if (profile == null) return;
        _profileApplier.ApplyProfile(profile);
        SelectedProfile = profile;
        LoadProfileIntoEditor(profile);
        StatusMessage = $"Profile '{profile.Name}' applied.";
    }

    [RelayCommand]
    public void DeleteProfile(ColorProfile? profile)
    {
        if (profile == null) return;
        var name = profile.Name;
        _profileRepository.Delete(profile.Id);
        Profiles.Remove(profile);
        if (SelectedProfile?.Id == profile.Id) NewProfileCommand.Execute(null);
        StatusMessage = $"Profile '{name}' deleted.";
        ProfilesChanged?.Invoke();
    }

    [RelayCommand]
    public void RestoreDefaults()
    {
        _profileApplier.RestoreDefaults();
        NewProfileCommand.Execute(null);
        StatusMessage = "Default values restored.";
    }

    [RelayCommand]
    public void RefreshDisplays()
    {
        if (!IsNvapiAvailable) return;
        Displays = new ObservableCollection<DisplayInfo>(_monitorService.DetectDisplays());
        LoadDisplaysForEditor();
        StatusMessage = $"{Displays.Count} NVIDIA monitor(s) detected.";
    }

    public void SetHotkey(string formatted)
    {
        Hotkey = formatted;
        HotkeyDisplay = formatted;
    }

    public void ClearHotkey()
    {
        Hotkey = null;
        HotkeyDisplay = "Click to assign shortcut...";
    }
}
