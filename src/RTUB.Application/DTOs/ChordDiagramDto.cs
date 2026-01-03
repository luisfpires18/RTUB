using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO para diagrama de acorde
/// </summary>
public class ChordDiagramDto
{
    /// <summary>
    /// ID do diagrama
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Tipo de instrumento
    /// </summary>
    public InstrumentType InstrumentType { get; set; }

    /// <summary>
    /// Nome do instrumento (localizado)
    /// </summary>
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>
    /// Nome do acorde
    /// </summary>
    public string ChordName { get; set; } = string.Empty;

    /// <summary>
    /// Nível de dificuldade
    /// </summary>
    public DifficultyLevel Difficulty { get; set; }

    /// <summary>
    /// Nome da dificuldade (localizado)
    /// </summary>
    public string DifficultyName { get; set; } = string.Empty;

    /// <summary>
    /// Dados de dedilhado do acorde
    /// </summary>
    public ChordFingeringDto? Fingering { get; set; }

    /// <summary>
    /// URL opcional para imagem do diagrama
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// URL opcional para amostra de áudio
    /// </summary>
    public string? AudioSampleUrl { get; set; }

    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
