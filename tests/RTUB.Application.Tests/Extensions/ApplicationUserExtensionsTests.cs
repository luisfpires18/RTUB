using FluentAssertions;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Extensions;

/// <summary>
/// Unit tests for ApplicationUserExtensions
/// Tests category checks and role validation logic
/// </summary>
public partial class ApplicationUserExtensionsTests
{
    private ApplicationUser CreateUserWithCategories(params MemberCategory[] categories)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestUser",
            Email = "test@example.com",
            UserName = "test@example.com"
        };

        if (categories.Any())
        {
            user.Categories = categories.ToList();
        }

        return user;
    }

    [Fact]
    public void IsLeitao_WithLeitaoCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);

        // Act & Assert
        user.IsLeitao().Should().BeTrue();
    }

    [Fact]
    public void IsLeitao_WithoutLeitaoCategory_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);

        // Act & Assert
        user.IsLeitao().Should().BeFalse();
    }

    [Fact]
    public void IsCaloiro_WithCaloiroCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);

        // Act & Assert
        user.IsCaloiro().Should().BeTrue();
    }

    [Fact]
    public void IsTuno_WithTunoCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);

        // Act & Assert
        user.IsTuno().Should().BeTrue();
    }

    [Fact]
    public void IsVeterano_WithVeteranoCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Veterano);

        // Act & Assert
        user.IsVeterano().Should().BeTrue();
    }

    [Fact]
    public void IsTunossauro_WithTunossauroCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tunossauro);

        // Act & Assert
        user.IsTunossauro().Should().BeTrue();
    }

    [Fact]
    public void IsTunoHonorario_WithTunoHonorarioCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.TunoHonorario);

        // Act & Assert
        user.IsTunoHonorario().Should().BeTrue();
    }

    [Fact]
    public void IsFundador_WithFundadorCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Fundador);

        // Act & Assert
        user.IsFundador().Should().BeTrue();
    }

    [Fact]
    public void IsFundador_WithoutFundadorCategory_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);

        // Act & Assert
        user.IsFundador().Should().BeFalse();
    }



    [Fact]
    public void IsOnlyLeitao_WithOnlyLeitaoCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);

        // Act & Assert
        user.IsOnlyLeitao().Should().BeTrue();
    }

    [Fact]
    public void IsOnlyLeitao_WithLeitaoAndCaloiro_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao, MemberCategory.Caloiro);

        // Act & Assert
        user.IsOnlyLeitao().Should().BeFalse();
    }

    [Fact]
    public void IsOnlyLeitao_WithLeitaoAndTuno_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao, MemberCategory.Tuno);

        // Act & Assert
        user.IsOnlyLeitao().Should().BeFalse();
    }

    [Fact]
    public void IsOnlyLeitao_WithoutLeitao_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);

        // Act & Assert
        user.IsOnlyLeitao().Should().BeFalse();
    }

    [Fact]
    public void IsTunoOrHigher_WithTuno_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);

        // Act & Assert
        user.IsTunoOrHigher().Should().BeTrue();
    }

    [Fact]
    public void IsTunoOrHigher_WithVeterano_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Veterano);

        // Act & Assert
        user.IsTunoOrHigher().Should().BeTrue();
    }

    [Fact]
    public void IsTunoOrHigher_WithTunossauro_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tunossauro);

        // Act & Assert
        user.IsTunoOrHigher().Should().BeTrue();
    }

    [Fact]
    public void IsTunoOrHigher_WithCaloiro_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);

        // Act & Assert
        user.IsTunoOrHigher().Should().BeFalse();
    }

    [Fact]
    public void IsTunoOrHigher_WithLeitao_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);

        // Act & Assert
        user.IsTunoOrHigher().Should().BeFalse();
    }

    [Fact]
    public void IsEffectiveMember_WithCaloiro_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);

        // Act & Assert
        user.IsEffectiveMember().Should().BeTrue();
    }

    [Fact]
    public void IsEffectiveMember_WithTuno_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);

        // Act & Assert
        user.IsEffectiveMember().Should().BeTrue();
    }

    [Fact]
    public void IsEffectiveMember_WithVeterano_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Veterano);

        // Act & Assert
        user.IsEffectiveMember().Should().BeTrue();
    }

    [Fact]
    public void IsEffectiveMember_WithTunossauro_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tunossauro);

        // Act & Assert
        user.IsEffectiveMember().Should().BeTrue();
    }

    [Fact]
    public void IsEffectiveMember_WithOnlyLeitao_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);

        // Act & Assert
        user.IsEffectiveMember().Should().BeFalse();
    }

    [Fact]
    public void CanBeMentor_WithTunoOrHigher_ReturnsTrue()
    {
        // Arrange
        var tunoUser = CreateUserWithCategories(MemberCategory.Tuno);
        var veteranoUser = CreateUserWithCategories(MemberCategory.Veterano);
        var tunossauroUser = CreateUserWithCategories(MemberCategory.Tunossauro);

        // Act & Assert
        tunoUser.CanBeMentor().Should().BeTrue();
        veteranoUser.CanBeMentor().Should().BeTrue();
        tunossauroUser.CanBeMentor().Should().BeTrue();
    }

    [Fact]
    public void CanBeMentor_WithCaloiroOrLeitao_ReturnsFalse()
    {
        // Arrange
        var caloiroUser = CreateUserWithCategories(MemberCategory.Caloiro);
        var leitaoUser = CreateUserWithCategories(MemberCategory.Leitao);

        // Act & Assert
        caloiroUser.CanBeMentor().Should().BeFalse();
        leitaoUser.CanBeMentor().Should().BeFalse();
    }
}
