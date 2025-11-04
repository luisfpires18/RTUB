using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the database with requests.
/// </summary>
public static partial class SeedData
{
    public static async Task SeedRequestsAsync(ApplicationDbContext dbContext)
    {
        // Seed sample requests from public
        if (!await dbContext.Requests.AnyAsync())
        {
            var reqs = new List<Request>
        {
            Request.Create("João Silva", "joao.silva@example.com", "919111222",
                "Casamento", new DateTime(2025, 6, 15), "Bragança",
                "Gostaria de contratar a RTUB para uma serenata no meu casamento. Podem contactar-me para mais detalhes?"),

            Request.Create("Maria Santos", "maria.santos@example.com", "918222333",
                "Atuação", new DateTime(2025, 7, 20), "Porto",
                "Empresa procura tuna para animar evento de final de ano. Orçamento disponível."),

            Request.Create("Ricardo Cunha", "ricardo.cunha@example.com", "912345678",
                "Aniversário", new DateTime(2025, 3, 28), "Vila Real",
                "Surpresa de aniversário (40 anos) — 3 a 4 músicas, estilo serenata."),

            Request.Create("Sofia Almeida", "sofia.almeida@example.com", "936001122",
                "Queima das Fitas", new DateTime(2025, 5, 8), "Coimbra",
                "Animação de receção antes do cortejo."),

            Request.Create("Tiago Pereira", "tiago.pereira@example.com", "910223344",
                "Receção ao Caloiro", new DateTime(2025, 10, 30), "Bragança",
                "Abertura do palco NERBA, 30 minutos."),

            Request.Create("Ana Lopes", "ana.lopes@example.com", "917556677",
                "Casamento", new DateTime(2026, 5, 17), "Guimarães",
                "Serenata ao fim da tarde na igreja."),

            Request.Create("Miguel Tavares", "miguel.tavares@example.com", "914445555",
                "Evento Corporativo", new DateTime(2025, 12, 12), "Porto",
                "Festa de Natal da empresa, 20-30 min de atuação."),

            Request.Create("Carla Ribeiro", "carla.ribeiro@example.com", "932220000",
                "Gala Solidária", new DateTime(2025, 11, 10), "Bragança",
                "Evento de angariação de fundos. Procura-se atuação pro bono."),

            Request.Create("Hugo Marques", "hugo.marques@example.com", "915330909",
                "Serenata", new DateTime(2025, 2, 14), "Bragança",
                "Pedido de São Valentim, 2 músicas junto ao castelo."),

            Request.Create("Beatriz Faria", "beatriz.faria@example.com", "934556677",
                "Batizado", new DateTime(2025, 9, 14), "Chaves",
                "Pequena atuação à saída da cerimónia."),

            Request.Create("Jorge Teixeira", "jorge.tx@example.com", "939112233",
                "FITAB", new DateTime(2025, 5, 3), "Bragança",
                "Confirmação de logística para a gala."),

            Request.Create("Rita Nunes", "rita.nunes@example.com", "918000111",
                "Arraial Académico", new DateTime(2025, 4, 12), "Braga",
                "Atuação de 25 minutos no palco principal."),

            Request.Create("Paula Martins", "paula.martins@example.com", "967223344",
                "Pedido Surpresa", new DateTime(2025, 6, 1), "Mirandela",
                "Serenata surpresa para pedido de casamento."),

            Request.Create("Diogo Rodrigues", "diogo.rod@example.com", "968112233",
                "Inauguração", new DateTime(2025, 3, 7), "Viana do Castelo",
                "Abertura de nova residência universitária."),

            Request.Create("Andreia Sousa", "andreia.sousa@example.com", "966778899",
                "Festa de Finalistas", new DateTime(2026, 6, 22), "Aveiro",
                "Atuação após entrega de diplomas."),

            Request.Create("Nuno Carvalho", "nuno.carvalho@example.com", "961234567",
                "Casamento", new DateTime(2025, 8, 2), "Vila do Conde",
                "Serenata junto ao rio, fim de tarde."),

            Request.Create("Helena Correia", "helena.correia@example.com", "965998877",
                "Jantar de Curso", new DateTime(2025, 10, 5), "Bragança",
                "Momento musical de 10 minutos entre pratos."),

            Request.Create("Pedro Lopes", "pedro.lopes@example.com", "962667788",
                "Festival Universitário", new DateTime(2025, 11, 22), "Guarda",
                "Tuna convidada para abertura.")
        };

            // Set some to "Analysing" (others stay with default status)
            foreach (var r in reqs.Where((_, i) => i % 3 == 0))
                r.UpdateStatus(RequestStatus.Analysing);

            await dbContext.Requests.AddRangeAsync(reqs);
            await dbContext.SaveChangesAsync();
        }
    }
}