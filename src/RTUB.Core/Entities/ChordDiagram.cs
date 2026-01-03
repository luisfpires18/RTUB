using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Representa um diagrama de acorde para um instrumento específico
/// </summary>
public class ChordDiagram : BaseEntity
{
    /// <summary>
    /// Tipo de instrumento para o qual este acorde é destinado
    /// </summary>
    [Required(ErrorMessage = "O tipo de instrumento é obrigatório")]
    public InstrumentType InstrumentType { get; set; }

    /// <summary>
    /// Nome do acorde (ex: C, D, Am, G7)
    /// </summary>
    [Required(ErrorMessage = "O nome do acorde é obrigatório")]
    [MaxLength(50, ErrorMessage = "O nome do acorde não pode exceder 50 caracteres")]
    public string ChordName { get; set; } = string.Empty;

    /// <summary>
    /// Nível de dificuldade do acorde
    /// </summary>
    [Required(ErrorMessage = "O nível de dificuldade é obrigatório")]
    public DifficultyLevel Difficulty { get; set; }

    /// <summary>
    /// Dados de dedilhado em formato JSON (armazenamento compatível com SQLite)
    /// </summary>
    [Required(ErrorMessage = "Os dados de dedilhado são obrigatórios")]
    public string FingeringData { get; set; } = string.Empty;

    /// <summary>
    /// URL opcional para imagem do diagrama de acorde
    /// </summary>
    [MaxLength(500, ErrorMessage = "A URL da imagem não pode exceder 500 caracteres")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// URL opcional para amostra de áudio do acorde
    /// </summary>
    [MaxLength(500, ErrorMessage = "A URL do áudio não pode exceder 500 caracteres")]
    public string? AudioSampleUrl { get; set; }

    public ChordDiagram() { }

    /// <summary>
    /// Cria um novo diagrama de acorde
    /// </summary>
    public static ChordDiagram Create(
        InstrumentType instrumentType,
        string chordName,
        DifficultyLevel difficulty,
        string fingeringData,
        string? imageUrl = null,
        string? audioSampleUrl = null)
    {
        if (string.IsNullOrWhiteSpace(chordName))
            throw new ArgumentException("O nome do acorde não pode estar vazio", nameof(chordName));

        if (string.IsNullOrWhiteSpace(fingeringData))
            throw new ArgumentException("Os dados de dedilhado não podem estar vazios", nameof(fingeringData));

        return new ChordDiagram
        {
            InstrumentType = instrumentType,
            ChordName = chordName,
            Difficulty = difficulty,
            FingeringData = fingeringData,
            ImageUrl = imageUrl,
            AudioSampleUrl = audioSampleUrl
        };
    }

    /// <summary>
    /// Atualiza os detalhes do diagrama de acorde
    /// </summary>
    public void UpdateDetails(
        string chordName,
        DifficultyLevel difficulty,
        string fingeringData,
        string? imageUrl,
        string? audioSampleUrl)
    {
        if (string.IsNullOrWhiteSpace(chordName))
            throw new ArgumentException("O nome do acorde não pode estar vazio", nameof(chordName));

        if (string.IsNullOrWhiteSpace(fingeringData))
            throw new ArgumentException("Os dados de dedilhado não podem estar vazios", nameof(fingeringData));

        ChordName = chordName;
        Difficulty = difficulty;
        FingeringData = fingeringData;
        ImageUrl = imageUrl;
        AudioSampleUrl = audioSampleUrl;
    }
}
