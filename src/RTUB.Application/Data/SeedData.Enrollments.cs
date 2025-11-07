using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    /// <summary>
    /// Mass-enroll members into events.
    /// - Guarantees a core lineup per EventType (festival / NERBA / convívio).
    /// - Adds extra participants deterministically using a stable hash.
    /// - Mixes confirmation states (most confirmed, some pending).
    /// </summary>
    public static async Task SeedEnrollmentsAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        if (await dbContext.Enrollments.AnyAsync()) return;

        var events = await dbContext.Events
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .ToListAsync();
        if (events.Count == 0) return;

        var members = await userManager.Users.AsNoTracking().ToListAsync();
        if (members.Count == 0) return;

        // Split pools to avoid null-key dictionary issues
        var membersWithInstrument = members.Where(m => m.MainInstrument.HasValue).ToList();
        var noInstrument = members.Where(m => !m.MainInstrument.HasValue).ToList();

        // Build dictionary only from members that have an instrument
        var byInstrument = membersWithInstrument
            .GroupBy(m => m.MainInstrument!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Round-robin per instrument
        var rrIndex = new Dictionary<InstrumentType, int>();
        foreach (InstrumentType it in Enum.GetValues(typeof(InstrumentType)))
            rrIndex[it] = 0;

        ApplicationUser TakeNext(InstrumentType it)
        {
            if (byInstrument.TryGetValue(it, out var list) && list.Count > 0)
            {
                var idx = rrIndex[it] % list.Count;
                rrIndex[it] = (rrIndex[it] + 1) % list.Count;
                return list[idx];
            }

            // Prefer anyone that *has* an instrument; else anyone at all
            if (membersWithInstrument.Count > 0)
                return membersWithInstrument[StableIndex("fallback_any_instrument_" + it, membersWithInstrument.Count)];

            return members[StableIndex("fallback_any_" + it, members.Count)];
        }

        var toAdd = new List<Enrollment>();
        var seen = new HashSet<string>(); // userId:eventId

        // dentro do SeedEnrollmentsAsync, troca apenas o AddOnce por isto:
        void AddOnce(ApplicationUser u, Event evt)
        {
            var key = $"{u.Id}:{evt.Id}";
            if (seen.Add(key))
            {
                var en = Enrollment.Create(u.Id, evt.Id);
                en.Instrument = u.MainInstrument; // Leitões: null -> UI mostra N/A
                toAdd.Add(en);
            }
        }

        foreach (var evt in events)
        {
            // Core lineup per event type
            var coreNeeds = evt.Type switch
            {
                EventType.Festival => new Dictionary<InstrumentType, int>
            {
                { InstrumentType.Guitarra,   3 },
                { InstrumentType.Bandolim,   2 },
                { InstrumentType.Baixo,      1 },
                { InstrumentType.Percussao,  1 },
                { InstrumentType.Pandeireta, 1 },
                { InstrumentType.Estandarte, 1 },
                { InstrumentType.Flauta,     1 },
                { InstrumentType.Cavaquinho, 1 },
                { InstrumentType.Acordeao,   1 },
            },
                EventType.Nerba => new Dictionary<InstrumentType, int>
            {
                { InstrumentType.Guitarra,   3 },
                { InstrumentType.Bandolim,   2 },
                { InstrumentType.Baixo,      1 },
                { InstrumentType.Percussao,  1 },
                { InstrumentType.Pandeireta, 2 },
                { InstrumentType.Estandarte, 1 },
            },
                _ => new Dictionary<InstrumentType, int> // Convívio & others
            {
                { InstrumentType.Guitarra,   2 },
                { InstrumentType.Bandolim,   1 },
                { InstrumentType.Baixo,      1 },
                { InstrumentType.Percussao,  1 },
                { InstrumentType.Pandeireta, 1 },
                { InstrumentType.Estandarte, 1 },
            }
            };

            foreach (var kv in coreNeeds)
            {
                for (int i = 0; i < kv.Value; i++)
                {
                    var u = TakeNext(kv.Key);
                    AddOnce(u, evt);
                }
            }

            // Extra participants (deterministic % by event type)
            int percent = evt.Type switch
            {
                EventType.Festival => 70,
                EventType.Nerba => 60,
                _ => 45
            };

            foreach (var u in members)
            {
                if (seen.Contains($"{u.Id}:{evt.Id}")) continue;
                var go = StablePercent(u.Email + ":" + evt.Id) < percent;
                if (!go) continue;
                AddOnce(u, evt);
            }

            // Give no-instrument members (Leitões) an additional deterministic chance
            foreach (var u in noInstrument)
            {
                if (seen.Contains($"{u.Id}:{evt.Id}")) continue;
                var go = StablePercent("leitao:" + u.Email + ":" + evt.Id) < Math.Max(25, percent - 15);
                if (!go) continue;
                AddOnce(u, evt);
            }
        }

        if (toAdd.Count > 0)
        {
            await dbContext.Enrollments.AddRangeAsync(toAdd);
            await dbContext.SaveChangesAsync();
        }
    }
    // ---------- Helpers ----------

    // Stable 0..99 based on SHA256 of input
    private static int StablePercent(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        // Use first two bytes to get 0..65535 then scale to 0..99
        int val = (bytes[0] << 8) | bytes[1];
        return (int)Math.Floor((val / 65535.0) * 100.0);
    }

    // Stable index into [0..count-1]
    private static int StableIndex(string input, int count)
    {
        if (count <= 0) return 0;
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        int val = (bytes[2] << 24) | (bytes[3] << 16) | (bytes[4] << 8) | bytes[5];
        val = Math.Abs(val);
        return val % count;
    }
}