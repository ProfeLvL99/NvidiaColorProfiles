namespace NvidiaColorProfileManager.Models;

/// <summary>
/// Representa un perfil de color guardado por el usuario.
/// </summary>
public class ColorProfile
{
    /// <summary>Identificador único del perfil.</summary>
    public int Id { get; set; }

    /// <summary>Nombre descriptivo del perfil.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Configuración de color asociada.</summary>
    public ColorSettings Settings { get; set; } = new();

    /// <summary>ID del monitor al que está asociado (0 = todos).</summary>
    public int DisplayId { get; set; }

    /// <summary>Nombre del monitor asociado (para mostrar en UI).</summary>
    public string DisplayName { get; set; } = "Todos los monitores";

    /// <summary>Fecha de creación.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>Fecha de última modificación.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>Atajo de teclado asignado (ej: "Control+Shift+F1"). Null si no tiene.</summary>
    public string? Hotkey { get; set; }
}
