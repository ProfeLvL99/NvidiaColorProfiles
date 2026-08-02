using System.IO;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Gestiona el inicio automático con Windows vía shortcut en la carpeta Startup.
/// </summary>
public class StartupService
{
    private readonly string _startupPath;
    private readonly string _shortcutName = "NVIDIA Color Profiles.lnk";

    public StartupService()
    {
        _startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
    }

    public bool IsStartupEnabled()
    {
        return File.Exists(Path.Combine(_startupPath, _shortcutName));
    }

    public void SetStartup(bool enable)
    {
        var shortcutPath = Path.Combine(_startupPath, _shortcutName);
        var exePath = Environment.ProcessPath;

        if (enable && exePath != null)
        {
            CreateShortcut(exePath, shortcutPath);
        }
        else
        {
            if (File.Exists(shortcutPath))
                File.Delete(shortcutPath);
        }
    }

    private void CreateShortcut(string targetPath, string shortcutPath)
    {
        // Usar WScript.Shell COM para crear el .lnk
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Arguments = "--minimized";
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath)!;
        shortcut.Save();
    }
}
