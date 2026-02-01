using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Pages.Members;
using RTUB.Web.Tests.Pages.Base;
using MembersPage = RTUB.Pages.Members.Members;

namespace RTUB.Web.Tests.Pages.Members;

/// <summary>
/// Component tests for Members.razor page
/// Tests page rendering, modal interactions, CRUD workflows, profile workflows, and authorization
/// </summary>
public class MembersPageTests : PageTestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
    private readonly Mock<IRoleAssignmentService> _mockRoleAssignmentService;
    private readonly Mock<IEnrollmentService> _mockEnrollmentService;
    private readonly Mock<IMemberInstrumentService> _mockMemberInstrumentService;
    private readonly Mock<IEmailNotificationService> _mockEmailNotificationService;
    private readonly Mock<IUserRoleQueryService> _mockUserRoleQueryService;
    private readonly Mock<IRetirementStatusService> _mockRetirementStatusService;
    private readonly Mock<IMemberStatusService> _mockMemberStatusService;
    private readonly Mock<IMemberStatisticsService> _mockMemberStatisticsService;
    private readonly Mock<IUserProfileService> _mockUserProfileService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<Microsoft.Extensions.Options.IOptions<RTUB.Application.Configuration.Toggles>> _mockToggles;

    public MembersPageTests()
    {
        // Setup service mocks
        _mockUserManager = SetupUserManager();
        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
            roleStore.Object, null!, null!, null!, null!);
        Services.AddSingleton(_mockRoleManager.Object);
        Services.AddSingleton(new AuditContext());
        _mockRoleAssignmentService = SetupService<IRoleAssignmentService>();
        _mockEnrollmentService = SetupService<IEnrollmentService>();
        _mockMemberInstrumentService = SetupService<IMemberInstrumentService>();
        _mockEmailNotificationService = SetupService<IEmailNotificationService>();
        _mockUserRoleQueryService = SetupService<IUserRoleQueryService>();
        _mockRetirementStatusService = SetupService<IRetirementStatusService>();
        _mockMemberStatusService = SetupService<IMemberStatusService>();
        _mockMemberStatisticsService = SetupService<IMemberStatisticsService>();
        _mockUserProfileService = SetupService<IUserProfileService>();
        _mockPushNotificationFactory = SetupService<IPushNotificationFactory>();
        _mockPushNotificationService = SetupService<IPushNotificationService>();
        _mockToggles = SetupService<Microsoft.Extensions.Options.IOptions<RTUB.Application.Configuration.Toggles>>();

        // Setup default service responses - Create mock DbSet for Users
        var emptyUsers = new List<ApplicationUser>().AsQueryable();
        var mockDbSet = new Mock<DbSet<ApplicationUser>>();
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Provider).Returns(emptyUsers.Provider);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Expression).Returns(emptyUsers.Expression);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.ElementType).Returns(emptyUsers.ElementType);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.GetEnumerator()).Returns(emptyUsers.GetEnumerator());
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        _mockRoleAssignmentService
            .Setup(x => x.GetAllRoleAssignmentsAsync())
            .ReturnsAsync(new List<RoleAssignment>());

        _mockUserRoleQueryService
            .Setup(x => x.GetUserRolesAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<RTUB.Application.DTOs.UserRoleDto>());

        _mockMemberInstrumentService
            .Setup(x => x.GetMemberInstrumentsByUserIdsAsync(It.IsAny<List<string>>()))
            .ReturnsAsync(new Dictionary<string, List<MemberInstrument>>());

        // Setup Toggles mock
        var toggles = new RTUB.Application.Configuration.Toggles();
        _mockToggles
            .Setup(x => x.Value)
            .Returns(toggles);
    }

    private const string SkipReason = "Members uses UserManager.Users.ToListAsync(); IQueryable mock doesn't implement IAsyncEnumerable. Requires IMemberQueryService or similar.";

    #region Page Rendering Tests

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_RendersPageTitle()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");

        // Act
        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Membros", "page should display 'Membros' title");
    }

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_ShowsMembersSection()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        // Users already set up in constructor

        // Act
        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Membros", "page should display members section");
    }

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_DisplaysMembers_WhenMembersExist()
    {
        // Arrange
        SetupAuthentication("test-user", "Test User");
        var members = new List<ApplicationUser>
        {
            CreateTestMember("user1", "Test User 1", "test1@example.com"),
            CreateTestMember("user2", "Test User 2", "test2@example.com")
        };

        // Setup mock DbSet with members
        var membersQueryable = members.AsQueryable();
        var mockDbSet = new Mock<DbSet<ApplicationUser>>();
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Provider).Returns(membersQueryable.Provider);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Expression).Returns(membersQueryable.Expression);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.ElementType).Returns(membersQueryable.ElementType);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.GetEnumerator()).Returns(membersQueryable.GetEnumerator());
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        // Act
        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => cut.Markup.Contains("Test User") || !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Test User 1", "page should display first member");
        cut.Markup.Should().Contain("Test User 2", "page should display second member");
    }

    #endregion

    #region Authorization Tests

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_ShowsCreateButton_ForAdminUser()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        // Users already set up in constructor

        // Act
        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Adicionar Membro", "admin should see create button");
    }

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_HidesCreateButton_ForRegularUser()
    {
        // Arrange
        SetupAuthentication("regular-user", "Regular User");
        // Users already set up in constructor

        // Act
        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().NotContain("Adicionar Membro", "regular user should not see create button");
    }

    #endregion

    #region Modal Interaction Tests

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_OpenCreateModal_ShowsCreateModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        // Users already set up in constructor

        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open create modal via reflection (method is private)
        var openCreateModalMethod = typeof(MembersPage).GetMethod("OpenCreateModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openCreateModalMethod!.Invoke(cut.Instance, null));

        // Assert
        cut.Markup.Should().Contain("Adicionar Novo Membro", "create modal should be displayed");
    }

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_OpenEditModal_ShowsEditModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var member = CreateTestMember("user1", "Test User", "test@example.com");
        var members = new List<ApplicationUser> { member };

        // Setup mock DbSet with member
        var membersQueryable = members.AsQueryable();
        var mockDbSet = new Mock<DbSet<ApplicationUser>>();
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Provider).Returns(membersQueryable.Provider);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Expression).Returns(membersQueryable.Expression);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.ElementType).Returns(membersQueryable.ElementType);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.GetEnumerator()).Returns(membersQueryable.GetEnumerator());
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        _mockUserManager
            .Setup(x => x.FindByIdAsync("user1"))
            .ReturnsAsync(member);

        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open edit modal via reflection (method is private)
        var openEditModalMethod = typeof(MembersPage).GetMethod("OpenEditModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openEditModalMethod!.Invoke(cut.Instance, new object[] { member }));

        // Assert
        cut.Markup.Should().Contain("Editar Membro", "edit modal should be displayed");
    }

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_OpenDeleteModal_ShowsDeleteModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var member = CreateTestMember("user1", "Test User", "test@example.com");
        var members = new List<ApplicationUser> { member };

        // Setup mock DbSet with member
        var membersQueryable = members.AsQueryable();
        var mockDbSet = new Mock<DbSet<ApplicationUser>>();
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Provider).Returns(membersQueryable.Provider);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Expression).Returns(membersQueryable.Expression);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.ElementType).Returns(membersQueryable.ElementType);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.GetEnumerator()).Returns(membersQueryable.GetEnumerator());
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open delete modal via reflection (method is private)
        var openDeleteModalMethod = typeof(MembersPage).GetMethod("OpenDeleteModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openDeleteModalMethod!.Invoke(cut.Instance, new object[] { member }));

        // Assert
        cut.Markup.Should().Contain("Confirmar Eliminação", "delete modal should be displayed");
    }

    #endregion

    #region CRUD Operation Tests

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_CreateMember_OpensModal()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        // Users already set up in constructor

        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Open create modal
        var openCreateModalMethod = typeof(MembersPage).GetMethod("OpenCreateModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => openCreateModalMethod!.Invoke(cut.Instance, null));

        // Assert
        cut.Markup.Should().Contain("Adicionar Novo Membro", "create modal should be displayed");
    }

    [Fact(Skip = SkipReason)]
    public async Task MembersPage_DeleteMember_CallsService()
    {
        // Arrange
        SetupAuthentication("admin-user", "Admin User", "Admin");
        var member = CreateTestMember("user1", "Test User", "test@example.com");
        var members = new List<ApplicationUser> { member };

        // Setup mock DbSet with member
        var membersQueryable = members.AsQueryable();
        var mockDbSet = new Mock<DbSet<ApplicationUser>>();
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Provider).Returns(membersQueryable.Provider);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Expression).Returns(membersQueryable.Expression);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.ElementType).Returns(membersQueryable.ElementType);
        mockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.GetEnumerator()).Returns(membersQueryable.GetEnumerator());
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        _mockUserProfileService
            .Setup(x => x.DeleteMemberWithRelatedDataAsync("user1"))
            .ReturnsAsync(true);

        // Setup for reload after delete
        var emptyUsers = new List<ApplicationUser>().AsQueryable();
        var emptyMockDbSet = new Mock<DbSet<ApplicationUser>>();
        emptyMockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Provider).Returns(emptyUsers.Provider);
        emptyMockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.Expression).Returns(emptyUsers.Expression);
        emptyMockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.ElementType).Returns(emptyUsers.ElementType);
        emptyMockDbSet.As<IQueryable<ApplicationUser>>().Setup(m => m.GetEnumerator()).Returns(emptyUsers.GetEnumerator());
        _mockUserManager.Setup(x => x.Users).Returns(emptyMockDbSet.Object);

        var cut = RenderComponent<MembersPage>();
        cut.WaitForState(() => !cut.Markup.Contains("A carregar"), TimeSpan.FromSeconds(2));

        // Act - Delete member via reflection (method is private)
        var deleteMemberMethod = typeof(MembersPage).GetMethod("DeleteMember",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await cut.InvokeAsync(() => deleteMemberMethod!.Invoke(cut.Instance, null));

        // Assert
        _mockUserProfileService.Verify(x => x.DeleteMemberWithRelatedDataAsync("user1"), Times.Once, "DeleteMemberWithRelatedDataAsync should be called");
    }

    #endregion

    #region Helper Methods

    private static ApplicationUser CreateTestMember(string id, string userName, string email)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            Email = email,
            Nickname = userName,
            FirstName = "Test",
            LastName = "User"
        };
    }

    #endregion
}
