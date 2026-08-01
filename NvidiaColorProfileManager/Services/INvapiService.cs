using NvidiaColorProfileManager.Models;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Interfaz para el servicio de interacción con NVIDIA NVAPI.
/// </summary>
public interface INvapiService
{
    /// <summary>Inicializa la NVAPI.</summary>
    bool Initialize();

    /// <summary>Obtiene los displays NVIDIA conectados.</summary>
    List<DisplayInfo> GetDisplays();

    /// <summary>Lee los settings de color actuales de un display.</summary>
    ColorSettings? ReadCurrentSettings(int displayId);

    /// <summary>Aplica settings de color a un display específico.</summary>
    void ApplySettings(int displayId, ColorSettings settings);

    /// <summary>Lee el Digital Vibrance actual de un display.</summary>
    int GetDigitalVibrance(int displayId);

    /// <summary>Establece el Digital Vibrance de un display.</summary>
    void SetDigitalVibrance(int displayId, int level);

    /// <summary>Indica si NVAPI está disponible y funcional.</summary>
    bool IsAvailable { get; }
}
