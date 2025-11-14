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

    #region Email Validation Tests

    [Fact]
    public void Email_WithValidEmail_ShouldBeValid()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Email = "test@example.com"
        };

        // Assert
        user.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Email_CanBeSet_AndRetrieved()
    {
        // Arrange
        var user = new ApplicationUser();
        var expectedEmail = "user@rtub.pt";

        // Act
        user.Email = expectedEmail;

        // Assert
        user.Email.Should().Be(expectedEmail);
    }

    [Fact]
    public void Email_HasRequiredAttribute()
    {
        // Arrange
        var emailProperty = typeof(ApplicationUser).GetProperty("Email");

        // Act
        var requiredAttribute = emailProperty?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), true);

        // Assert
        requiredAttribute.Should().NotBeNull();
        requiredAttribute.Should().NotBeEmpty("Email should have Required attribute");
    }

    [Fact]
    public void Email_HasEmailAddressAttribute()
    {
        // Arrange
        var emailProperty = typeof(ApplicationUser).GetProperty("Email");

        // Act
        var emailAttribute = emailProperty?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.EmailAddressAttribute), true);

        // Assert
        emailAttribute.Should().NotBeNull();
        emailAttribute.Should().NotBeEmpty("Email should have EmailAddress attribute");
    }

    #endregion

    #region SelectedCategory Tests

    [Fact]
    public void SelectedCategory_CanBeSet_AndRetrieved()
    {
        // Arrange
        var user = new ApplicationUser();
        var expectedCategory = "Tuno";

        // Act
        user.SelectedCategory = expectedCategory;

        // Assert
        user.SelectedCategory.Should().Be(expectedCategory);
    }

    [Fact]
    public void SelectedCategory_CanBeNull()
    {
        // Arrange & Act
        var user = new ApplicationUser
        {
            SelectedCategory = null
        };

        // Assert
        user.SelectedCategory.Should().BeNull();
    }

    [Fact]
    public void SelectedCategory_HasNotMappedAttribute()
    {
        // Arrange
        var selectedCategoryProperty = typeof(ApplicationUser).GetProperty("SelectedCategory");

        // Act
        var notMappedAttribute = selectedCategoryProperty?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute), true);

        // Assert
        notMappedAttribute.Should().NotBeNull();
        notMappedAttribute.Should().NotBeEmpty("SelectedCategory should have NotMapped attribute");
    }

    [Fact]
    public void SelectedCategory_AcceptsValidCategories()
    {
        // Arrange
        var user = new ApplicationUser();
        var validCategories = new[] { "Leitão", "Caloiro", "Tuno" };

        // Act & Assert
        foreach (var category in validCategories)
        {
            user.SelectedCategory = category;
            user.SelectedCategory.Should().Be(category);
        }
    }

    #endregion
    
    #region CurrentRole Tests
    
    [Fact]
    public void CurrentRole_WithoutYearTuno_ReturnsNA()
    {
        // Arrange
        var user = new ApplicationUser
        {
            YearTuno = null
        };
        
        // Act
        var role = user.CurrentRole;
        
        // Assert
        role.Should().Be("N/A");
    }
    
    [Fact]
    public void CurrentRole_WithLessThan2Years_ReturnsTuno()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = new ApplicationUser
        {
            YearTuno = currentYear - 1
        };
        
        // Act
        var role = user.CurrentRole;
        
        // Assert
        role.Should().Be("TUNO");
    }
    
    [Fact]
    public void CurrentRole_With2To5Years_ReturnsVeterano()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = new ApplicationUser
        {
            YearTuno = currentYear - 3
        };
        
        // Act
        var role = user.CurrentRole;
        
        // Assert
        role.Should().Be("VETERANO");
    }
    
    [Fact]
    public void CurrentRole_With6OrMoreYears_ReturnsTunossauro()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = new ApplicationUser
        {
            YearTuno = currentYear - 7
        };
        
        // Act
        var role = user.CurrentRole;
        
        // Assert
        role.Should().Be("TUNOSSAURO");
    }
    
    [Fact]
    public void CurrentRole_WithMonthTuno_BeforeCurrentMonth_CalculatesCorrectly()
    {
        // Arrange - User became Tuno in January, current month is after
        var currentYear = DateTime.Now.Year;
        var currentMonth = DateTime.Now.Month;
        
        // Only run if current month is after January
        if (currentMonth > 1)
        {
            var user = new ApplicationUser
            {
                YearTuno = currentYear - 2,
                MonthTuno = 1 // January
            };
            
            // Act
            var role = user.CurrentRole;
            
            // Assert - Should be VETERANO (2 full years have passed)
            role.Should().Be("VETERANO");
        }
    }
    
    [Fact]
    public void CurrentRole_WithMonthTuno_AfterCurrentMonth_CalculatesCorrectly()
    {
        // Arrange - User became Tuno in December, current month is before
        var currentYear = DateTime.Now.Year;
        var currentMonth = DateTime.Now.Month;
        
        // Only run if current month is before December
        if (currentMonth < 12)
        {
            var user = new ApplicationUser
            {
                YearTuno = currentYear - 2,
                MonthTuno = 12 // December
            };
            
            // Act
            var role = user.CurrentRole;
            
            // Assert - Should still be TUNO (haven't reached 2-year anniversary)
            role.Should().Be("TUNO");
        }
    }
    
    [Fact]
    public void CurrentRole_WithMonthTuno_AtTunossauroThreshold_CalculatesCorrectly()
    {
        // Arrange - User became Tuno 6 years ago in a future month
        var currentYear = DateTime.Now.Year;
        var currentMonth = DateTime.Now.Month;
        
        // Only run if current month is before December
        if (currentMonth < 12)
        {
            var user = new ApplicationUser
            {
                YearTuno = currentYear - 6,
                MonthTuno = 12 // December (future month)
            };
            
            // Act
            var role = user.CurrentRole;
            
            // Assert - Should be VETERANO (not yet 6 full years)
            role.Should().Be("VETERANO");
        }
    }
    
    #endregion
}
