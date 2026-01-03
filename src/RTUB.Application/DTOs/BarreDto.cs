namespace RTUB.Application.DTOs;

/// <summary>
/// Representa informações de barra (pestana) em um acorde
/// </summary>
public class BarreDto
{
    /// <summary>
    /// Traste onde a barra começa
    /// </summary>
    public int Fret { get; set; }

    /// <summary>
    /// Corda inicial da barra (mais grave)
    /// </summary>
    public int FromString { get; set; }

    /// <summary>
    /// Corda final da barra (mais aguda)
    /// </summary>
    public int ToStringNumber { get; set; }
}
