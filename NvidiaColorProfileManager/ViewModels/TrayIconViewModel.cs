using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvidiaColorProfileManager.Models;
using NvidiaColorProfileManager.Services;
using System.Collections.ObjectModel;

namespace NvidiaColorProfileManager.ViewModels;

/// <summary>
/// ViewModel para el menú del System Tray.
/// </summary>
public partial class TrayIconViewModel : ObservableObject
{
    private readonly IProfileRepository _profileRepository;
    private readonly IProfileApplierService _profileApplier;

    [ObservableProperty]
    private ObservableCollection<ColorProfile> _profiles = new();

    public TrayIconViewModel(IProfileRepository profileRepository, IProfileApplierService profileApplier)
    {
        _profileRepository = profileRepository;
        _profileApplier = profileApplier;
    }

    public void LoadProfiles()
    {
        Profiles = new ObservableCollection<ColorProfile>(_profileRepository.GetAll());
    }

    [RelayCommand]
    public void ApplyProfile(ColorProfile profile)
    {
        _profileApplier.ApplyProfile(profile);
    }

    [RelayCommand]
    public void RestoreDefaults()
    {
        _profileApplier.RestoreDefaults();
    }
}
