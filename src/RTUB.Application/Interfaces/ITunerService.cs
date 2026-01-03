using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Serviço para afinação de instrumentos musicais
/// </summary>
public interface ITunerService
{
    /// <summary>
    /// Obtém os presets de afinação para um instrumento específico
    /// </summary>
    /// <param name="instrument">Tipo de instrumento</param>
    /// <param name="ct">Token de cancelamento</param>
    /// <returns>Lista de configurações de afinação disponíveis</returns>
    Task<IEnumerable<InstrumentTuning>> GetTuningPresetsAsync(InstrumentType instrument, CancellationToken ct = default);

    /// <summary>
    /// Valida uma frequência detectada e retorna informações sobre a nota
    /// </summary>
    /// <param name="frequency">Frequência em Hz</param>
    /// <param name="referencePitch">Frequência de referência para A4 (padrão: 440 Hz)</param>
    /// <returns>Informações sobre a nota detectada incluindo desvio e se está afinada</returns>
    PitchInfo ValidatePitch(double frequency, double referencePitch = 440);
}
