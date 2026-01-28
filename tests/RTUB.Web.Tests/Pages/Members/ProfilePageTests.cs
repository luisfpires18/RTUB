using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Pages.Members;
using RTUB.Web.Tests.Pages.Base;

namespace RTUB.Web.Tests.Pages.Members;

/// <summary>
/// Component tests for Profile.razor page
/// Tests page rendering, profile editing, password change, mentor assignment, and authorization
/// </summary>
public class ProfilePageTests : PageTestBase
{
    private const string SkipModal = "Modal renders outside component fragment; cannot assert modal markup in bUnit.";

    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<IUserProfileService> _mockUserProfileService;
    private readonly Mock<IRoleAssignmentService> _mockRoleAssignmentService;
    private readonly Mock<IRankingService> _mockRankingService;
    private readonly Mock<IMemberInstrumentService> _mockMemberInstrumentService;
    private readonly Mock<IRetirementStatusService> _mockRetirementStatusService;
    private readonly Mock<IMemberStatusService> _mockMemberStatusService;
    private readonly Mock<RTUB.Web.Services.ProfilePictureUpdateService> _mockProfilePictureUpdateService;

    public ProfilePageTests()
    {
        // Setup service mocks
        _mockUserManager = SetupUserManager();
        _mockSignInManager = SetupSignInManager();
        _mockUserProfileService = SetupService<IUserProfileService>();
        _mockRoleAssignmentService = SetupService<IRoleAssignmentService>();
        _mockRankingService = SetupService<IRankingService>();
        _mockMemberInstrumentService = SetupService<IMemberInstrumentService>();
        _mockRetirementStatusService = SetupService<IRetirementStatusService>();
        _mockMemberStatusService = SetupService<IMemberStatusService>();
        _mockProfilePictureUpdateService = SetupService<RTUB.Web.Services.ProfilePictureUpdateService>();

        // Setup default service responses
        var testUser = CreateTestUser("test-user", "Test", "User");
        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);

        _mockUserManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);

        // Setup mock DbSet for Users property
        var users = new[] { testUser }.AsQueryable();
        var mockDbSet = new Mock<DbSet<ApplicationUser>>();
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Provider).Returns(users.Provider);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Expression).Returns(users.Expression);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.ElementType).Returns(users.ElementType);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.GetEnumerator()).Returns(users.GetEnumerator());
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        _mockRoleAssignmentService
            .Setup(x => x.GetAllRoleAssignmentsAsync())
            .ReturnsAsync(new List<RoleAssignment>());

        _mockRankingService
            .Setup(x => x.GetRankProgressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RTUB.Application.DTOs.RankProgressInfo
            {
                CurrentLevel = 1,
                CurrentRankName = "Iniciante",
                CurrentXp = 0,
                XpForNextLevel = 100,
                XpForCurrentLevel = 0
            });

        _mockMemberInstrumentService
            .Setup(x => x.GetMemberInstrumentsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<MemberInstrument>());

        // RetirementStatusService.EvaluateRetirementStatusAsync is called in try-catch, so we can throw
        _mockRetirementStatusService
            .Setup(x => x.EvaluateRetirementStatusAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Not implemented in test"));

        _mockMemberStatusService
            .Setup(x => x.GetMemberStatusAsync(It.IsAny<string>()))
            .ReturnsAsync(new RTUB.Application.DTOs.MemberStatusResult
            {
                IsRetired = false,
                LastRehearsalDate = null,
                LastEventDate = null
            });

        SetupAuthentication("test-user", "Test User");
    }

    #region Page Rendering Tests

    [Fact]
    public async Task ProfilePage_RendersPageTitle()
    {
        // Arrange & Act
        var cut = RenderComponent<Profile>();
        cut.WaitForState(() => cut.Markup.Contains("Meu Perfil") || cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Meu Perfil", "page should display 'Meu Perfil' title");
    }

    [Fact]
    public async Task ProfilePage_ShowsLoadingState_Initially()
    {
        // Arrange - Setup slow service to test loading state
        var tcs = new TaskCompletionSource<ApplicationUser?>();
        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .Returns(tcs.Task);

        // Act
        var cut = RenderComponent<Profile>();

        // Assert - Check loading state before async operations complete
        cut.Markup.Should().Contain("A carregar", "page should show loading state initially");
        
        // Complete the delayed task to allow test cleanup
        tcs.SetResult(CreateTestUser("test-user", "Test", "User"));
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ProfilePage_DisplaysUserProfile_WhenLoaded()
    {
        // Arrange
        var testUser = CreateTestUser("test-user", "Test", "User", "TestNickname");
        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);

        // Act
        var cut = RenderComponent<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Test", "page should display user first name");
        cut.Markup.Should().Contain("User", "page should display user last name");
    }

    [Fact]
    public async Task ProfilePage_ShowsPasswordChangeWarning_WhenRequired()
    {
        // Arrange
        var testUser = CreateTestUser("test-user", "Test", "User");
        testUser.RequirePasswordChange = true;
        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);

        // Act
        var cut = RenderComponent<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Ação Necessária", "page should show password change warning");
        cut.Markup.Should().Contain("Alterar Palavra-passe Agora", "page should show password change button");
    }

    [Fact]
    public async Task ProfilePage_DisplaysPersonalSection()
    {
        // Arrange
        var testUser = CreateTestUser("test-user", "Test", "User");
        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);

        // Act
        var cut = RenderComponent<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert - Personal section is shown by default (sectionsExpanded["personal"] = true)
        cut.Markup.Should().Contain("Informações Pessoais", "page should display personal section title");
    }

    [Fact]
    public async Task ProfilePage_DisplaysTunaSection()
    {
        // Arrange
        var testUser = CreateTestUser("test-user", "Test", "User");
        _mockUserManager
            .Setup(x => x.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync(testUser);

        // Act
        var cut = RenderComponent<Profile>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert - Tuna section is shown by default (sectionsExpanded["tuna"] = true)
        cut.Markup.Should().Contain("Tuna", "page should display tuna section");
    }

    #endregion

    #region Helper Methods

    private static ApplicationUser CreateTestUser(string id, string firstName, string lastName, string? nickname = null)
    {
        return new ApplicationUser
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Nickname = nickname,
            Email = $"{id}@test.com",
            UserName = id,
            RequirePasswordChange = false
        };
    }

    private Mock<SignInManager<ApplicationUser>> SetupSignInManager()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        
        var httpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<Microsoft.AspNetCore.Identity.IUserClaimsPrincipalFactory<ApplicationUser>>();
        var options = Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Identity.IdentityOptions());
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<SignInManager<ApplicationUser>>>();
        var schemes = new Mock<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
        var confirmation = new Mock<Microsoft.AspNetCore.Identity.IUserConfirmation<ApplicationUser>>();

        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            httpContextAccessor.Object,
            userPrincipalFactory.Object,
            options,
            logger.Object,
            schemes.Object,
            confirmation.Object);

        Services.AddSingleton(signInManager.Object);
        Services.AddSingleton(new RTUB.Application.Services.AuditContext());
        return signInManager;
    }

    #endregion
}
