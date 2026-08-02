using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NvidiaColorProfileManager.Services;
using NvidiaColorProfileManager.ViewModels;

namespace NvidiaColorProfileManager;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static bool StartMinimized { get; private set; }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Check if launched with --minimized (e.g., from Windows startup)
        StartMinimized = e.Args.Contains("--minimized");

        // Single instance: si ya hay otra instancia, traerla al frente y salir
        var currentProcess = Process.GetCurrentProcess();
        var existingProcess = Process.GetProcessesByName(currentProcess.ProcessName)
            .FirstOrDefault(p => p.Id != currentProcess.Id);

        if (existingProcess != null)
        {
            var hwnd = existingProcess.MainWindowHandle;
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
            Shutdown();
            return;
        }
        // Configurar DI
        var services = new ServiceCollection();

        // Servicios
        services.AddSingleton<INvapiService, NvapiService>();
        services.AddSingleton<IMonitorDetectionService, MonitorDetectionService>();
        services.AddSingleton<IProfileApplierService, ProfileApplierService>();
        services.AddSingleton<WindowsGammaService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<StartupService>();

        // Repositorio (SQLite en %AppData%)
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NvidiaColorProfileManager");
        Directory.CreateDirectory(appDataPath);
        string dbPath = Path.Combine(appDataPath, "profiles.db");
        services.AddSingleton<IProfileRepository>(new ProfileRepository(dbPath));

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ProfileEditorViewModel>();
        services.AddSingleton<TrayIconViewModel>();
        services.AddSingleton<SettingsViewModel>();

        Services = services.BuildServiceProvider();

        // Iniciar ventana principal
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
