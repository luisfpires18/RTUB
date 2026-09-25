using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Services.AfterHours;

/// <summary>
/// Database-backed tuning. Rows are read on every call (a handful of rows; no cache to go stale): actions load
/// the snapshot once inside their own write transaction with <see cref="LoadAsync(ApplicationDbContext)"/>.
/// </summary>
public class AfterHoursTuningService(IDbContextFactory<ApplicationDbContext> contextFactory) : IAfterHoursTuningService
{
    public async Task<AfterHoursTuning> GetAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        return await LoadAsync(context);
    }

    /// <summary>One read of every override, applied over the defaults. Invalid rows are ignored here.</summary>
    internal static async Task<AfterHoursTuning> LoadAsync(ApplicationDbContext context) =>
        AfterHoursTuning.From(await RowsAsync(context), out _);

    public async Task<IReadOnlyList<TuningSettingView>> GetSettingsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await context.AfterHoursTuningSettings.AsNoTracking().ToDictionaryAsync(r => r.Key);
        var effective = AfterHoursTuning.From(rows.Select(r => KeyValuePair.Create(r.Key, r.Value.Value)), out var invalid);
        return AfterHoursTuning.Settings.Select(s =>
        {
            rows.TryGetValue(s.Key, out var row);
            return new TuningSettingView(s, s.Read(AfterHoursTuning.Default), s.Read(effective), row?.Value,
                invalid.FirstOrDefault(i => i.Key == s.Key).Error, row?.UpdatedAtUtc, row?.UpdatedByUserId);
        }).ToList();
    }

    public async Task<IReadOnlyList<InvalidTuningRow>> GetInvalidRowsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var rows = await RowsAsync(context);
        AfterHoursTuning.From(rows, out var invalid);
        return invalid.Select(i => new InvalidTuningRow(i.Key, rows.First(r => r.Key == i.Key).Value, i.Error)).ToList();
    }

    private static async Task<List<KeyValuePair<string, string>>> RowsAsync(ApplicationDbContext context) =>
        (await context.AfterHoursTuningSettings.AsNoTracking().ToListAsync())
            .Select(r => KeyValuePair.Create(r.Key, r.Value)).ToList();
}
