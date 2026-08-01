using NvidiaColorProfileManager.Models;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Servicio para aplicar perfiles de color a los displays.
/// </summary>
public class ProfileApplierService : IProfileApplierService
{
    private readonly INvapiService _nvapiService;
    private readonly IMonitorDetectionService _monitorService;
    private readonly WindowsGammaService _gammaService;

    public ProfileApplierService(
        INvapiService nvapiService,
        IMonitorDetectionService monitorService,
        WindowsGammaService gammaService)
    {
        _nvapiService = nvapiService;
        _monitorService = monitorService;
        _gammaService = gammaService;
    }

    public void ApplyProfile(ColorProfile profile)
    {
        int targetId = profile.DisplayId;

        if (targetId < 0)
        {
            // Id < 0 = "Todos los monitores"
            _gammaService.ApplyToAllMonitors(
                profile.Settings.Gamma,
                profile.Settings.Brightness,
                profile.Settings.Contrast,
                profile.Settings.ColorTemperature);

            if (_nvapiService.IsAvailable)
            {
                var displays = _monitorService.DetectDisplays();
                for (int i = 0; i < displays.Count; i++)
                    _nvapiService.SetDigitalVibrance(i, profile.Settings.DigitalVibrance);
            }
        }
        else
        {
            // Id >= 0 = monitor específico
            int nvIndex = targetId;

            _gammaService.ApplyToMonitor(nvIndex,
                profile.Settings.Gamma,
                profile.Settings.Brightness,
                profile.Settings.Contrast,
                profile.Settings.ColorTemperature);

            if (_nvapiService.IsAvailable)
            {
                var displays = _monitorService.DetectDisplays();
                if (nvIndex >= 0 && nvIndex < displays.Count)
                    _nvapiService.SetDigitalVibrance(nvIndex, profile.Settings.DigitalVibrance);
            }
        }
    }

    public void RestoreDefaults()
    {
        _gammaService.RestoreDefaults();

        if (_nvapiService.IsAvailable)
        {
            var displays = _monitorService.DetectDisplays();
            for (int i = 0; i < displays.Count; i++)
                _nvapiService.SetDigitalVibrance(i, 50);
        }
    }
}
