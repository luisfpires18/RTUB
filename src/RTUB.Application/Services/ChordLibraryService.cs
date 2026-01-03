using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Implementação do serviço de biblioteca de acordes
/// Segue princípios SOLID: SRP (responsabilidade única), DIP (injeção de dependências)
/// </summary>
public class ChordLibraryService : IChordLibraryService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ChordLibraryService> _logger;

    private const string CommonChordsCacheKeyPrefix = "CommonChords_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public ChordLibraryService(
        ApplicationDbContext context,
        IMemoryCache cache,
        ILogger<ChordLibraryService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(IEnumerable<ChordDiagramDto> Chords, int TotalCount)> SearchAsync(
        InstrumentType? instrument = null,
        DifficultyLevel? difficulty = null,
        string? searchTerm = null,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        // Validação de parâmetros
        if (pageNumber < 1)
            throw new ArgumentException("O número da página deve ser maior que zero", nameof(pageNumber));

        if (pageSize < 1 || pageSize > 100)
            throw new ArgumentException("O tamanho da página deve estar entre 1 e 100", nameof(pageSize));

        try
        {
            var query = _context.ChordDiagrams.AsQueryable();

            // Aplicar filtros
            if (instrument.HasValue)
            {
                query = query.Where(c => c.InstrumentType == instrument.Value);
            }

            if (difficulty.HasValue)
            {
                query = query.Where(c => c.Difficulty == difficulty.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                // Pesquisa case-insensitive no nome do acorde
                var searchTermLower = searchTerm.ToLower();
                query = query.Where(c => c.ChordName.ToLower().Contains(searchTermLower));
            }

            // Obter contagem total
            var totalCount = await query.CountAsync(ct);

            // Aplicar paginação e ordenação
            var chords = await query
                .OrderBy(c => c.InstrumentType)
                .ThenBy(c => c.Difficulty)
                .ThenBy(c => c.ChordName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync(ct);

            // Mapear para DTOs
            var chordDtos = chords.Select(MapToDto).ToList();

            return (chordDtos, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pesquisar acordes. Instrumento: {Instrument}, Dificuldade: {Difficulty}, Termo: {SearchTerm}",
                instrument, difficulty, searchTerm);
            throw;
        }
    }

    public async Task<ChordDiagramDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            var chord = await _context.ChordDiagrams
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            return chord != null ? MapToDto(chord) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter acorde por ID: {ChordId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<ChordDiagramDto>> GetCommonChordsAsync(
        InstrumentType instrument,
        int limit = 10,
        CancellationToken ct = default)
    {
        if (limit < 1 || limit > 50)
            throw new ArgumentException("O limite deve estar entre 1 e 50", nameof(limit));

        var cacheKey = $"{CommonChordsCacheKeyPrefix}{instrument}_{limit}";

        // Tentar obter do cache
        if (_cache.TryGetValue<List<ChordDiagramDto>>(cacheKey, out var cachedChords) && cachedChords != null)
        {
            _logger.LogDebug("Acordes comuns obtidos do cache para instrumento: {Instrument}", instrument);
            return cachedChords;
        }

        try
        {
            // Buscar do banco de dados
            // Priorizar acordes fáceis e médios, ordenar alfabeticamente
            var chords = await _context.ChordDiagrams
                .Where(c => c.InstrumentType == instrument)
                .OrderBy(c => c.Difficulty)
                .ThenBy(c => c.ChordName)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync(ct);

            var chordDtos = chords.Select(MapToDto).ToList();

            // Armazenar no cache
            _cache.Set(cacheKey, chordDtos, CacheDuration);

            _logger.LogInformation("Retornando {Count} acordes comuns para instrumento: {Instrument}",
                chordDtos.Count, instrument);

            return chordDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter acordes comuns para instrumento: {Instrument}", instrument);
            throw;
        }
    }

    /// <summary>
    /// Mapeia uma entidade ChordDiagram para ChordDiagramDto
    /// </summary>
    private ChordDiagramDto MapToDto(ChordDiagram chord)
    {
        ChordFingeringDto? fingering = null;

        // Deserializar dados de dedilhado JSON
        if (!string.IsNullOrWhiteSpace(chord.FingeringData))
        {
            try
            {
                fingering = JsonSerializer.Deserialize<ChordFingeringDto>(chord.FingeringData);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Erro ao deserializar dados de dedilhado para acorde ID: {ChordId}", chord.Id);
            }
        }

        return new ChordDiagramDto
        {
            Id = chord.Id,
            InstrumentType = chord.InstrumentType,
            InstrumentName = InstrumentTypeHelper.GetDisplayName(chord.InstrumentType),
            ChordName = chord.ChordName,
            Difficulty = chord.Difficulty,
            DifficultyName = GetDifficultyDisplayName(chord.Difficulty),
            Fingering = fingering,
            ImageUrl = chord.ImageUrl,
            AudioSampleUrl = chord.AudioSampleUrl,
            CreatedAt = chord.CreatedAt
        };
    }

    /// <summary>
    /// Obtém o nome de exibição localizado para o nível de dificuldade
    /// </summary>
    private static string GetDifficultyDisplayName(DifficultyLevel difficulty)
    {
        var memberInfo = typeof(DifficultyLevel).GetMember(difficulty.ToString()).FirstOrDefault();
        var displayAttribute = memberInfo?.GetCustomAttributes(typeof(DisplayAttribute), false)
            .FirstOrDefault() as DisplayAttribute;

        return displayAttribute?.Name ?? difficulty.ToString();
    }
}
