using Microsoft.AspNetCore.Identity;
using Moq;
using RTUB.Application.Services;
using RTUB.Core.Constants;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using System.Security.Claims;
using Xunit;

namespace RTUB.Application.Tests.Services;

public class UserClaimsServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly UserClaimsService _service;

    public UserClaimsServiceTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null, null, null, null, null, null, null, null);
        
        _service = new UserClaimsService(_userManagerMock.Object);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_AddsCategoryClaimsForTuno()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim>());
        
        _userManagerMock.Setup(x => x.AddClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.SyncUserClaimsAsync(user);

        // Assert
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Category && c.Value == MemberCategory.Tuno.ToString()))), 
            Times.Once);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_AddsPositionClaimsForMagister()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            Positions = new List<Position> { Position.Magister }
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim>());
        
        _userManagerMock.Setup(x => x.AddClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.SyncUserClaimsAsync(user);

        // Assert
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Position && c.Value == Position.Magister.ToString()))), 
            Times.Once);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_AddsMagisterPermissions()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            Positions = new List<Position> { Position.Magister }
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim>());
        
        _userManagerMock.Setup(x => x.AddClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.SyncUserClaimsAsync(user);

        // Assert
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == Permissions.ManageAllFinances))), 
            Times.Once);
        
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == Permissions.AssignAllPositions))), 
            Times.Once);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_AddsEnsaiadorPermissions()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            Positions = new List<Position> { Position.Ensaiador }
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim>());
        
        _userManagerMock.Setup(x => x.AddClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.SyncUserClaimsAsync(user);

        // Assert
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == Permissions.ManageAllRehearsals))), 
            Times.Once);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_AddsEffectiveMemberPermissionsForCaloiro()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            Categories = new List<MemberCategory> { MemberCategory.Caloiro }
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim>());
        
        _userManagerMock.Setup(x => x.AddClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.SyncUserClaimsAsync(user);

        // Assert
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == Permissions.IsEffectiveMember))), 
            Times.Once);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_AddsTunoPermissions()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim>());
        
        _userManagerMock.Setup(x => x.AddClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.SyncUserClaimsAsync(user);

        // Assert
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == Permissions.CanBeMentor))), 
            Times.Once);
        
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == Permissions.CanVote))), 
            Times.Once);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_AddsLeitaoRestrictedForLeitaoOnly()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            Categories = new List<MemberCategory> { MemberCategory.Leitao }
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim>());
        
        _userManagerMock.Setup(x => x.AddClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.SyncUserClaimsAsync(user);

        // Assert
        _userManagerMock.Verify(x => x.AddClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == Permissions.LeitaoRestricted))), 
            Times.Once);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_RemovesOldClaimsBeforeAddingNew()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };

        var oldClaims = new List<Claim>
        {
            new Claim(CustomClaimTypes.Category, MemberCategory.Caloiro.ToString()),
            new Claim(CustomClaimTypes.Position, Position.Ensaiador.ToString())
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(oldClaims);
        
        _userManagerMock.Setup(x => x.RemoveClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);
        
        _userManagerMock.Setup(x => x.AddClaimsAsync(user, It.IsAny<IEnumerable<Claim>>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.SyncUserClaimsAsync(user);

        // Assert
        _userManagerMock.Verify(x => x.RemoveClaimsAsync(user, 
            It.Is<IEnumerable<Claim>>(claims => 
                claims.Any(c => c.Type == CustomClaimTypes.Category && c.Value == MemberCategory.Caloiro.ToString()))), 
            Times.Once);
    }

    [Fact]
    public async Task AddPermissionClaimAsync_AddsPermissionClaim()
    {
        // Arrange
        var user = new ApplicationUser { Id = "test-user" };
        var permission = Permissions.ViewEvents;

        _userManagerMock.Setup(x => x.AddClaimAsync(user, It.IsAny<Claim>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.AddPermissionClaimAsync(user, permission);

        // Assert
        _userManagerMock.Verify(x => x.AddClaimAsync(user, 
            It.Is<Claim>(c => c.Type == CustomClaimTypes.Permission && c.Value == permission)), 
            Times.Once);
    }

    [Fact]
    public async Task RemovePermissionClaimAsync_RemovesPermissionClaim()
    {
        // Arrange
        var user = new ApplicationUser { Id = "test-user" };
        var permission = Permissions.ViewEvents;
        
        var existingClaims = new List<Claim>
        {
            new Claim(CustomClaimTypes.Permission, permission)
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(existingClaims);
        
        _userManagerMock.Setup(x => x.RemoveClaimAsync(user, It.IsAny<Claim>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _service.RemovePermissionClaimAsync(user, permission);

        // Assert
        _userManagerMock.Verify(x => x.RemoveClaimAsync(user, 
            It.Is<Claim>(c => c.Type == CustomClaimTypes.Permission && c.Value == permission)), 
            Times.Once);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsTrueWhenUserHasPermission()
    {
        // Arrange
        var user = new ApplicationUser { Id = "test-user" };
        var permission = Permissions.ViewEvents;
        
        var claims = new List<Claim>
        {
            new Claim(CustomClaimTypes.Permission, permission)
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(claims);

        // Act
        var result = await _service.HasPermissionAsync(user, permission);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalseWhenUserDoesNotHavePermission()
    {
        // Arrange
        var user = new ApplicationUser { Id = "test-user" };
        var permission = Permissions.ViewEvents;
        
        var claims = new List<Claim>();

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(claims);

        // Act
        var result = await _service.HasPermissionAsync(user, permission);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetPermissionsAsync_ReturnsAllUserPermissions()
    {
        // Arrange
        var user = new ApplicationUser { Id = "test-user" };
        
        var claims = new List<Claim>
        {
            new Claim(CustomClaimTypes.Permission, Permissions.ViewEvents),
            new Claim(CustomClaimTypes.Permission, Permissions.CreateEvent),
            new Claim(CustomClaimTypes.Category, MemberCategory.Tuno.ToString()) // Should be filtered out
        };

        _userManagerMock.Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(claims);

        // Act
        var result = await _service.GetPermissionsAsync(user);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(Permissions.ViewEvents, result);
        Assert.Contains(Permissions.CreateEvent, result);
    }
}
