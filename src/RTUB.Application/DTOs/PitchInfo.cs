namespace RTUB.Application.DTOs;

/// <summary>
/// Informação sobre a análise de frequência detectada
/// </summary>
public class PitchInfo
{
    /// <summary>
    /// Nome da nota musical (ex: "A", "C#", "Bb")
    /// </summary>
    public string NoteName { get; set; } = string.Empty;

    /// <summary>
    /// Oitava da nota (ex: 4 para A4)
    /// </summary>
    public int Octave { get; set; }

    /// <summary>
    /// Frequência detectada em Hz
    /// </summary>
    public double Frequency { get; set; }

    /// <summary>
    /// Desvio em cents da nota mais próxima (-50 a +50)
    /// Valores positivos = mais agudo, negativos = mais grave
    /// </summary>
    public double CentsOffset { get; set; }

    /// <summary>
    /// Indica se a nota está afinada (dentro da tolerância)
    /// </summary>
    public bool InTune { get; set; }
}
