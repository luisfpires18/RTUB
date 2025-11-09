using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for ApplicationUser entity
/// Tests domain logic and entity properties including City field
/// </summary>
public class ApplicationUserTests
{
    [Fact]
    public void City_CanBeSet_AndRetrieved()
    {
        // Arrange
        var user = new ApplicationUser();
        var expectedCity = "Bragança";

        // Act
        user.City = expectedCity;

        // Assert
        user.City.Should().Be(expectedCity);
    }

    [Fact]
    public void City_CanBeNull()
    {
        // Arrange & Act
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User",
            City = null
        };

        // Assert
        user.City.Should().BeNull();
    }

    [Fact]
    public void City_CanBeEmpty()
    {
        // Arrange & Act
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User",
            City = ""
        };

        // Assert
        user.City.Should().BeEmpty();
    }

    [Fact]
    public void ApplicationUser_WithAllPersonalInfo_IncludesCity()
    {
        // Arrange
        var firstName = "João";
        var lastName = "Silva";
        var nickname = "Joãozinho";
        var email = "joao@example.com";
        var phoneContact = "912345678";
        var city = "Porto";
        var dateOfBirth = new DateTime(2000, 1, 1);

        // Act
        var user = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            Nickname = nickname,
            Email = email,
            PhoneContact = phoneContact,
            City = city,
            DateOfBirth = dateOfBirth
        };

        // Assert
        user.FirstName.Should().Be(firstName);
        user.LastName.Should().Be(lastName);
        user.Nickname.Should().Be(nickname);
        user.Email.Should().Be(email);
        user.PhoneContact.Should().Be(phoneContact);
        user.City.Should().Be(city);
        user.DateOfBirth.Should().Be(dateOfBirth);
    }

    [Fact]
    public void City_MaxLength_ShouldBe100Characters()
    {
        // Arrange
        var user = new ApplicationUser();
        var longCityName = new string('A', 100);

        // Act
        user.City = longCityName;

        // Assert
        user.City.Should().HaveLength(100);
        user.City.Should().Be(longCityName);
    }

    [Fact]
    public void Age_Calculation_IsNotAffectedByCity()
    {
        // Arrange
        var user = new ApplicationUser
        {
            DateOfBirth = DateTime.Today.AddYears(-25),
            City = "Lisboa"
        };

        // Act
        var age = user.Age;

        // Assert
        age.Should().Be(25);
        user.City.Should().Be("Lisboa");
    }

    [Fact]
    public void ApplicationUser_WithCityAndDegree_BothFieldsWork()
    {
        // Arrange
        var city = "Coimbra";
        var degree = "Engenharia Informática";

        // Act
        var user = new ApplicationUser
        {
            City = city,
            Degree = degree
        };

        // Assert
        user.City.Should().Be(city);
        user.Degree.Should().Be(degree);
    }

    [Fact]
    public void Subscribed_DefaultsToTrue()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        user.Subscribed.Should().BeTrue();
    }

    [Fact]
    public void Subscribed_CanBeSetToTrue()
    {
        // Arrange
        var user = new ApplicationUser();

        // Act
        user.Subscribed = true;

        // Assert
        user.Subscribed.Should().BeTrue();
    }

    [Fact]
    public void Subscribed_CanBeSetToFalse()
    {
        // Arrange
        var user = new ApplicationUser { Subscribed = true };

        // Act
        user.Subscribed = false;

        // Assert
        user.Subscribed.Should().BeFalse();
    }
}
