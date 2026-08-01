using NvAPIWrapper;
using NvAPIWrapper.Display;
using NvidiaColorProfileManager.Models;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Implementación del servicio NVAPI usando Varun.NvAPIWrapper.Net.
/// </summary>
public class NvapiService : INvapiService
{
    private Display[]? _displays;
    private bool _initialized;

    public bool IsAvailable => _initialized;

    public bool Initialize()
    {
        try
        {
            NVIDIA.Initialize();
            _displays = Display.GetDisplays();
            _initialized = _displays.Length > 0;
            return _initialized;
        }
        catch (Exception)
        {
            _initialized = false;
            return false;
        }
    }

    public List<DisplayInfo> GetDisplays()
    {
        if (!_initialized || _displays == null)
            return new List<DisplayInfo>();

        try
        {
            // Re-fetch displays to get latest state
            _displays = Display.GetDisplays();
        }
        catch
        {
            // Use cached displays if re-fetch fails
        }

        return _displays.Select((d, i) => new DisplayInfo
        {
            Id = i,
            Name = CleanDisplayName(d.Name, i),
            IsConnected = true,
            IsPrimary = i == 0
        }).ToList();
    }

    private static string CleanDisplayName(string rawName, int index)
    {
        // Limpiar nombres técnicos como "\\.\DISPLAY1"
        if (rawName.StartsWith(@"\\.\"))
            return $"Monitor {index + 1} ({rawName[4..]})";

        // Si es muy corto o genérico
        if (rawName.Length < 5 || rawName.Equals("Display", StringComparison.OrdinalIgnoreCase))
            return $"Monitor {index + 1}";

        return rawName;
    }

    public ColorSettings? ReadCurrentSettings(int displayId)
    {
        if (!_initialized || _displays == null || displayId >= _displays.Length)
            return null;

        var display = _displays[displayId];
        var settings = new ColorSettings();

        try
        {
            // Digital Vibrance
            var dvc = display.DigitalVibranceControl;
            settings.DigitalVibrance = dvc.CurrentLevel;
        }
        catch { /* mantener default */ }

        // Brightness, Contrast, Hue requieren la API nativa ColorControl
        // que puede variar entre versiones. Se leerán en futuras iteraciones.
        // Por ahora, mantenemos defaults.

        return settings;
    }

    public void ApplySettings(int displayId, ColorSettings settings)
    {
        if (!_initialized || _displays == null || displayId >= _displays.Length)
            return;

        var display = _displays[displayId];

        // Digital Vibrance
        try
        {
            var dvc = display.DigitalVibranceControl;
            dvc.CurrentLevel = Math.Clamp(settings.DigitalVibrance, 0, 100);
        }
        catch { }

        // Brillo, Contraste, Hue: requieren la API nativa ColorControl.
        // Se implementarán cuando la API nativa esté confirmada.
    }

    public int GetDigitalVibrance(int displayId)
    {
        if (!_initialized || _displays == null || displayId >= _displays.Length)
            return 50;

        try
        {
            return _displays[displayId].DigitalVibranceControl.CurrentLevel;
        }
        catch
        {
            return 50;
        }
    }

    public void SetDigitalVibrance(int displayId, int level)
    {
        if (!_initialized || _displays == null || displayId >= _displays.Length)
            return;

        try
        {
            var dvc = _displays[displayId].DigitalVibranceControl;
            dvc.CurrentLevel = Math.Clamp(level, 0, 100);
        }
        catch { }
    }
}
