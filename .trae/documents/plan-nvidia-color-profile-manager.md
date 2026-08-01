# Plan: NVIDIA Color Profile Manager para Windows 11

## Resumen

Aplicación nativa WPF (.NET 9) para Windows 11 que gestiona, guarda y aplica perfiles de color usando la NVIDIA NVAPI. La app permite leer/escribir Brillo, Contraste, Gamma, Saturación (Digital Vibrance) y Tono (Hue), con soporte multimonitor, system tray, y UI Fluent Design.

***

## 1. Análisis de Viabilidad de NvAPIWrapper

### Librerías disponibles

| Librería                        | NuGet                           | Estado                                  | Target                 |
| ------------------------------- | ------------------------------- | --------------------------------------- | ---------------------- |
| **falahati/NvAPIWrapper**       | `NvAPIWrapper.Net`              | Maduro, última actualización 2021       | .NET Standard 2.0      |
| **Varun.NvAPIWrapper.Net**      | `Varun.NvAPIWrapper.Net` v9.0.3 | Fork modernizado con Try\* methods      | net6.0                 |
| **terrymacdonald/NVAPIWrapper** | No en NuGet                     | En progreso (2026), bindings ClangSharp | net9.0 (código fuente) |

### Recomendación

Usar **Varun.NvAPIWrapper.Net** porque:

* Fork activo y modernizado del original de falahati

* Target net6.0 compatible con .NET 9

* Soporta GPUs modernas (GDDR6X/GDDR7, PCIe 5, RTX 40/50 series)

* Agrega métodos `Try*` seguros que no lanzan excepciones en hardware no soportado

* Incluye las mismas APIs de Display Control que el original

### Capacidades confirmadas (Display Control)

| Función                           | API NVAPI                                 | Disponible                          | Rango              |
| --------------------------------- | ----------------------------------------- | ----------------------------------- | ------------------ |
| **Digital Vibrance (Saturación)** | `NvAPI_SetDVCLevel` / `NvAPI_GetDVCLevel` | Si                                  | 0-100%             |
| **Brillo**                        | `NvAPI_Disp_ColorControl` (Brightness)    | Si                                  | 0-100%             |
| **Contraste**                     | `NvAPI_Disp_ColorControl` (Contrast)      | Si                                  | 0-100%             |
| **Gamma**                         | `NvAPI_Disp_ColorControl` (Gamma)         | Si, pero limitado                   | Depende del driver |
| **Tono (Hue)**                    | `NvAPI_Disp_ColorControl` (Hue)           | Si                                  | 0-360°             |
| **Temperatura de Color**          | No directamente vía NVAPI                 | Solo vía ajuste RGB en ColorControl | Parcial            |

### Limitaciones conocidas

1. **Solo GPUs NVIDIA** - Si no hay GPU NVIDIA o driver instalado, la app no puede usar NVAPI. Se puede implementar fallback con Windows GDI (`SetDeviceGammaRamp`) para gamma/brillo/contraste básico.
2. **Temperatura de Color** - NVAPI no expone temperatura de color en Kelvin directamente. Se puede lograr manipulando los canales RGB vía `ColorControl`, pero no es lo mismo que el ajuste de temperatura del panel de control.
3. **Gamma vía NVAPI** - El control de gamma por `ColorControl` solo funciona si el driver lo soporta en modo "NVIDIA controlled". Algunas configuraciones delegan el gamma a la aplicación. Se recomienda complementar con Windows GDI (`SetDeviceGammaRamp`) como fallback.
4. **Driver dependency** - Requiere NVIDIA driver R410+. La versión moderna (Varun) soporta R580+.
5. **Sin soporte para GSync, DSR, o configuración 3D** - Solo Display Control.

***

## 2. Arquitectura del Proyecto

### Stack tecnológico

| Capa              | Tecnología                                                          |
| ----------------- | ------------------------------------------------------------------- |
| **Runtime**       | .NET 9.0 (último estable a agosto 2026)                             |
| **UI**            | WPF con `iNKORE.UI.WPF.Modern` (Fluent 2 Design)                    |
| **GPU**           | `Varun.NvAPIWrapper.Net` v9.x                                       |
| **MVVM Toolkit**  | `CommunityToolkit.Mvvm`                                             |
| **Persistencia**  | `Microsoft.Data.Sqlite` + serialización JSON con `System.Text.Json` |
| **System Tray**   | `H.NotifyIcon` (o `System.Windows.Forms.NotifyIcon` integrado)      |
| **DI**            | `Microsoft.Extensions.DependencyInjection`                          |
| **Configuración** | `Microsoft.Extensions.Configuration` (appsettings.json)             |

### Patrón: MVVM (Model-View-ViewModel)

```
NvidiaColorProfileManager/
├── NvidiaColorProfileManager.csproj
├── App.xaml / App.xaml.cs
├── appsettings.json
│
├── Models/
│   ├── ColorProfile.cs              # Entidad de perfil de color
│   ├── DisplayInfo.cs               # Info de un monitor conectado
│   ├── ColorSettings.cs             # Valores: Brightness, Contrast, Gamma, Vibrance, Hue
│   └── ApplicationState.cs          # Estado global (perfil activo, monitores)
│
├── ViewModels/
│   ├── MainViewModel.cs             # VM principal (lista perfiles, monitores)
│   ├── ProfileEditorViewModel.cs    # VM para crear/editar perfil
│   ├── TrayIconViewModel.cs         # VM para menú de system tray
│   └── SettingsViewModel.cs         # Configuración general de la app
│
├── Views/
│   ├── MainWindow.xaml/.cs          # Ventana principal
│   ├── ProfileEditorWindow.xaml/.cs # Editor de perfil
│   ├── TrayIconResources.xaml       # Resources para el tray icon
│   └── Controls/
│       ├── ColorSlider.xaml/.cs     # Control reutilizable (slider + label)
│       └── MonitorSelector.xaml/.cs # Selector de monitor destino
│
├── Services/
│   ├── INvapiService.cs             # Interfaz del servicio GPU
│   ├── NvapiService.cs              # Implementación: init NVAPI, leer/escribir DVC/ColorControl
│   ├── IProfileRepository.cs        # Interfaz de persistencia
│   ├── ProfileRepository.cs         # SQLite + JSON fallback
│   ├── IMonitorDetectionService.cs  # Interfaz detección monitores
│   ├── MonitorDetectionService.cs   # Detección y enumeración de displays NVIDIA
│   ├── IProfileApplierService.cs    # Interfaz aplicación de perfiles
│   ├── ProfileApplierService.cs     # Lógica de aplicar perfil a monitor(es)
│   └── WindowsGammaService.cs       # Fallback gamma/brillo via GDI
│
├── Converters/
│   ├── BoolToVisibilityConverter.cs
│   └── PercentageToSliderValueConverter.cs
│
├── Helpers/
│   └── NvapiErrorHandler.cs         # Manejo de errores NVAPI
│
└── Resources/
    ├── Icons/
    └── Styles/
```

### Flujo de dependencias

```
View ──(binding)──> ViewModel ──> Services ──> NvAPIWrapper/GDI
                       │
                       └──> Repositories ──> SQLite/JSON
```

***

## 3. Comandos para Inicializar el Proyecto

### Requisitos previos

* Windows 11 con GPU NVIDIA + drivers instalados

* Visual Studio 2022 (17.8+) con workload ".NET Desktop Development"

* .NET 9.0 SDK

### Paso a paso en PowerShell

```powershell
# 1. Verificar SDK instalado
dotnet --version  # debe mostrar 9.0.x

# 2. Crear solución y proyecto WPF
cd "f:\_Programas\NvidiaPerfilesDeColor"
dotnet new wpf -n NvidiaColorProfileManager --framework net9.0-windows
dotnet new sln -n NvidiaColorProfileManager
dotnet sln add .\NvidiaColorProfileManager\NvidiaColorProfileManager.csproj

# 3. Agregar paquetes NuGet
cd NvidiaColorProfileManager

# NVAPI wrapper modernizado
dotnet add package Varun.NvAPIWrapper.Net --version 9.0.3

# UI Fluent Design
dotnet add package iNKORE.UI.WPF.Modern

# MVVM Toolkit
dotnet add package CommunityToolkit.Mvvm

# SQLite
dotnet add package Microsoft.Data.Sqlite

# DI
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.Json

# 4. Crear estructura de carpetas
mkdir Models, ViewModels, Views, Services, Converters, Helpers, Resources
mkdir Views\Controls
mkdir Resources\Icons, Resources\Styles

# 5. Compilar para verificar
dotnet build
```

***

## 4. Código PoC - Leer Digital Vibrance del Monitor Principal

```csharp
using NvAPIWrapper;
using NvAPIWrapper.Display;

namespace NvidiaColorProfileManager.PoC;

public static class DigitalVibranceReader
{
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
                Console.WriteLine($"  Conectado    : {display.IsConnected}");

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
```

### Notas del PoC

* `NVIDIA.Initialize()` debe llamarse una sola vez al iniciar la aplicación.

* `Display.GetDisplays()` devuelve los monitores conectados a GPUs NVIDIA (no todos los del sistema).

* `display.DigitalVibranceControl.CurrentLevel` lee el valor actual de saturación (0-100).

* Si falla, el mensaje indicará si es por falta de GPU NVIDIA o driver no compatible.

* Para lectura masiva de color (brillo, contraste, gamma, hue), se usa `DisplayApi.ColorControl()` con las estructuras `ColorDataColorControl`, `ColorDataGamma`, etc.

***

## 5. Suposiciones y Decisiones

1. **Solo NVIDIA** - La app no dará soporte a GPUs AMD o Intel. Se mostrará un mensaje claro si no se detecta hardware NVIDIA.
2. **.NET 9** - Se usará la última versión estable de .NET en el momento de implementación (actualmente .NET 9).
3. **iNKORE.UI.WPF.Modern** - Elegido sobre ModernWpf por soporte de Fluent 2, Mica/Acrylic backdrops, y mejor mantenimiento en 2026.
4. **SQLite** como almacenamiento principal de perfiles, con export/import JSON para compartir perfiles.
5. **Temperatura de Color** - Se implementará como ajuste de balance RGB, no en Kelvin (limitación de NVAPI). Se documentará claramente en la UI.
6. **Gamma** - Se usará NVAPI como método primario con fallback automático a Windows GDI `SetDeviceGammaRamp`.

***

## 6. Verificación

1. Ejecutar el PoC en una máquina con GPU NVIDIA para confirmar que `DigitalVibranceControl` funciona.
2. `dotnet build` exitoso con todos los paquetes NuGet.
3. La estructura de carpetas debe coincidir con el diseño de arquitectura.
4. El PoC debe mostrar correctamente el nombre del monitor y el valor actual de Digital Vibrance.

