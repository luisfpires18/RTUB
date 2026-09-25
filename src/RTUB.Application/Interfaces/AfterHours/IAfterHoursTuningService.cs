using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>One tunable as the admin page shows it: default, stored override (if any) and what play uses.</summary>
public sealed record TuningSettingView(
    TuningSetting Setting, string DefaultValue, string EffectiveValue, string? OverrideValue, string? InvalidReason,
    DateTime? UpdatedAtUtc, string? UpdatedByUserId);

/// <summary>An override row that play ignores (unknown key or a value that fails validation).</summary>
public sealed record InvalidTuningRow(string Key, string Value, string Error);

/// <summary>Read side of the database-backed tuning. Writes are Owner-only, in <see cref="IAfterHoursAdminService"/>.</summary>
public interface IAfterHoursTuningService
{
    /// <summary>The effective snapshot: defaults with every valid override applied. Never throws on bad rows.</summary>
    Task<AfterHoursTuning> GetAsync();

    Task<IReadOnlyList<TuningSettingView>> GetSettingsAsync();

    Task<IReadOnlyList<InvalidTuningRow>> GetInvalidRowsAsync();
}
