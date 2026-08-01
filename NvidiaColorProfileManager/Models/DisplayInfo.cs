namespace NvidiaColorProfileManager.Models;

/// <summary>
/// Información de un monitor conectado a la GPU NVIDIA.
/// </summary>
public class DisplayInfo
{
    /// <summary>ID interno del display (handle de NVAPI).</summary>
    public int Id { get; set; }

    /// <summary>Nombre del monitor.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Indica si el monitor está actualmente conectado.</summary>
    public bool IsConnected { get; set; }

    /// <summary>Es el monitor principal del sistema.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Configuración de color actual leída del display.</summary>
    public ColorSettings? CurrentSettings { get; set; }

    public override string ToString() => Name;
}
