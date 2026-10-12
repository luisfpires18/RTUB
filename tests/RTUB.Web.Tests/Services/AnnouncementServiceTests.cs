using FluentAssertions;
using RTUB.Web.Services;
using Xunit;

namespace RTUB.Web.Tests.Services;

/// <summary>
/// <see cref="AnnouncementService"/> (task 037): the latest announcement, handed out to the React shell's poll while it
/// is younger than <see cref="AnnouncementService.Lifetime"/>. The clock is a fake, so nothing waits on wall-clock time.
/// </summary>
public class AnnouncementServiceTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly DateTimeOffset Start = new(2026, 10, 12, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NothingSent_HandsOutNothing()
    {
        new AnnouncementService(new Clock(Start)).Current.Should().BeNull();
    }

    [Fact]
    public void ASentAnnouncement_IsHandedOutUntilItsLifetimeEnds()
    {
        var clock = new Clock(Start);
        var service = new AnnouncementService(clock);

        service.Broadcast("O servidor vai reiniciar.");

        service.Current.Should().Be(new AnnouncementService.Announcement(Start.ToUnixTimeMilliseconds(), "O servidor vai reiniciar."));
        clock.Now = Start + AnnouncementService.Lifetime - TimeSpan.FromSeconds(1);
        service.Current.Should().NotBeNull();
        clock.Now = Start + AnnouncementService.Lifetime;
        service.Current.Should().BeNull("an announcement is for whoever is online around the time it is sent");
    }

    [Fact]
    public void ANewAnnouncement_ReplacesTheLastOne_WithANewId()
    {
        var clock = new Clock(Start);
        var service = new AnnouncementService(clock);
        service.Broadcast("Primeiro.");
        var first = service.Current!;

        clock.Now = Start.AddMinutes(1);
        service.Broadcast("Segundo.");

        service.Current!.Message.Should().Be("Segundo.");
        service.Current.Id.Should().BeGreaterThan(first.Id, "a page that dismissed the first must still show the second");
    }
}
