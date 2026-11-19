using FluentAssertions;
using RTUB.Web.EmailTemplates.Models;

namespace RTUB.Web.Tests.Services;

/// <summary>
/// Tests for email template rendering - validates models and expected content structure
/// </summary>
public partial class EmailTemplateTests
{
    [Fact]
    public void EventNotificationModel_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var model = new EventNotificationModel
        {
            EventTitle = "Festa de São João",
            StartDate = new DateTime(2025, 6, 23),
            EventLocation = "Ribeira do Porto",
            EventLink = "https://rtub.azurewebsites.net/events/1",
            EventDescription = "Vamos celebrar o São João com música e dança tradicional!",
            PreferencesLink = "https://rtub.azurewebsites.net/profile"
        };

        // Assert
        model.EventTitle.Should().Be("Festa de São João");
        model.StartDate.Should().Be(new DateTime(2025, 6, 23));
        model.EventLocation.Should().Be("Ribeira do Porto");
        model.EventLink.Should().Be("https://rtub.azurewebsites.net/events/1");
        model.EventDescription.Should().Be("Vamos celebrar o São João com música e dança tradicional!");
        model.PreferencesLink.Should().Be("https://rtub.azurewebsites.net/profile");
    }

    [Fact]
    public void NewRequestNotificationModel_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var model = new NewRequestNotificationModel
        {
            RequestName = "João Silva",
            RequestEmail = "joao@example.com",
            Phone = "+351 912 345 678",
            EventType = "Casamento",
            DateInfo = "15 de Agosto de 2025",
            Location = "Quinta do Vale",
            Message = "Gostaria de contratar o rancho para animar o nosso casamento.",
            CreatedAt = new DateTime(2025, 6, 1, 10, 30, 0),
            RequestUrl = "https://rtub.azurewebsites.net/requests/123",
            Priority = "Alta"
        };

        // Assert
        model.RequestName.Should().Be("João Silva");
        model.RequestEmail.Should().Be("joao@example.com");
        model.Phone.Should().Be("+351 912 345 678");
        model.EventType.Should().Be("Casamento");
        model.DateInfo.Should().Be("15 de Agosto de 2025");
        model.Location.Should().Be("Quinta do Vale");
        model.Message.Should().Be("Gostaria de contratar o rancho para animar o nosso casamento.");
        model.CreatedAt.Should().Be(new DateTime(2025, 6, 1, 10, 30, 0));
        model.RequestUrl.Should().Be("https://rtub.azurewebsites.net/requests/123");
        model.Priority.Should().Be("Alta");
    }

    [Fact]
    public void PasswordResetModel_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var model = new PasswordResetModel
        {
            CallbackUrl = "https://rtub.azurewebsites.net/reset-password?token=abc123"
        };

        // Assert
        model.CallbackUrl.Should().Be("https://rtub.azurewebsites.net/reset-password?token=abc123");
    }

    [Fact]
    public void WelcomeEmailModel_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var password = TestSecret.NewPassword();
        var model = new WelcomeEmailModel
        {
            UserName = "luisfpires",
            FullName = "Luís Pires",
            Password = password,
            DashboardUrl = "https://rtub.azurewebsites.net/",
            ProfileUrl = "https://rtub.azurewebsites.net/profile",
            EventsUrl = "https://rtub.azurewebsites.net/events",
        };

        // Assert
        model.UserName.Should().Be("luisfpires");
        model.FullName.Should().Be("Luís Pires");
        model.Password.Should().Be(password);
        model.DashboardUrl.Should().Be("https://rtub.azurewebsites.net/");
        model.ProfileUrl.Should().Be("https://rtub.azurewebsites.net/profile");
        model.EventsUrl.Should().Be("https://rtub.azurewebsites.net/events");
    }

    [Fact]
    public void ButtonModel_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var model = new ButtonModel
        {
            Href = "https://example.com",
            Label = "Clique aqui",
            BgColor = "#6E56CF",
            TextColor = "#FFFFFF",
            IsSecondary = false
        };

        // Assert
        model.Href.Should().Be("https://example.com");
        model.Label.Should().Be("Clique aqui");
        model.BgColor.Should().Be("#6E56CF");
        model.TextColor.Should().Be("#FFFFFF");
        model.IsSecondary.Should().BeFalse();
    }

    [Fact]
    public void EventNotificationModel_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var model = new EventNotificationModel();

        // Assert
        model.EventTitle.Should().Be(string.Empty);
        model.StartDate.Should().Be(default(DateTime));
        model.EventLocation.Should().Be(string.Empty);
        model.EventLink.Should().Be(string.Empty);
        model.EventDescription.Should().Be(string.Empty);
        model.PreferencesLink.Should().Be("https://rtub.azurewebsites.net/profile");
    }

    [Fact]
    public void NewRequestNotificationModel_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var model = new NewRequestNotificationModel();

        // Assert
        model.RequestName.Should().Be(string.Empty);
        model.RequestEmail.Should().Be(string.Empty);
        model.Phone.Should().Be(string.Empty);
        model.EventType.Should().Be(string.Empty);
        model.DateInfo.Should().Be(string.Empty);
        model.Location.Should().Be(string.Empty);
        model.Message.Should().Be(string.Empty);
        model.RequestUrl.Should().Be("https://rtub.azurewebsites.net/requests");
        model.Priority.Should().Be("Normal");
        model.CreatedAt.Should().Be(default(DateTime));
    }

    [Fact]
    public void WelcomeEmailModel_ShouldHaveDefaultUrlValues()
    {
        // Arrange & Act
        var model = new WelcomeEmailModel();

        // Assert
        model.DashboardUrl.Should().Be("https://rtub.azurewebsites.net/");
        model.ProfileUrl.Should().Be("https://rtub.azurewebsites.net/profile");
        model.EventsUrl.Should().Be("https://rtub.azurewebsites.net/events");
    }

    [Fact]
    public void ButtonModel_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var model = new ButtonModel();

        // Assert
        model.Href.Should().Be("#");
        model.Label.Should().Be("Clique aqui");
        model.BgColor.Should().Be("#6E56CF");
        model.TextColor.Should().Be("#FFFFFF");
        model.IsSecondary.Should().BeFalse();
    }
}
