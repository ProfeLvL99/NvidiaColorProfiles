using System.Collections.ObjectModel;

namespace NvidiaColorProfileManager.Models;

/// <summary>
/// Estado global de la aplicación.
/// </summary>
public class ApplicationState
{
    /// <summary>Lista de monitores NVIDIA detectados.</summary>
    public ObservableCollection<DisplayInfo> Displays { get; set; } = new();

    /// <summary>Lista de perfiles guardados.</summary>
    public ObservableCollection<ColorProfile> Profiles { get; set; } = new();

    /// <summary>Perfil actualmente activo (null si no hay ninguno aplicado).</summary>
    public ColorProfile? ActiveProfile { get; set; }

    /// <summary>Indica si NVAPI se inicializó correctamente.</summary>
    public bool IsNvapiAvailable { get; set; }

    /// <summary>Mensaje de error si NVAPI no está disponible.</summary>
    public string? NvapiErrorMessage { get; set; }
}
