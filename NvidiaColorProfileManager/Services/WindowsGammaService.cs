using System.Runtime.InteropServices;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Control de gamma, brillo, contraste y temperatura de color vía Windows GDI.
/// </summary>
public class WindowsGammaService
{
    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref GammaRamp lpRamp);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr CreateDC(string? lpszDriver, string lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int cbSize;
        public Rect rcMonitor, rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct GammaRamp
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    private GammaRamp _currentRamp;
    private int _targetMonitorIndex;
    private int _currentMonitorIndex;

    public void ApplyToAllMonitors(double gamma, double brightness, double contrast, int colorTemperature = 0)
    {
        gamma = Math.Clamp(gamma, 0.5, 3.0);
        brightness = Math.Clamp(brightness, 0, 100);
        contrast = Math.Clamp(contrast, 0, 100);
        colorTemperature = Math.Clamp(colorTemperature, -100, 100);

        _currentRamp = BuildGammaRamp(gamma, brightness, contrast, colorTemperature);
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, ApplyToAllCallback, IntPtr.Zero);
    }

    public void ApplyToMonitor(int monitorIndex, double gamma, double brightness, double contrast, int colorTemperature = 0)
    {
        gamma = Math.Clamp(gamma, 0.5, 3.0);
        brightness = Math.Clamp(brightness, 0, 100);
        contrast = Math.Clamp(contrast, 0, 100);
        colorTemperature = Math.Clamp(colorTemperature, -100, 100);

        _currentRamp = BuildGammaRamp(gamma, brightness, contrast, colorTemperature);
        _targetMonitorIndex = monitorIndex;
        _currentMonitorIndex = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, ApplyToSingleCallback, IntPtr.Zero);
    }

    public void RestoreDefaults()
    {
        ApplyToAllMonitors(1.0, 50, 50, 0);
    }

    private static GammaRamp BuildGammaRamp(double gamma, double brightness, double contrast, int colorTemperature)
    {
        var ramp = new GammaRamp
        {
            Red = new ushort[256],
            Green = new ushort[256],
            Blue = new ushort[256]
        };

        double brightnessFactor = brightness / 50.0;
        double contrastFactor = contrast / 50.0;

        // Temperatura de color: ajusta balance R/B
        // temp > 0 (frío): reduce rojo, boostea azul
        // temp < 0 (cálido): boostea rojo, reduce azul
        double tempFactor = colorTemperature / 100.0; // -1.0 a 1.0
        double redMult = 1.0 - tempFactor * 0.40;   // 1.4 en cálido, 1.0 en neutral, 0.6 en frío
        double blueMult = 1.0 + tempFactor * 0.40;   // 0.6 en cálido, 1.0 en neutral, 1.4 en frío

        for (int i = 0; i < 256; i++)
        {
            double value = Math.Pow(i / 255.0, 1.0 / gamma) * 65535.0;
            value = ((value - 32767.5) * contrastFactor + 32767.5) * brightnessFactor;
            value = Math.Clamp(value, 0, 65535);

            ramp.Green[i] = (ushort)value;
            ramp.Red[i] = (ushort)Math.Clamp(value * redMult, 0, 65535);
            ramp.Blue[i] = (ushort)Math.Clamp(value * blueMult, 0, 65535);
        }
        return ramp;
    }

    private bool ApplyToAllCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
    {
        ApplyGammaToMonitor(hMonitor);
        return true;
    }

    private bool ApplyToSingleCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
    {
        if (_currentMonitorIndex == _targetMonitorIndex)
        {
            ApplyGammaToMonitor(hMonitor);
            return false;
        }
        _currentMonitorIndex++;
        return true;
    }

    private void ApplyGammaToMonitor(IntPtr hMonitor)
    {
        var monitorInfo = new MonitorInfoEx();
        monitorInfo.cbSize = Marshal.SizeOf(typeof(MonitorInfoEx));

        if (GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            IntPtr hDC = CreateDC(null, monitorInfo.szDevice, null, IntPtr.Zero);
            if (hDC != IntPtr.Zero)
            {
                try { SetDeviceGammaRamp(hDC, ref _currentRamp); }
                catch { }
                finally { DeleteDC(hDC); }
            }
        }
    }
}
