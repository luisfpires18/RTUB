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
        // Only seed if no rehearsals exist
        if (await dbContext.Rehearsals.AnyAsync())
            return;

        var rehearsals = new List<Rehearsal>();

        // Past rehearsals (last 12 weeks)
        var today = DateTime.Today;
        var startDate = today.AddDays(-84); // 12 weeks
        
        // Generate past rehearsals for Tuesdays and Thursdays
        for (var date = startDate; date < today; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Tuesday || date.DayOfWeek == DayOfWeek.Thursday)
            {
                var rehearsal = Rehearsal.Create(
                    date,
                    "Quinta de Santa Apolónia",
                    GetThemeForDate(date)
                );
                
                // Add some notes for certain rehearsals
                if (date.Day % 7 == 0)
                {
                    rehearsal.UpdateDetails(
                        rehearsal.Location,
                        rehearsal.Theme,
                        "Ensaio focado em repertório de Natal"
                    );
                }
                
                rehearsals.Add(rehearsal);
            }
        }

        // Future rehearsals (next 12 weeks)
        var endDate = today.AddDays(84); // 12 weeks
        for (var date = today; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Tuesday || date.DayOfWeek == DayOfWeek.Thursday)
            {
                var rehearsal = Rehearsal.Create(
                    date,
                    "Quinta de Santa Apolónia",
                    GetThemeForDate(date)
                );
                
                // Cancel one future rehearsal as an example
                if (date == today.AddDays(14) && date.DayOfWeek == DayOfWeek.Thursday)
                {
                    rehearsal.Cancel();
                    rehearsal.UpdateDetails(
                        rehearsal.Location,
                        rehearsal.Theme,
                        "Cancelado devido a feriado"
                    );
                }
                
                rehearsals.Add(rehearsal);
            }
        }

        dbContext.Rehearsals.AddRange(rehearsals);
        await dbContext.SaveChangesAsync();

        // Now seed attendance data for past rehearsals
        var members = await userManager.Users.AsNoTracking().ToListAsync();
        if (members.Count == 0)
            return;

        var attendances = new List<RehearsalAttendance>();
        var random = new Random(42); // Fixed seed for reproducibility

        // Only add attendance for past rehearsals
        var pastRehearsals = rehearsals.Where(r => r.Date < today).ToList();
        
        foreach (var rehearsal in pastRehearsals)
        {
            // Randomly select 60-80% of members to attend each rehearsal
            var attendanceRate = 0.6 + (random.NextDouble() * 0.2); // 60-80%
            var attendingCount = (int)(members.Count * attendanceRate);
            
            var attendingMembers = members
                .OrderBy(x => random.Next())
                .Take(attendingCount)
                .ToList();

            foreach (var member in attendingMembers)
            {
                var attendance = RehearsalAttendance.Create(
                    rehearsal.Id,
                    member.Id,
                    member.MainInstrument
                );
                
                // Approve most attendances (simulate admin approval for past rehearsals)
                // 5% remain pending as examples
                if (random.NextDouble() > 0.05) // 95% approval rate
                {
                    attendance.MarkAttendance(true); // Approved by admin
                }
                // else: remains false (pending approval)
                
                attendances.Add(attendance);
            }
        }

        dbContext.RehearsalAttendances.AddRange(attendances);
        await dbContext.SaveChangesAsync();
    }

    private static string? GetThemeForDate(DateTime date)
    {
        // Assign themes based on the time of year
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
            return null; // Some rehearsals without specific theme
    }
}
