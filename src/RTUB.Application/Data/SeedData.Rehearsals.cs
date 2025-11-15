using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the database with sample rehearsal data
/// </summary>
public static partial class SeedData
{
    public static async Task SeedRehearsalsAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        if (await dbContext.Rehearsals.AnyAsync())
            return;

        var today = DateTime.Today;
        var startDate = today.AddDays(-84);
        var endDate = today.AddDays(84);

        var rehearsalsToCreate = new List<Rehearsal>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Tuesday && date.DayOfWeek != DayOfWeek.Thursday)
            {
                continue;
            }

            var rehearsal = Rehearsal.Create(
                date,
                "Quinta de Santa Apolónia",
                GetThemeForDate(date)
            );

            if (date < today && date.Day % 7 == 0)
            {
                rehearsal.UpdateDetails(
                    rehearsal.Location,
                    rehearsal.Theme,
                    "Ensaio focado em repertório de Natal"
                );
            }

            if (date > today && date == today.AddDays(14) && date.DayOfWeek == DayOfWeek.Thursday)
            {
                rehearsal.Cancel();
                rehearsal.UpdateDetails(
                    rehearsal.Location,
                    rehearsal.Theme,
                    "Cancelado devido a feriado"
                );
            }

            rehearsalsToCreate.Add(rehearsal);
        }

        if (rehearsalsToCreate.Count == 0)
            return;

        await dbContext.Rehearsals.AddRangeAsync(rehearsalsToCreate);
        await dbContext.SaveChangesAsync();

        var members = await userManager.Users
            .AsNoTracking()
            .OrderBy(m => m.Id)
            .ToListAsync();
        if (members.Count == 0)
            return;

        var pastRehearsals = await dbContext.Rehearsals
            .AsNoTracking()
            .Where(r => r.Date < today)
            .OrderBy(r => r.Date)
            .ToListAsync();
        if (pastRehearsals.Count == 0)
            return;

        var attendances = new List<RehearsalAttendance>();

        foreach (var rehearsal in pastRehearsals)
        {
            var seed = (int)((((long)rehearsal.Id * 92821L) ^ rehearsal.Date.DayOfYear) & 0x7FFFFFFF);
            if (seed == 0)
            {
                seed = rehearsal.Id + 7;
            }

            var random = new Random(seed);

            var attendanceRate = 0.6 + (random.NextDouble() * 0.2);
            var attendingCount = Math.Clamp((int)Math.Round(members.Count * attendanceRate), 3, members.Count);

            var attendingMembers = members
                .OrderBy(_ => random.Next())
                .Take(attendingCount)
                .ToList();

            foreach (var member in attendingMembers)
            {
                var attendance = RehearsalAttendance.Create(
                    rehearsal.Id,
                    member.Id,
                    member.MainInstrument
                );

                var willAttend = random.NextDouble() > 0.08;
                attendance.WillAttend = willAttend;

                if (willAttend && random.NextDouble() > 0.1)
                {
                    attendance.MarkAttendance(true);
                }

                if (random.NextDouble() < 0.15)
                {
                    attendance.Notes = "Chegou alguns minutos atrasado";
                }
                else if (random.NextDouble() < 0.15)
                {
                    attendance.Notes = "Participou na preparação do repertório";
                }

                attendances.Add(attendance);
            }
        }

        if (attendances.Count > 0)
        {
            await dbContext.RehearsalAttendances.AddRangeAsync(attendances);
            await dbContext.SaveChangesAsync();
        }
    }

    private static string? GetThemeForDate(DateTime date)
    {
        var month = date.Month;
        var day = date.Day;

        if (month == 12 || (month == 11 && day > 20))
            return "Repertório de Natal";
        else if (month == 1 && day <= 10)
            return "Cantar os Reis";
        else if (month == 10 || month == 11)
            return "Preparação NERBA";
        else if (month >= 4 && month <= 6)
            return "Preparação Festivais";
        else if (day % 3 == 0)
            return "Prática de Fado";
        else if (day % 5 == 0)
            return "Repertório Tradicional";
        else
            return null;
    }
}
