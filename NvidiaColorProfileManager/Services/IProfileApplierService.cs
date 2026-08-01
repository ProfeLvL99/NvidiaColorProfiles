using NvidiaColorProfileManager.Models;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Interfaz para el servicio de aplicación de perfiles.
/// </summary>
public interface IProfileApplierService
{
    /// <summary>Aplica un perfil a todos los displays o al display específico.</summary>
    void ApplyProfile(ColorProfile profile);

    /// <summary>Restaura los valores por defecto en todos los displays.</summary>
    void RestoreDefaults();
}
