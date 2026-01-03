namespace RTUB.Application.DTOs;

/// <summary>
/// Representa os dados de dedilhado de um acorde
/// </summary>
public class ChordFingeringDto
{
    /// <summary>
    /// Número de cordas do instrumento
    /// </summary>
    public int Strings { get; set; }

    /// <summary>
    /// Array de trastes para cada corda (-1 = não tocar, 0 = corda solta, >0 = traste)
    /// </summary>
    public int[] Frets { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Array de dedos para cada corda (0 = não usar, 1-4 = dedos)
    /// </summary>
    public int[] Fingers { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Traste base do diagrama (usado para acordes em posições mais altas)
    /// </summary>
    public int BaseFret { get; set; } = 1;

    /// <summary>
    /// Array de barras (pestanas) no acorde
    /// </summary>
    public BarreDto[] Barres { get; set; } = Array.Empty<BarreDto>();
}
