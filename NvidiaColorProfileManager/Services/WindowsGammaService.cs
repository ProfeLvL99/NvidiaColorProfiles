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

        double brightnessFactor = brightness / 50.0;
        double contrastFactor = contrast / 50.0;
        double tempFactor = colorTemperature / 100.0;

        double redMult = 1.0 - tempFactor * 0.40;
        double blueMult = 1.0 + tempFactor * 0.40;

        // Clamp gamma to a safe range for the power function
        const double minGamma = 0.5;
        const double maxGamma = 3.0;
        double safeGamma = Math.Clamp(gamma, minGamma, maxGamma);

        for (int i = 0; i < 256; i++)
        {
            double n = i / 255.0;

            // 1. Pure power-law gamma (independent, preserves 0 and 65535)
            double r = Math.Pow(n, 1.0 / safeGamma) * 65535.0;
            double g = Math.Pow(n, 1.0 / safeGamma) * 65535.0;
            double b = Math.Pow(n, 1.0 / safeGamma) * 65535.0;

            // 2. Contrast — S-curve around midpoint (C>50 pushes away, C<50 pulls toward center)
            r = (r - 32767.5) * contrastFactor + 32767.5;
            g = (g - 32767.5) * contrastFactor + 32767.5;
            b = (b - 32767.5) * contrastFactor + 32767.5;

            // 3. Brightness — linear multiplier (B>50 brighter, B<50 darker)
            r *= brightnessFactor;
            g *= brightnessFactor;
            b *= brightnessFactor;

            // 4. Color temperature — per-channel multiplier
            r *= redMult;
            b *= blueMult;

            // Clamp to valid range, then EnsureStrictlyIncreasing fixes any plateaus
            ramp.Red[i]   = (ushort)Math.Clamp(Math.Round(r), 0, 65535);
            ramp.Green[i] = (ushort)Math.Clamp(Math.Round(g), 0, 65535);
            ramp.Blue[i]  = (ushort)Math.Clamp(Math.Round(b), 0, 65535);
        }

        // Fix any duplicate/clamped values — guarantees valid ramp for SetDeviceGammaRamp
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
