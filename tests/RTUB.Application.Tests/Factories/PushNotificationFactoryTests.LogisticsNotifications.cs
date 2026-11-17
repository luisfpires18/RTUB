using RTUB.Application.Interfaces;

namespace RTUB.Application.Tests.Factories;

// PushNotificationFactoryTests: Logistics Notifications tests.
public partial class PushNotificationFactoryTests
{
    [Fact]
    public void LogisticsNotifications_OpenTheCardOnItsBoard_WithTitleAndBoardOnly()
    {
        // Task 069: never the card's details; the link opens the card (/logistics/{board}?card={id}).
        foreach (var kind in Enum.GetValues<LogisticsNotice>())
        {
            var n = _factory.CreateLogisticsNotification(kind, 3, 41, "Comprar cordas", "FITAB 2027", "Zé", new DateTime(2027, 3, 1));
            Assert.Equal("/logistics/3?card=41", n.Url);
            Assert.Equal("logistics-card-41", n.Tag);
            Assert.Contains("«Comprar cordas» (FITAB 2027)", n.Body);
        }

        Assert.Equal("«Comprar cordas» (FITAB 2027) · prazo 01/03",
            _factory.CreateLogisticsNotification(LogisticsNotice.Reminder, 3, 41, "Comprar cordas", "FITAB 2027", dueDate: new DateTime(2027, 3, 1)).Body);
        Assert.Contains("Zé mencionou-te", _factory.CreateLogisticsNotification(LogisticsNotice.Mention, 3, 41, "x", "y", "Zé").Body);
        Assert.EndsWith("…» (y).", _factory.CreateLogisticsNotification(LogisticsNotice.Owner, 3, 41, new string('a', 200), "y").Body);
    }
}
