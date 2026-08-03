using System.Runtime.InteropServices;

namespace NvidiaColorProfileManager.Services;

/// <summary>
/// Control de gamma, brillo, contraste y temperatura de color vía Windows GDI.
/// </summary>
public class WindowsGammaService
{
    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, IntPtr lpRamp);

    [DllImport("kernel32.dll")]
    private static extern uint GetLastError();

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
        _applyFailed = false;
        LastErrorCode = 0;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, ApplyToAllCallback, IntPtr.Zero);

        LastApplySucceeded = !_applyFailed;
    }

    /// <summary>True if the last gamma ramp was successfully applied to at least one monitor.</summary>
    public bool LastApplySucceeded { get; private set; } = true;

    /// <summary>Windows error code from the last failed SetDeviceGammaRamp call (0 if success).</summary>
    public uint LastErrorCode { get; private set; }

    private bool _applyFailed;

    public void ApplyToMonitor(int monitorIndex, double gamma, double brightness, double contrast, int colorTemperature = 0)
    {
        gamma = Math.Clamp(gamma, 0.5, 3.0);
        brightness = Math.Clamp(brightness, 0, 100);
        contrast = Math.Clamp(contrast, 0, 100);
        colorTemperature = Math.Clamp(colorTemperature, -100, 100);

        _currentRamp = BuildGammaRamp(gamma, brightness, contrast, colorTemperature);
        _targetMonitorIndex = monitorIndex;
        _currentMonitorIndex = 0;
        _applyFailed = false;
        LastErrorCode = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, ApplyToSingleCallback, IntPtr.Zero);

        LastApplySucceeded = !_applyFailed;
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

        // Encode brightness as a gamma multiplier — this keeps ramp endpoints at 0 and 65535
        // B=50 → multiplier=1.0 (no change). B<50 → higher gamma (darker midtones). B>50 → lower gamma (brighter midtones).
        double brightnessGamma = brightness > 0 ? 50.0 / brightness : 10.0;
        double contrastFactor = contrast / 50.0;
        double tempFactor = colorTemperature / 100.0;

        // Color temperature via per-channel gamma: warm = red gamma higher (more red), blue gamma lower (less blue)
        double redGamma = gamma * brightnessGamma * (1.0 + tempFactor * 0.7);
        double greenGamma = gamma * brightnessGamma;
        double blueGamma = gamma * brightnessGamma * (1.0 - tempFactor * 0.7);

        // Fold contrast into gamma — keeps output as pure power-law (what SetDeviceGammaRamp validates)
        // C>50 → higher gamma (steeper, more contrast). C<50 → lower gamma (flatter, less contrast).
        double contrastPower = contrastFactor > 0.01 ? Math.Pow(contrastFactor, 0.6) : 0.1;
        redGamma   *= contrastPower;
        greenGamma *= contrastPower;
        blueGamma  *= contrastPower;

        // Prevent degenerate gamma values (too low → first entries round to 0 creating duplicates;
        // too high → ramp too flat and SetDeviceGammaRamp may reject).
        const double minGamma = 0.4;
        const double maxGamma = 3.5;
        redGamma   = Math.Clamp(redGamma,   minGamma, maxGamma);
        greenGamma = Math.Clamp(greenGamma, minGamma, maxGamma);
        blueGamma  = Math.Clamp(blueGamma,  minGamma, maxGamma);

        for (int i = 0; i < 256; i++)
        {
            double n = i / 255.0;

            double r = Math.Pow(n, 1.0 / redGamma) * 65535.0;
            double g = Math.Pow(n, 1.0 / greenGamma) * 65535.0;
            double b = Math.Pow(n, 1.0 / blueGamma) * 65535.0;

            ramp.Red[i] = (ushort)Math.Round(r);
            ramp.Green[i] = (ushort)Math.Round(g);
            ramp.Blue[i] = (ushort)Math.Round(b);
        }

        // Fix any duplicate values caused by rounding (can still happen at extreme low gamma)
        EnsureStrictlyIncreasing(ramp.Red);
        EnsureStrictlyIncreasing(ramp.Green);
        EnsureStrictlyIncreasing(ramp.Blue);

        return ramp;
    }

    /// <summary>
    /// Fix duplicate values after rounding: if two adjacent values are equal, bump the later one up by 1.
    /// Guarantees monotonicity regardless of rounding artifacts from extremely low gamma values.
    /// </summary>
    private static void EnsureStrictlyIncreasing(ushort[] ramp)
    {
        for (int i = 1; i < 256; i++)
        {
            if (ramp[i] <= ramp[i - 1] && ramp[i - 1] < 65535)
                ramp[i] = (ushort)(ramp[i - 1] + 1);
        }
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
                try
                {
                    // Marshal the three 256-ushort arrays as a contiguous short[768] block
                    var rampData = new short[768];
                    for (int i = 0; i < 256; i++)
                    {
                        rampData[i]       = unchecked((short)_currentRamp.Red[i]);
                        rampData[256 + i] = unchecked((short)_currentRamp.Green[i]);
                        rampData[512 + i] = unchecked((short)_currentRamp.Blue[i]);
                    }

                    IntPtr ptr = Marshal.AllocHGlobal(768 * sizeof(short));
                    try
                    {
                        Marshal.Copy(rampData, 0, ptr, 768);
                        if (!SetDeviceGammaRamp(hDC, ptr))
                        {
                            _applyFailed = true;
                            LastErrorCode = GetLastError();
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(ptr);
                    }
                }
                catch { _applyFailed = true; }
                finally { DeleteDC(hDC); }
            }
        }
    }
}
