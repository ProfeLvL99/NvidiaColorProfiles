using CommunityToolkit.Mvvm.ComponentModel;

namespace NvidiaColorProfileManager.ViewModels;

/// <summary>
/// ViewModel para la configuración general de la aplicación.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private string _theme = "System"; // System, Light, Dark
}
