using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Representa uma configuração de afinação para um instrumento
/// </summary>
public class InstrumentTuning : BaseEntity
{
    [Required(ErrorMessage = "O tipo de instrumento é obrigatório")]
    public InstrumentType InstrumentType { get; set; }

    [Required(ErrorMessage = "O nome da afinação é obrigatório")]
    [MaxLength(100, ErrorMessage = "O nome da afinação não pode exceder 100 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "As notas de afinação são obrigatórias")]
    [MaxLength(500, ErrorMessage = "As notas de afinação não podem exceder 500 caracteres")]
    public string TuningNotes { get; set; } = string.Empty;

    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Gets the tuning notes as an array (deserialized from JSON)
    /// </summary>
    [NotMapped]
    public string[] NotesArray
    {
        get
        {
            try
            {
                return string.IsNullOrWhiteSpace(TuningNotes)
                    ? Array.Empty<string>()
                    : JsonSerializer.Deserialize<string[]>(TuningNotes) ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }

    public InstrumentTuning() { }

    public static InstrumentTuning Create(InstrumentType instrumentType, string name, string tuningNotes, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da afinação não pode estar vazio", nameof(name));

        if (string.IsNullOrWhiteSpace(tuningNotes))
            throw new ArgumentException("As notas de afinação não podem estar vazias", nameof(tuningNotes));

        return new InstrumentTuning
        {
            InstrumentType = instrumentType,
            Name = name,
            TuningNotes = tuningNotes,
            IsDefault = isDefault
        };
    }

    public void UpdateDetails(string name, string tuningNotes, bool isDefault)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome da afinação não pode estar vazio", nameof(name));

        if (string.IsNullOrWhiteSpace(tuningNotes))
            throw new ArgumentException("As notas de afinação não podem estar vazias", nameof(tuningNotes));

        Name = name;
        TuningNotes = tuningNotes;
        IsDefault = isDefault;
    }
}
