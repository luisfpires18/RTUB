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
/// Seeds the database with finance transactions and reports.
/// </summary>
public static partial class SeedData
{
    public static async Task SeedFinanceAsync(ApplicationDbContext dbContext)
    {
        // Seed additional sample data for Activities, Transactions, Reports
        if (!await dbContext.Reports.AnyAsync())
        {
            var currentYear = DateTime.Now.Year;

            // Two fiscal years (current and previous) to make reports richer
            var fyPrev = FiscalYear.Create(currentYear - 1, currentYear);
            var fyCurr = FiscalYear.Create(currentYear, currentYear + 1);
            await dbContext.FiscalYears.AddRangeAsync(new[] { fyPrev, fyCurr });
            await dbContext.SaveChangesAsync();

            // Reports
            var reportPrev = Report.Create($"Relatório Financeiro {currentYear - 1}", currentYear - 1,
                "Relatório de atividades e transações do ano anterior");
            var reportCurr = Report.Create($"Relatório Financeiro {currentYear}", currentYear,
                "Relatório de atividades e transações do ano corrente");
            await dbContext.Reports.AddRangeAsync(new[] { reportPrev, reportCurr });
            await dbContext.SaveChangesAsync();

            // Activities (grouped by report)
            var aFitab = Activity.Create(reportCurr.Id, "FITAB", "Festival Internacional de Tunas Académicas de Bragança");
            var aSRC = Activity.Create(reportCurr.Id, "Receção ao Caloiro (NERBA)", "Atuações e barraca");
            var aSerenatas = Activity.Create(reportCurr.Id, "Serenatas e Atuações", "Casamentos, jantares, corporativos");
            var aAnivers = Activity.Create(reportCurr.Id, "34º Aniversário", "Celebração interna e logística");
            var aRTona = Activity.Create(reportCurr.Id, "RTÔNA (RTUB + Tôna Tuna)", "Arraial e convívio");
            var aMerch = Activity.Create(reportCurr.Id, "Merchandising", "Venda de t-shirts, pins e capotes");
            var aReparos = Activity.Create(reportCurr.Id, "Instrumentos e Reparações", "Compras e manutenções");
            var aGerais = Activity.Create(reportCurr.Id, "Despesas Gerais", "Manutenção e operações");
            var aFormacao = Activity.Create(reportCurr.Id, "Formação e Workshops", "Aulas internas e clínicas");
            var aViagens = Activity.Create(reportPrev.Id, "Viagens e Deslocações", "Deslocações do ano anterior");

            await dbContext.Activities.AddRangeAsync(new[]
            {
            aFitab, aSRC, aSerenatas, aAnivers, aRTona, aMerch, aReparos, aGerais, aFormacao, aViagens
        });
            await dbContext.SaveChangesAsync();

            // Transactions
            var tx = new List<Transaction>
        {
            // FITAB (current year)
            Transaction.Create(new DateTime(currentYear, 5, 15), "Receita de bilheteira FITAB", "Receitas", 5500m, "Income", aFitab.Id),
            Transaction.Create(new DateTime(currentYear, 5, 16), "Patrocínios FITAB", "Patrocínios", 2500m, "Income", aFitab.Id),
            Transaction.Create(new DateTime(currentYear, 5, 10), "Som e luz", "Técnica", 1600m, "Expense", aFitab.Id),
            Transaction.Create(new DateTime(currentYear, 5, 12), "Backline adicional", "Técnica", 500m, "Expense", aFitab.Id),
            Transaction.Create(new DateTime(currentYear, 5, 14), "Marketing e cartazes", "Marketing", 900m, "Expense", aFitab.Id),
            Transaction.Create(new DateTime(currentYear, 5, 17), "Seguro do evento", "Seguros", 220m, "Expense", aFitab.Id),

            // SRC (NERBA)
            Transaction.Create(new DateTime(currentYear, 10, 30), "Receita barraca", "Vendas", 3200m, "Income", aSRC.Id),
            Transaction.Create(new DateTime(currentYear, 10, 31), "Taxa stand NERBA", "Taxas", 300m, "Expense", aSRC.Id),
            Transaction.Create(new DateTime(currentYear, 11, 1), "Gelo e bebidas", "Consumos", 750m, "Expense", aSRC.Id),
            Transaction.Create(new DateTime(currentYear, 11, 2), "Reposição de stock", "Consumos", 500m, "Expense", aSRC.Id),

            // Serenatas & atuações
            Transaction.Create(new DateTime(currentYear, 2, 14), "Serenata São Valentim", "Receitas", 250m, "Income", aSerenatas.Id),
            Transaction.Create(new DateTime(currentYear, 3, 20), "Casamento — Quinta da Serra", "Receitas", 600m, "Income", aSerenatas.Id),
            Transaction.Create(new DateTime(currentYear, 4, 18), "Evento corporativo — Porto", "Receitas", 350m, "Income", aSerenatas.Id),
            Transaction.Create(new DateTime(currentYear, 4, 17), "Transporte para atuação", "Transporte", 140m, "Expense", aSerenatas.Id),
            Transaction.Create(new DateTime(currentYear, 6, 1), "Casamento — Mirandela", "Receitas", 500m, "Income", aSerenatas.Id),
            Transaction.Create(new DateTime(currentYear, 6, 1), "Gasóleo — Mirandela", "Transporte", 90m, "Expense", aSerenatas.Id),

            // 34º Aniversário
            Transaction.Create(new DateTime(currentYear, 12, 4), "Bilheteira jantar", "Receitas", 800m, "Income", aAnivers.Id),
            Transaction.Create(new DateTime(currentYear, 12, 4), "Catering", "Alimentação", 520m, "Expense", aAnivers.Id),
            Transaction.Create(new DateTime(currentYear, 12, 4), "Decoração", "Materiais", 120m, "Expense", aAnivers.Id),

            // RTÔNA
            Transaction.Create(new DateTime(currentYear, 10, 8), "Arrecadação de bebidas", "Vendas", 900m, "Income", aRTona.Id),
            Transaction.Create(new DateTime(currentYear, 10, 8), "Licença de ruído", "Licenças", 70m, "Expense", aRTona.Id),
            Transaction.Create(new DateTime(currentYear, 10, 8), "Segurança privada", "Serviços", 180m, "Expense", aRTona.Id),

            // Merchandising
            Transaction.Create(new DateTime(currentYear, 3, 5), "Venda t-shirts", "Vendas", 600m, "Income", aMerch.Id),
            Transaction.Create(new DateTime(currentYear, 3, 1), "Produção t-shirts", "Produção", 420m, "Expense", aMerch.Id),
            Transaction.Create(new DateTime(currentYear, 4, 2), "Venda pins", "Vendas", 150m, "Income", aMerch.Id),
            Transaction.Create(new DateTime(currentYear, 4, 1), "Compra pins", "Produção", 60m, "Expense", aMerch.Id),

            // Instrumentos & reparações
            Transaction.Create(new DateTime(currentYear, 1, 10), "Cordas de guitarra", "Manutenção", 80m, "Expense", aReparos.Id),
            Transaction.Create(new DateTime(currentYear, 2, 5), "Revisão acordeão", "Manutenção", 120m, "Expense", aReparos.Id),
            Transaction.Create(new DateTime(currentYear, 7, 15), "Compra de pandeiretas", "Equipamento", 180m, "Expense", aReparos.Id),

            // Despesas gerais
            Transaction.Create(new DateTime(currentYear, 1, 5),  "Quotas de membros", "Quotas", 1100m, "Income", aGerais.Id),
            Transaction.Create(new DateTime(currentYear, 2, 5),  "Materiais de limpeza", "Operação", 45m, "Expense", aGerais.Id),
            Transaction.Create(new DateTime(currentYear, 1, 25), "Alojamento — convidados", "Hospedagem", 260m, "Expense", aGerais.Id),

            // Formação
            Transaction.Create(new DateTime(currentYear, 3, 12), "Workshop de direção musical", "Inscrições", 0m, "Income", aFormacao.Id),
            Transaction.Create(new DateTime(currentYear, 3, 12), "Cachê formador", "Serviços", 200m, "Expense", aFormacao.Id),
            Transaction.Create(new DateTime(currentYear, 3, 12), "Coffee-break", "Alimentação", 60m, "Expense", aFormacao.Id),

            // Viagens (previous year)
            Transaction.Create(new DateTime(currentYear - 1, 11, 20), "Gasóleo — viagem a festival", "Transporte", 160m, "Expense", aViagens.Id),
            Transaction.Create(new DateTime(currentYear - 1, 11, 21), "Portagens", "Transporte", 35m, "Expense", aViagens.Id),
            Transaction.Create(new DateTime(currentYear - 1, 11, 22), "Subsídio deslocação (doação)", "Doações", 300m, "Income", aViagens.Id),
        };

            await dbContext.Transactions.AddRangeAsync(tx);
            await dbContext.SaveChangesAsync();

            // Recalculate per-activity
            var allActivities = await dbContext.Activities
                .Include(a => a.Transactions)
                .Where(a => a.ReportId == reportCurr.Id || a.ReportId == reportPrev.Id)
                .ToListAsync();

            foreach (var a in allActivities)
                a.RecalculateFinancials();

            await dbContext.SaveChangesAsync();

            // Update each report totals
            foreach (var rep in new[] { reportPrev, reportCurr })
            {
                var repActs = allActivities.Where(a => a.ReportId == rep.Id).ToList();
                var totalIncome = repActs.Sum(a => a.TotalIncome);
                var totalExpenses = repActs.Sum(a => a.TotalExpenses);
                rep.UpdateFinancials(totalIncome, totalExpenses);
            }

            await dbContext.SaveChangesAsync();
        }
    }
}