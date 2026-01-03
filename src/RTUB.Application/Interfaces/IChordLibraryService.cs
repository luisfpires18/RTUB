using RTUB.Application.DTOs;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Serviço para gerenciar a biblioteca de acordes musicais
/// </summary>
public interface IChordLibraryService
{
    /// <summary>
    /// Pesquisa acordes com filtros e paginação
    /// </summary>
    /// <param name="instrument">Tipo de instrumento (opcional)</param>
    /// <param name="difficulty">Nível de dificuldade (opcional)</param>
    /// <param name="searchTerm">Termo de pesquisa para nome do acorde (opcional)</param>
    /// <param name="pageNumber">Número da página (padrão: 1)</param>
    /// <param name="pageSize">Tamanho da página (padrão: 20)</param>
    /// <param name="ct">Token de cancelamento</param>
    /// <returns>Tupla com lista de acordes e contagem total</returns>
    Task<(IEnumerable<ChordDiagramDto> Chords, int TotalCount)> SearchAsync(
        InstrumentType? instrument = null,
        DifficultyLevel? difficulty = null,
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Obtém um diagrama de acorde por ID
    /// </summary>
    /// <param name="id">ID do diagrama de acorde</param>
    /// <param name="ct">Token de cancelamento</param>
    /// <returns>DTO do diagrama de acorde ou null se não encontrado</returns>
    Task<ChordDiagramDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Obtém os acordes mais comuns para um instrumento
    /// </summary>
    /// <param name="instrument">Tipo de instrumento</param>
    /// <param name="limit">Número máximo de acordes a retornar (padrão: 10)</param>
    /// <param name="ct">Token de cancelamento</param>
    /// <returns>Lista de acordes mais comuns</returns>
    Task<IEnumerable<ChordDiagramDto>> GetCommonChordsAsync(
        InstrumentType instrument,
        int limit = 10,
        CancellationToken ct = default);
}
