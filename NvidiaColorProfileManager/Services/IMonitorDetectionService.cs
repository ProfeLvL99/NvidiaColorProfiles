using NvidiaColorProfileManager.Models;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Servicio para detección de monitores conectados a GPUs NVIDIA.
/// </summary>
public interface IMonitorDetectionService
{
    /// <summary>Detecta y devuelve todos los displays NVIDIA conectados.</summary>
    List<DisplayInfo> DetectDisplays();

    /// <summary>Indica si hay al menos un display NVIDIA disponible.</summary>
    bool HasDisplays { get; }
}
