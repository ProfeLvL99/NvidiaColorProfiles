namespace NvidiaColorProfileManager.Models;

/// <summary>
/// Valores de configuración de color para un perfil.
/// </summary>
public class ColorSettings
{
    /// <summary>Brillo (0-100)</summary>
    public int Brightness { get; set; } = 50;

    /// <summary>Contraste (0-100)</summary>
    public int Contrast { get; set; } = 50;

    /// <summary>Gamma (0.5 - 3.0)</summary>
    public double Gamma { get; set; } = 1.0;

    /// <summary>Digital Vibrance / Saturación (0-100)</summary>
    public int DigitalVibrance { get; set; } = 50;

    /// <summary>Temperatura de color (-100 = cálido, 0 = neutral 6500K, +100 = frío)</summary>
    public int ColorTemperature { get; set; } = 0;

    public ColorSettings Clone()
    {
        return new ColorSettings
        {
            Brightness = Brightness,
            Contrast = Contrast,
            Gamma = Gamma,
            DigitalVibrance = DigitalVibrance,
            ColorTemperature = ColorTemperature,
        };
    }
}
