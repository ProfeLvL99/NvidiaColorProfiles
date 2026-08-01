namespace NvidiaColorProfileManager.Helpers;

/// <summary>
/// Utilidades para manejo de errores de NVAPI.
/// </summary>
public static class NvapiErrorHandler
{
    /// <summary>
    /// Mensajes amigables para los errores más comunes de NVAPI.
    /// </summary>
    public static string GetFriendlyMessage(Exception ex)
    {
        return ex.Message switch
        {
            string msg when msg.Contains("NVIDIA driver") =>
                "Driver NVIDIA no encontrado. Instalá los drivers más recientes.",

            string msg when msg.Contains("NVAPI not initialized") =>
                "NVAPI no pudo inicializarse. Verificá que los drivers NVIDIA estén funcionando.",

            string msg when msg.Contains("NVIDIAApiException") =>
                "Error en la API de NVIDIA. Posiblemente el hardware no soporta esta operación.",

            _ => $"Error inesperado: {ex.Message}"
        };
    }
}
