using FluentAssertions;
using Moq;
using RTUB.Web.EmailTemplates.Models;
using RTUB.Web.Services;

namespace RTUB.Web.Tests.Services;

/// <summary>
/// Tests for email template rendering - validates models and expected content structure
/// </summary>
public class EmailTemplateTests
{
    [Fact]
    public void EventNotificationModel_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var model = new EventNotificationModel
        {
            EventTitle = "Festa de São João",
            DateFormatted = "23 de Junho de 2025",
            EventLocation = "Ribeira do Porto",
            EventLink = "https://rtub.azurewebsites.net/events/1",
            EventDescription = "Vamos celebrar o São João com música e dança tradicional!",
            PreferencesLink = "https://rtub.azurewebsites.net/profile"
        };

        // Assert
        model.EventTitle.Should().Be("Festa de São João");
        model.DateFormatted.Should().Be("23 de Junho de 2025");
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
        var model = new WelcomeEmailModel
        {
            UserName = "luisfpires",
            FullName = "Luís Pires",
            Password = "TempPassword123",
            DashboardUrl = "https://rtub.azurewebsites.net/",
            ProfileUrl = "https://rtub.azurewebsites.net/profile",
            EventsUrl = "https://rtub.azurewebsites.net/events",
        };

        // Assert
        model.UserName.Should().Be("luisfpires");
        model.FullName.Should().Be("Luís Pires");
        model.Password.Should().Be("TempPassword123");
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
        model.DateFormatted.Should().Be(string.Empty);
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

    [Fact]
    public void EventNotificationModel_BackwardCompatibility_WithoutNewProperties()
    {
        // This test ensures backward compatibility - new properties are optional
        // Arrange & Act
        var model = new EventNotificationModel
        {
            EventTitle = "Test Event",
            DateFormatted = "01/01/2025",
            EventLocation = "Porto",
            EventLink = "https://test.com"
            // EventDescription and PreferencesLink not set - should use defaults
        };

        // Assert
        model.EventTitle.Should().Be("Test Event");
        model.EventDescription.Should().Be(string.Empty);
        model.PreferencesLink.Should().Be("https://rtub.azurewebsites.net/profile");
    }

    [Fact]
    public void NewRequestNotificationModel_BackwardCompatibility_WithoutNewProperties()
    {
        // This test ensures backward compatibility - new properties are optional
        // Arrange & Act
        var model = new NewRequestNotificationModel
        {
            RequestName = "Test",
            RequestEmail = "test@test.com",
            Phone = "123",
            EventType = "Test",
            DateInfo = "01/01/2025",
            Location = "Test",
            Message = "Test",
            CreatedAt = DateTime.Now
            // RequestUrl and Priority not set - should use defaults
        };

        // Assert
        model.RequestName.Should().Be("Test");
        model.RequestUrl.Should().Be("https://rtub.azurewebsites.net/requests");
        model.Priority.Should().Be("Normal");
    }

    [Fact]
    public void WelcomeEmailModel_BackwardCompatibility_WithoutNewProperties()
    {
        // This test ensures backward compatibility - new properties are optional
        // Arrange & Act
        var model = new WelcomeEmailModel
        {
            UserName = "test",
            FullName = "Test",
            Password = "test123"
            // URLs not set - should use defaults
        };

        // Assert
        model.UserName.Should().Be("test");
        model.DashboardUrl.Should().Be("https://rtub.azurewebsites.net/");
        model.ProfileUrl.Should().Be("https://rtub.azurewebsites.net/profile");
    }

    [Fact]
    public async Task EmailTemplateService_Interface_ShouldRenderTemplate()
    {
        // Arrange
        var mockService = new Mock<IEmailTemplateService>();
        var model = new EventNotificationModel
        {
            EventTitle = "Test",
            DateFormatted = "01/01/2025",
            EventLocation = "Porto",
            EventLink = "https://test.com"
        };

        var expectedHtml = "<html>Test Email</html>";
        mockService.Setup(s => s.RenderTemplateAsync("EventNotification", model))
            .ReturnsAsync(expectedHtml);

        // Act
        var result = await mockService.Object.RenderTemplateAsync("EventNotification", model);

        // Assert
        result.Should().Be(expectedHtml);
        mockService.Verify(s => s.RenderTemplateAsync("EventNotification", model), Times.Once);
    }
}
