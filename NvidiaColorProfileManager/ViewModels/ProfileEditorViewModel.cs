using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvidiaColorProfileManager.Models;
using NvidiaColorProfileManager.Services;
using System.Collections.ObjectModel;

namespace NvidiaColorProfileManager.ViewModels;

public partial class ProfileEditorViewModel : ObservableObject
{
    private readonly IProfileRepository _profileRepository;
    private readonly IMonitorDetectionService _monitorService;
    private readonly INvapiService _nvapiService;
    private readonly IProfileApplierService _profileApplier;
    private readonly WindowsGammaService _gammaService;

    private bool _isEditing;
    private int _editingProfileId;
    private ColorSettings? _originalSettings;
    private bool _suppressPreview;

    [ObservableProperty] private string _dialogTitle = "New Profile";
    [ObservableProperty] private string _profileName = string.Empty;
    [ObservableProperty] private ObservableCollection<DisplayInfo> _availableDisplays = new();
    [ObservableProperty] private DisplayInfo? _selectedTargetDisplay;
    [ObservableProperty] private int _brightness = 50;
    [ObservableProperty] private int _contrast = 50;
    [ObservableProperty] private double _gamma = 1.0;
    [ObservableProperty] private int _digitalVibrance = 50;
    [ObservableProperty] private int _colorTemperature = 0;
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string? _hotkey;
    [ObservableProperty] private string _hotkeyDisplay = "Click to assign shortcut...";

    public event Action<ColorProfile>? ProfileSaved;
    public event Action? RequestClose;

    public ProfileEditorViewModel(
        IProfileRepository profileRepository,
        IMonitorDetectionService monitorService,
        INvapiService nvapiService,
        IProfileApplierService profileApplier,
        WindowsGammaService gammaService)
    {
        _profileRepository = profileRepository;
        _monitorService = monitorService;
        _nvapiService = nvapiService;
        _profileApplier = profileApplier;
        _gammaService = gammaService;
    }

    partial void OnBrightnessChanged(int value) => ApplyLivePreview();
    partial void OnContrastChanged(int value) => ApplyLivePreview();
    partial void OnGammaChanged(double value) => ApplyLivePreview();
    partial void OnDigitalVibranceChanged(int value) => ApplyLivePreview();
    partial void OnColorTemperatureChanged(int value) => ApplyLivePreview();

    private void ApplyLivePreview()
    {
        if (_suppressPreview || !IsOpen) return;

        int targetId = SelectedTargetDisplay?.Id ?? -1;

        if (targetId < 0)
        {
            _gammaService.ApplyToAllMonitors(Gamma, Brightness, Contrast, ColorTemperature);
            if (_nvapiService.IsAvailable)
            {
                var displays = _monitorService.DetectDisplays();
                for (int i = 0; i < displays.Count; i++)
                    _nvapiService.SetDigitalVibrance(i, DigitalVibrance);
            }
        }
        else
        {
            int nvIndex = targetId;
            _gammaService.ApplyToMonitor(nvIndex, Gamma, Brightness, Contrast, ColorTemperature);
            if (_nvapiService.IsAvailable)
            {
                var displays = _monitorService.DetectDisplays();
                if (nvIndex >= 0 && nvIndex < displays.Count)
                    _nvapiService.SetDigitalVibrance(nvIndex, DigitalVibrance);
            }
        }
    }

    [RelayCommand]
    public void OpenForCreate()
    {
        _originalSettings = _nvapiService.IsAvailable
            ? _nvapiService.ReadCurrentSettings(0) : new ColorSettings();

        _isEditing = false;
        _editingProfileId = 0;
        DialogTitle = "New Profile";
        _suppressPreview = true;
        ResetToDefaults();
        _suppressPreview = false;
        Hotkey = null;
        HotkeyDisplay = "Click to assign shortcut...";
        LoadDisplays();
        IsOpen = true;
    }

    [RelayCommand]
    public void OpenForEdit(ColorProfile profile)
    {
        _originalSettings = _nvapiService.IsAvailable
            ? _nvapiService.ReadCurrentSettings(0) : new ColorSettings();

        _isEditing = true;
        _editingProfileId = profile.Id;
        DialogTitle = $"Edit: {profile.Name}";

        _suppressPreview = true;
        ProfileName = profile.Name;
        Brightness = profile.Settings.Brightness;
        Contrast = profile.Settings.Contrast;
        Gamma = profile.Settings.Gamma;
        DigitalVibrance = profile.Settings.DigitalVibrance;
        ColorTemperature = profile.Settings.ColorTemperature;
        _suppressPreview = false;

        Hotkey = profile.Hotkey;
        HotkeyDisplay = profile.Hotkey ?? "Click to assign shortcut...";

        LoadDisplays();

        var targetDisplay = AvailableDisplays.FirstOrDefault(d => d.Id == profile.DisplayId);
        SelectedTargetDisplay = targetDisplay ?? AvailableDisplays.FirstOrDefault(d => d.Id < 0);

        ApplyLivePreview();
        IsOpen = true;
    }

    [RelayCommand]
    public void ReadCurrentFromDisplay()
    {
        if (!_nvapiService.IsAvailable) return;
        int displayId = SelectedTargetDisplay?.Id ?? 0;
        if (displayId < 0) displayId = 0;
        var settings = _nvapiService.ReadCurrentSettings(displayId);

        if (settings != null)
        {
            _suppressPreview = true;
            Brightness = settings.Brightness;
            Contrast = settings.Contrast;
            Gamma = settings.Gamma;
            DigitalVibrance = settings.DigitalVibrance;
            ColorTemperature = settings.ColorTemperature;
            _suppressPreview = false;
            ApplyLivePreview();
        }
    }

    [RelayCommand]
    public void Save()
    {
        if (string.IsNullOrWhiteSpace(ProfileName)) return;

        var profile = new ColorProfile
        {
            Id = _editingProfileId,
            Name = ProfileName.Trim(),
            DisplayId = SelectedTargetDisplay?.Id ?? -1,
            DisplayName = SelectedTargetDisplay?.Name ?? "All monitors",
            Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? null : Hotkey.Trim(),
            Settings = new ColorSettings
            {
                Brightness = Brightness,
                Contrast = Contrast,
                Gamma = Gamma,
                DigitalVibrance = DigitalVibrance,
                ColorTemperature = ColorTemperature,
            }
        };

        if (_isEditing) _profileRepository.Update(profile);
        else _profileRepository.Add(profile);

        ProfileSaved?.Invoke(profile);
        IsOpen = false;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        if (_originalSettings != null)
        {
            int targetId = SelectedTargetDisplay?.Id ?? -1;
            if (targetId < 0)
            {
                _gammaService.ApplyToAllMonitors(
                    _originalSettings.Gamma, _originalSettings.Brightness,
                    _originalSettings.Contrast, _originalSettings.ColorTemperature);
                if (_nvapiService.IsAvailable)
                {
                    var displays = _monitorService.DetectDisplays();
                    for (int i = 0; i < displays.Count; i++)
                        _nvapiService.SetDigitalVibrance(i, _originalSettings.DigitalVibrance);
                }
            }
            else
            {
                int nvIndex = targetId;
                _gammaService.ApplyToMonitor(nvIndex,
                    _originalSettings.Gamma, _originalSettings.Brightness,
                    _originalSettings.Contrast, _originalSettings.ColorTemperature);
                if (_nvapiService.IsAvailable)
                {
                    var displays = _monitorService.DetectDisplays();
                    if (nvIndex >= 0 && nvIndex < displays.Count)
                        _nvapiService.SetDigitalVibrance(nvIndex, _originalSettings.DigitalVibrance);
                }
            }
        }

        IsOpen = false;
        RequestClose?.Invoke();
    }

    private void LoadDisplays()
    {
        var displays = _monitorService.DetectDisplays();
        AvailableDisplays = new ObservableCollection<DisplayInfo>(displays);
        AvailableDisplays.Insert(0, new DisplayInfo { Id = -1, Name = "All monitors", IsConnected = true });
    }

    private void ResetToDefaults()
    {
        ProfileName = string.Empty;
        Brightness = 50;
        Contrast = 50;
        Gamma = 1.0;
        DigitalVibrance = 50;
        ColorTemperature = 0;
        SelectedTargetDisplay = null;
    }

    public void SetHotkeyFromKeyEvent(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
    {
        var formatted = HotkeyService.FormatHotkeyFromKeyEvent(key, modifiers);
        if (formatted != null) { Hotkey = formatted; HotkeyDisplay = formatted; }
    }
}
