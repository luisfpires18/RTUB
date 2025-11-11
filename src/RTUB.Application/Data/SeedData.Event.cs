using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the database with initial data such as administrator role and user.
/// </summary>
public static partial class SeedData
{
    public static async Task SeedEventsAsync(ApplicationDbContext dbContext)
    {
        // Seed sample events
        if (!await dbContext.Events.AnyAsync())
        {
            var events = new List<Event>();

            // --- EXISTENTES (os teus) ---
            var event1 = Event.Create("VII CITARA Festival Evora", new DateTime(2025, 9, 25), "Évora", EventType.Festival, "Festival de Tunas de 25 a 27 de setembro de 2025");
            event1.SetImage(null);

            var event2 = Event.Create("Semana da Receção ao Caloiro 2025", new DateTime(2025, 10, 30), "Bragança", EventType.Nerba, "30 de outubro a 2 Novembro - NERBA");
            event2.SetImage(null);

            var event3 = Event.Create("Magusto 2025", new DateTime(2025, 11, 12), "Bragança", EventType.Convivio, "Convívio tradicional de São Martinho");
            event3.SetImage(null);

            var event4 = Event.Create("34º Aniversário", new DateTime(2025, 12, 4), "Bragança", EventType.Convivio, "Celebração do 34º Aniversário da RTUB - 4, 5, 6 Dezembro");
            event4.SetImage(null);

            var event5 = Event.Create("XXX Edição do ETUMa - Festival de Tunas Universitárias da Madeira", new DateTime(2025, 12, 11), "Madeira", EventType.Festival, "11 a 13 de Dezembro 2025");
            event5.SetImage(null);

            var event6 = Event.Create("XIV Festunag - Águeda", new DateTime(2026, 3, 20), "Águeda", EventType.Festival, "20-21-22 Março 2026");
            event6.SetImage(null);

            events.AddRange(new[] { event1, event2, event3, event4, event5, event6 });

            // --- CONFIRMADOS NAS REDES (FITAB / SRC / RTÔNA) ---
            var fitab2018Noite = Event.Create("XX FITAB — Noite de Serenatas", new DateTime(2018, 5, 18), "Bragança", EventType.Festival, "Noite de Serenatas do XX FITAB (Castelo de Bragança).");
            fitab2018Noite.SetImage(null);
            var fitab2018Gala = Event.Create("XX FITAB — Gala", new DateTime(2018, 5, 19), "Bragança", EventType.Festival, "Gala do XX FITAB — Teatro Municipal de Bragança.");
            fitab2018Gala.SetImage(null);

            var fitab2019Gala = Event.Create("XXI FITAB — Gala", new DateTime(2019, 5, 21), "Bragança", EventType.Festival, "Gala do XXI FITAB — Teatro Municipal de Bragança.");
            fitab2019Gala.SetImage(null);

            var fitab2024Noite = Event.Create("XXIV FITAB — Noite de Serenatas", new DateTime(2024, 5, 24), "Bragança", EventType.Festival, "XXIV FITAB — Noite de Serenatas.");
            fitab2024Noite.SetImage(null);
            var fitab2024Gala = Event.Create("XXIV FITAB — Gala", new DateTime(2024, 5, 25), "Bragança", EventType.Festival, "XXIV FITAB — Gala no TMB.");
            fitab2024Gala.SetImage(null);

            var fitab2025Noite = Event.Create("XXV FITAB — Noite de Serenatas", new DateTime(2025, 5, 2), "Bragança", EventType.Festival, "XXV FITAB — Noite de Serenatas na Praça de Camões.");
            fitab2025Noite.SetImage(null);
            var fitab2025Gala = Event.Create("XXV FITAB — Gala", new DateTime(2025, 5, 3), "Bragança", EventType.Festival, "XXV FITAB — Gala no TMB.");
            fitab2025Gala.SetImage(null);

            var barracaSRC19 = Event.Create("Barraca RTUB — SRC'19", new DateTime(2019, 10, 23), "Bragança (NERBA)", EventType.Nerba, "Semana da Receção ao Caloiro 2019 — Barraca RTUB.");
            var src2022 = Event.Create("Semana da Receção ao Caloiro 2022", new DateTime(2022, 11, 8), "Bragança (NERBA)", EventType.Nerba, "Receção ao Caloiro 2022 — atuação RTUB.");
            var rtona2025 = Event.Create("RTÔNA (RTUB + Tôna Tuna)", new DateTime(2025, 10, 8), "Bragança", EventType.Convivio, "Arraial conjunto RTUB + Tôna Tuna.");

            events.AddRange(new[]
            {
                fitab2018Noite, fitab2018Gala, fitab2019Gala,
                fitab2024Noite, fitab2024Gala, fitab2025Noite, fitab2025Gala,
                barracaSRC19, src2022, rtona2025
            });

            // --- CANTAR OS REIS (2015–2026) ---
            events.AddRange(new[]
            {
                Event.Create("Cantar os Reis 2015", new DateTime(2015, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2016", new DateTime(2016, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2017", new DateTime(2017, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2018", new DateTime(2018, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2019", new DateTime(2019, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2020", new DateTime(2020, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2021", new DateTime(2021, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2022", new DateTime(2022, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2023", new DateTime(2023, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2024", new DateTime(2024, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2025", new DateTime(2025, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade."),
                Event.Create("Cantar os Reis 2026", new DateTime(2026, 1, 6), "Bragança", EventType.Convivio, "Tradição de cantar os Reis pela cidade.")
            });

            // --- MAGUSTOS (2015–2024, 2026) — evita duplicar 2025 que já tens ---
            events.AddRange(new[]
            {
                Event.Create("Magusto 2015", new DateTime(2015, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2016", new DateTime(2016, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2017", new DateTime(2017, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2018", new DateTime(2018, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2019", new DateTime(2019, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2020", new DateTime(2020, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2021", new DateTime(2021, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2022", new DateTime(2022, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2023", new DateTime(2023, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2024", new DateTime(2024, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB."),
                Event.Create("Magusto 2026", new DateTime(2026, 11, 11), "Bragança", EventType.Convivio, "Castanhas, jeropiga e convívio RTUB.")
            });

            // --- ANIVERSÁRIOS (2015–2024, 2026) — 2025 já existe ---
            events.AddRange(new[]
            {
                Event.Create("24º Aniversário", new DateTime(2015, 12, 4), "Bragança", EventType.Convivio, "Celebração do 24º Aniversário da RTUB."),
                Event.Create("25º Aniversário", new DateTime(2016, 12, 4), "Bragança", EventType.Convivio, "Celebração do 25º Aniversário da RTUB."),
                Event.Create("26º Aniversário", new DateTime(2017, 12, 4), "Bragança", EventType.Convivio, "Celebração do 26º Aniversário da RTUB."),
                Event.Create("27º Aniversário", new DateTime(2018, 12, 4), "Bragança", EventType.Convivio, "Celebração do 27º Aniversário da RTUB."),
                Event.Create("28º Aniversário", new DateTime(2019, 12, 4), "Bragança", EventType.Convivio, "Celebração do 28º Aniversário da RTUB."),
                Event.Create("29º Aniversário", new DateTime(2020, 12, 4), "Bragança", EventType.Convivio, "Celebração do 29º Aniversário da RTUB."),
                Event.Create("30º Aniversário", new DateTime(2021, 12, 4), "Bragança", EventType.Convivio, "Celebração do 30º Aniversário da RTUB."),
                Event.Create("31º Aniversário", new DateTime(2022, 12, 4), "Bragança", EventType.Convivio, "Celebração do 31º Aniversário da RTUB."),
                Event.Create("32º Aniversário", new DateTime(2023, 12, 4), "Bragança", EventType.Convivio, "Celebração do 32º Aniversário da RTUB."),
                Event.Create("33º Aniversário", new DateTime(2024, 12, 4), "Bragança", EventType.Convivio, "Celebração do 33º Aniversário da RTUB."),
                Event.Create("35º Aniversário", new DateTime(2026, 12, 4), "Bragança", EventType.Convivio, "Celebração do 35º Aniversário da RTUB.")
            });

            // --- JANTARES/CONVÍVIOS DE NATAL (para preencher e variar anos) ---
            events.AddRange(new[]
            {
                Event.Create("Jantar de Natal 2016", new DateTime(2016, 12, 15), "Bragança", EventType.Convivio, "Convívio de Natal RTUB."),
                Event.Create("Jantar de Natal 2017", new DateTime(2017, 12, 15), "Bragança", EventType.Convivio, "Convívio de Natal RTUB."),
                Event.Create("Jantar de Natal 2018", new DateTime(2018, 12, 15), "Bragança", EventType.Convivio, "Convívio de Natal RTUB."),
                Event.Create("Jantar de Natal 2019", new DateTime(2019, 12, 15), "Bragança", EventType.Convivio, "Convívio de Natal RTUB."),
                Event.Create("Jantar de Natal 2020", new DateTime(2020, 12, 16), "Bragança", EventType.Convivio, "Convívio de Natal RTUB."),
                Event.Create("Jantar de Natal 2021", new DateTime(2021, 12, 16), "Bragança", EventType.Convivio, "Convívio de Natal RTUB.")
            });

            // --- MAIS 2 SERENATAS/CONVÍVIOS SOLTOS PARA PASSAR 50+ ---
            events.AddRange(new[]
            {
                Event.Create("Serenata ao IPB", new DateTime(2021, 4, 23), "Bragança", EventType.Convivio, "Serenata académica no IPB."),
                Event.Create("Serenata à Cidade", new DateTime(2023, 5, 1), "Bragança", EventType.Convivio, "Serenata primaveril no centro histórico.")
            });

            await dbContext.Events.AddRangeAsync(events);
            await dbContext.SaveChangesAsync();
        }
    }

}