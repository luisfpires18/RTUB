using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    /// <summary>
    /// Seed enrollments so each event has a realistic roster derived from the seeded members.
    /// The logic keeps a core line-up per instrument and mixes in extra participants deterministically.
    /// </summary>
    public static async Task SeedEnrollmentsAsync(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        if (await dbContext.Enrollments.AnyAsync())
            return;

        var events = await dbContext.Events
            .AsNoTracking()
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .ToListAsync();
        if (events.Count == 0)
            return;

        // Get all members for older events and active members for recent events
        var allMembers = await userManager.Users
            .AsNoTracking()
            .OrderBy(m => m.Id)
            .ToListAsync();
        if (allMembers.Count == 0)
            return;

        var activeMembers = allMembers.Where(m => !m.IsRetired).ToList();

        // Define threshold: 6 months ago (matches retirement logic)
        var today = DateTime.Today;
        var sixMonthsAgo = today.AddMonths(-6);

        var enrollments = new List<Enrollment>();
        var assigned = new HashSet<string>();

        void AddEnrollment(ApplicationUser user, Event evt, Random random)
        {
            var key = $"{user.Id}:{evt.Id}";
            if (!assigned.Add(key))
                return;

            var enrollment = Enrollment.Create(user.Id, evt.Id);
            // Note: Instrument will be set from MemberInstruments table by the application
            enrollment.Instrument = null;

            if (random.NextDouble() < 0.1)
            {
                enrollment.WillAttend = false;
                enrollment.Notes = "Indisponível por motivos pessoais";
            }
            else if (random.NextDouble() < 0.2)
            {
                enrollment.Notes = "Disponível para atuar";
            }

            enrollments.Add(enrollment);
        }

        foreach (var evt in events)
        {
            // Use all members for old events, active members only for recent ones
            var membersPool = evt.Date < sixMonthsAgo ? allMembers : activeMembers;

            // Create a general fallback queue for the current event
            var generalFallback = new Queue<ApplicationUser>(membersPool.OrderBy(m => m.Id));

            ApplicationUser CycleQueue(Queue<ApplicationUser> queue)
            {
                if (queue.Count == 0)
                {
                    // If queue is empty, cycle from generalFallback
                    if (generalFallback.Count == 0)
                        generalFallback = new Queue<ApplicationUser>(membersPool.OrderBy(m => m.Id));

                    var user = generalFallback.Dequeue();
                    generalFallback.Enqueue(user);
                    return user;
                }

                var u = queue.Dequeue();
                queue.Enqueue(u);
                return u;
            }

            int seed = (int)((((long)evt.Id * 73856093L) ^ evt.Date.DayOfYear ^ (int)evt.Type) & 0x7FFFFFFF);
            if (seed == 0)
            {
                seed = evt.Id + 1;
            }

            var random = new Random(seed);

            var coreRequirements = evt.Type switch
            {
                EventType.Festival => new Dictionary<InstrumentType, int>
                {
                    { InstrumentType.Guitarra, 3 },
                    { InstrumentType.Bandolim, 2 },
                    { InstrumentType.Baixo, 1 },
                    { InstrumentType.Percussao, 1 },
                    { InstrumentType.Pandeireta, 1 },
                    { InstrumentType.Estandarte, 1 },
                    { InstrumentType.Flauta, 1 },
                    { InstrumentType.Cavaquinho, 1 },
                    { InstrumentType.Acordeao, 1 }
                },
                EventType.Nerba => new Dictionary<InstrumentType, int>
                {
                    { InstrumentType.Guitarra, 3 },
                    { InstrumentType.Bandolim, 2 },
                    { InstrumentType.Baixo, 1 },
                    { InstrumentType.Percussao, 1 },
                    { InstrumentType.Pandeireta, 2 },
                    { InstrumentType.Estandarte, 1 }
                },
                _ => new Dictionary<InstrumentType, int>
                {
                    { InstrumentType.Guitarra, 2 },
                    { InstrumentType.Bandolim, 1 },
                    { InstrumentType.Baixo, 1 },
                    { InstrumentType.Percussao, 1 },
                    { InstrumentType.Pandeireta, 1 },
                    { InstrumentType.Estandarte, 1 }
                }
            };

            // Assign members based on requirements (simplified - no longer using instrument-based assignment)
            foreach (var requirement in coreRequirements)
            {
                for (var i = 0; i < requirement.Value; i++)
                {
                    var user = CycleQueue(generalFallback);
                    AddEnrollment(user, evt, random);
                }
            }

            double extraRate = evt.Type switch
            {
                EventType.Festival => 0.65,
                EventType.Nerba => 0.55,
                _ => 0.4
            };

            // Add extra members randomly
            foreach (var member in membersPool)
            {
                if (assigned.Contains($"{member.Id}:{evt.Id}"))
                    continue;

                if (random.NextDouble() <= extraRate)
                {
                    AddEnrollment(member, evt, random);
                }
            }
        }

        if (enrollments.Count > 0)
        {
            await dbContext.Enrollments.AddRangeAsync(enrollments);
            await dbContext.SaveChangesAsync();
        }
    }
}
