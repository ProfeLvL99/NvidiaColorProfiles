using NvidiaColorProfileManager.Models;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Interfaz para el repositorio de perfiles de color.
/// </summary>
public interface IProfileRepository
{
    /// <summary>Obtiene todos los perfiles guardados.</summary>
    List<ColorProfile> GetAll();

    /// <summary>Obtiene un perfil por su ID.</summary>
    ColorProfile? GetById(int id);

    /// <summary>Guarda un nuevo perfil.</summary>
    void Add(ColorProfile profile);

    /// <summary>Actualiza un perfil existente.</summary>
    void Update(ColorProfile profile);

    /// <summary>Elimina un perfil por su ID.</summary>
    void Delete(int id);

    /// <summary>Importa perfiles desde archivo JSON.</summary>
    List<ColorProfile> ImportFromJson(string filePath);

    /// <summary>Exporta un perfil a archivo JSON.</summary>
    void ExportToJson(ColorProfile profile, string filePath);
}
