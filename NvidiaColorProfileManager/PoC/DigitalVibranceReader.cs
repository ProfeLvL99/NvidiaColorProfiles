using NvAPIWrapper;
using NvAPIWrapper.Display;

namespace NvidiaColorProfileManager.PoC;

/// <summary>
/// Prueba de concepto: Lee el Digital Vibrance de todos los monitores NVIDIA conectados.
/// Ejecutar desde Program.cs o como aplicación de consola independiente.
/// </summary>
public static class DigitalVibranceReader
{
    /// <summary>
    /// Ejecuta el PoC leyendo el Digital Vibrance de cada display NVIDIA.
    /// </summary>
    public static void Run()
    {
        try
        {
            // 1. Inicializar NVAPI
            NVIDIA.Initialize();
            Console.WriteLine("[OK] NVAPI inicializada correctamente.");

            // 2. Obtener todos los displays conectados a GPUs NVIDIA
            var displays = Display.GetDisplays();

            if (displays.Length == 0)
            {
                Console.WriteLine("[!] No se encontraron displays NVIDIA.");
                return;
            }

            Console.WriteLine($"\nDisplays NVIDIA detectados: {displays.Length}\n");

            // 3. Iterar y leer Digital Vibrance de cada display
            for (int i = 0; i < displays.Length; i++)
            {
                var display = displays[i];
                Console.WriteLine($"--- Display {i + 1} ---");
                Console.WriteLine($"  Nombre       : {display.Name}");

                try
                {
                    var dvc = display.DigitalVibranceControl;
                    Console.WriteLine($"  Vibrance Actual : {dvc.CurrentLevel}");
                    Console.WriteLine($"  Vibrance Default: {dvc.DefaultLevel}");
                    Console.WriteLine($"  Vibrance Mín/Máx: {dvc.MinimumLevel} / {dvc.MaximumLevel}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [!] Error al leer Digital Vibrance: {ex.Message}");
                }

                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] NVAPI falló: {ex.Message}");
            Console.WriteLine("Asegurate de tener GPU NVIDIA con drivers instalados.");
        }
    }
}
