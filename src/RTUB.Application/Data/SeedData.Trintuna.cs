using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    /// <summary>
    /// Dev seed only (full dataset: local dev and Azure DEV, never PROD): the 7 Trintuna almoços. 1-6 are historical
    /// (Passados); 7º (Bragança) is the next one (Próximos). No date, no organizer. Runs only on an empty table.
    /// </summary>
    internal static async Task SeedTrintunaAsync(ApplicationDbContext db)
    {
        if (await db.Gatherings.AnyAsync())
        {
            return;
        }

        (string Place, string Description)[] almocos =
        {
            ("Vizela", "Croquetes"),
            ("Lamares", "Bronha"),
            ("Marco de Canaveses", "Frango"),
            ("Chaves", "Conchita"),
            ("Fafe", "Coma"),
            ("Mogadouro", "Pássaro"),
            ("Bragança", "Jeans"),
        };
        db.Gatherings.AddRange(almocos.Select((a, i) => new Gathering
        {
            Number = i + 1,
            Place = a.Place,
            Description = a.Description,
            IsHistorical = i < 6,
            CreatedAt = DateTime.UtcNow,
        }));
        await db.SaveChangesAsync();
    }
}
