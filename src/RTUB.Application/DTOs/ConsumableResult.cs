namespace RTUB.Application.DTOs;

/// <summary>
/// Result of applying a consumable during interactive combat.
/// Contains all information the Razor page needs to build the JSON response for JS.
/// </summary>
public class ConsumableResult
{
    /// <summary>Whether the consumable was successfully applied.</summary>
    public bool Success { get; set; }

    /// <summary>User-facing message (Portuguese) — set on both success and failure.</summary>
    public string? Message { get; set; }

    /// <summary>Consumable type key (e.g. "fino", "caneca").</summary>
    public string? Type { get; set; }

    /// <summary>Heal amount applied (for healing consumables).</summary>
    public long? HealAmount { get; set; }

    /// <summary>Player's HP after the consumable was applied.</summary>
    public long? PlayerHP { get; set; }

    /// <summary>Player's max HP.</summary>
    public long? PlayerMaxHP { get; set; }

    /// <summary>Cooldown applied to this consumable in seconds.</summary>
    public double? CooldownSeconds { get; set; }

    /// <summary>Buff message for the UI (e.g. "🛡️ ESCUDO x3").</summary>
    public string? BuffMessage { get; set; }

    /// <summary>Whether a buff is now active.</summary>
    public bool? BuffActive { get; set; }

    /// <summary>Player's new action time (for Penalty buff).</summary>
    public double? NewActionTime { get; set; }

    /// <summary>
    /// Creates a failure result with the given Portuguese message.
    /// </summary>
    public static ConsumableResult Fail(string message) => new() { Success = false, Message = message };
}
