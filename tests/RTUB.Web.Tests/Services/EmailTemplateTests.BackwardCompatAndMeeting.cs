// Email model backward compatibility, template service render and meeting notification model
using FluentAssertions;
using Moq;
using RTUB.Web.EmailTemplates.Models;
using RTUB.Web.Services;

namespace RTUB.Web.Tests.Services;

public partial class EmailTemplateTests
{
    [Fact]
    public void EventNotificationModel_BackwardCompatibility_WithoutNewProperties()
    {
        // This test ensures backward compatibility - new properties are optional
        // Arrange & Act
        var model = new EventNotificationModel
        {
            EventTitle = "Test Event",
            StartDate = new DateTime(2025, 1, 1),
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
            Password = TestSecret.NewPassword()
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
            StartDate = new DateTime(2025, 1, 1),
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

    [Fact]
    public void MeetingNotificationModel_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var model = new MeetingNotificationModel
        {
            MeetingType = "Assembleia Geral Ordinária",
            MeetingTitle = "AGO Janeiro 2025",
            DateFormatted = "quinta-feira, 15 de Janeiro de 2025 • 20:00",
            Location = "Sede do RTUB",
            Statement = "Esta reunião tem como objetivo discutir os temas importantes da organização.",
            SenderNickname = "Presidente",
            SenderCity = "Bragança, 10 de Janeiro de 2025",
            PreferencesLink = "https://rtub.azurewebsites.net/profile",
            Nickname = "TestUser",
            FullName = "Test User Full Name"
        };

        // Assert
        model.MeetingType.Should().Be("Assembleia Geral Ordinária");
        model.MeetingTitle.Should().Be("AGO Janeiro 2025");
        model.DateFormatted.Should().Be("quinta-feira, 15 de Janeiro de 2025 • 20:00");
        model.Location.Should().Be("Sede do RTUB");
        model.Statement.Should().Be("Esta reunião tem como objetivo discutir os temas importantes da organização.");
        model.SenderNickname.Should().Be("Presidente");
        model.SenderCity.Should().Be("Bragança, 10 de Janeiro de 2025");
        model.PreferencesLink.Should().Be("https://rtub.azurewebsites.net/profile");
        model.Nickname.Should().Be("TestUser");
        model.FullName.Should().Be("Test User Full Name");
    }

    [Fact]
    public void MeetingNotificationModel_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var model = new MeetingNotificationModel();

        // Assert
        model.MeetingType.Should().Be(string.Empty);
        model.MeetingTitle.Should().Be(string.Empty);
        model.DateFormatted.Should().Be(string.Empty);
        model.Location.Should().Be(string.Empty);
        model.Statement.Should().Be(string.Empty);
        model.SenderNickname.Should().Be(string.Empty);
        model.SenderCity.Should().Be(string.Empty);
        model.PreferencesLink.Should().Be("https://rtub.azurewebsites.net/profile");
        model.Nickname.Should().Be(string.Empty);
        model.FullName.Should().Be(string.Empty);
    }
}
