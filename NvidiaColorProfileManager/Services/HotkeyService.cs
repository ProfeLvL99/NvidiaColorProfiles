using System.Runtime.InteropServices;
using System.Windows.Input;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Servicio para registrar atajos de teclado globales usando RegisterHotKey de Win32.
/// </summary>
public class HotkeyService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;

    // Modificadores
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    private IntPtr _windowHandle;
    private readonly Dictionary<int, int> _profileHotkeys = new(); // hotkeyId -> profileId
    private int _nextHotkeyId = 1;
    private bool _disposed;

    /// <summary>Evento disparado cuando se presiona un hotkey registrado.</summary>
    public event Action<int>? HotkeyPressed; // profileId

    /// <summary>
    /// Inicializa el servicio con el handle de la ventana principal.
    /// </summary>
    public void Initialize(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// Registra un hotkey para un perfil.
    /// </summary>
    /// <param name="profileId">ID del perfil.</param>
    /// <param name="hotkey">String como "Control+Shift+F1" o "Alt+N". Null o vacío para desregistrar.</param>
    public void RegisterProfileHotkey(int profileId, string? hotkey)
    {
        // Primero desregistrar hotkeys existentes para este perfil
        UnregisterProfileHotkeys(profileId);

        if (string.IsNullOrWhiteSpace(hotkey) || _windowHandle == IntPtr.Zero)
            return;

        if (!TryParseHotkey(hotkey, out uint modifiers, out uint key))
            return;

        int hotkeyId = _nextHotkeyId++;
        if (RegisterHotKey(_windowHandle, hotkeyId, modifiers, key))
        {
            _profileHotkeys[hotkeyId] = profileId;
        }
    }

    /// <summary>
    /// Elimina todos los hotkeys de un perfil.
    /// </summary>
    public void UnregisterProfileHotkeys(int profileId)
    {
        var toRemove = _profileHotkeys.Where(kv => kv.Value == profileId).Select(kv => kv.Key).ToList();
        foreach (var hotkeyId in toRemove)
        {
            if (_windowHandle != IntPtr.Zero)
                UnregisterHotKey(_windowHandle, hotkeyId);
            _profileHotkeys.Remove(hotkeyId);
        }
    }

    /// <summary>
    /// Procesa un mensaje WM_HOTKEY y dispara el evento correspondiente.
    /// </summary>
    public void ProcessHotkeyMessage(IntPtr wParam)
    {
        int hotkeyId = wParam.ToInt32();
        if (_profileHotkeys.TryGetValue(hotkeyId, out int profileId))
        {
            HotkeyPressed?.Invoke(profileId);
        }
    }

    /// <summary>
    /// Registra todos los hotkeys desde la lista de perfiles.
    /// </summary>
    public void RegisterAll(IEnumerable<(int ProfileId, string? Hotkey)> profiles)
    {
        // Limpiar todos los hotkeys existentes
        foreach (var hotkeyId in _profileHotkeys.Keys.ToList())
        {
            if (_windowHandle != IntPtr.Zero)
                UnregisterHotKey(_windowHandle, hotkeyId);
        }
        _profileHotkeys.Clear();
        _nextHotkeyId = 1;

        foreach (var (profileId, hotkey) in profiles)
        {
            RegisterProfileHotkey(profileId, hotkey);
        }
    }

    /// <summary>
    /// Parsea un string de hotkey (ej: "Control+Shift+F1") a modificadores y key virtual.
    /// </summary>
    public static bool TryParseHotkey(string hotkey, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;

        var parts = hotkey.Split('+', StringSplitOptions.TrimEntries);
        string? keyPart = null;

        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD_ALT;
                    break;
                case "shift":
                    modifiers |= MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    keyPart = part;
                    break;
            }
        }

        if (string.IsNullOrEmpty(keyPart))
            return false;

        // Intentar parsear la tecla
        var keyConverter = new KeyConverter();
        try
        {
            var keyObj = keyConverter.ConvertFromString(keyPart);
            if (keyObj is Key wpfKey)
            {
                key = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
                return true;
            }
        }
        catch
        {
            // Intentar F1-F12 directamente
            if (keyPart.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(keyPart[1..], out int fNum) && fNum >= 1 && fNum <= 24)
            {
                key = (uint)(0x70 + fNum - 1); // VK_F1 = 0x70
                return true;
            }

            // Intentar teclas especiales
            switch (keyPart.ToUpperInvariant())
            {
                case "HOME": key = 0x24; return true;
                case "END": key = 0x23; return true;
                case "INSERT": key = 0x2D; return true;
                case "DELETE": key = 0x2E; return true;
                case "PAGEUP": key = 0x21; return true;
                case "PAGEDOWN": key = 0x22; return true;
                case "PRINTSCREEN": key = 0x2C; return true;
                case "PAUSE": key = 0x13; return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Convierte un evento KeyEventArgs a string de hotkey legible.
    /// </summary>
    public static string? FormatHotkeyFromKeyEvent(Key key, ModifierKeys modifiers)
    {
        if (key == Key.None || key == Key.System)
            return null;

        // Ignorar si solo se presionó una tecla modificadora
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
            return null;

        var parts = new List<string>();

        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Control");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_windowHandle != IntPtr.Zero)
        {
            foreach (var hotkeyId in _profileHotkeys.Keys)
            {
                UnregisterHotKey(_windowHandle, hotkeyId);
            }
        }
        _profileHotkeys.Clear();
    }
}
