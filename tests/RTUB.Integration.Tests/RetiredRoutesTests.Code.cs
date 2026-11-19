using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace RTUB.Integration.Tests;

// The source, the solution and the assemblies hold no Blazor host, no Razor component and none of the files and types
// that only a removed page used.
public partial class RetiredRoutesTests
{
    [Fact]
    public void NoRazorComponent_IsLeftInTheSource_OrTheAssembly()
    {
        var src = Path.Combine(RepoPaths.Root(), "src");
        Directory.EnumerateFiles(src, "*.razor*", SearchOption.AllDirectories)
            .Where(f => !f.Replace('\\', '/').Contains("/obj/") && !f.Replace('\\', '/').Contains("/bin/"))
            .Should().BeEmpty("App.razor, MainLayout, _Imports and every component went with the host");

        var web = typeof(RTUB.Program).Assembly;
        web.GetTypes().Where(t => typeof(IComponent).IsAssignableFrom(t)).Should().BeEmpty("no Blazor component is compiled");
        web.GetTypes().SelectMany(t => t.GetCustomAttributes(typeof(RouteAttribute), false)).Should().BeEmpty("0 Blazor pages");
    }

    [Fact]
    public void RTUBShared_IsGone_FromTheDisk_TheSolution_AndTheWebProject()
    {
        var root = RepoPaths.Root();
        File.Exists(Path.Combine(root, "src", "RTUB.Shared", "RTUB.Shared.csproj")).Should().BeFalse();
        File.Exists(Path.Combine(root, "tests", "RTUB.Shared.Tests", "RTUB.Shared.Tests.csproj")).Should().BeFalse();
        File.ReadAllText(Path.Combine(root, "RTUB.sln")).Should().NotContain("RTUB.Shared");
        File.ReadAllText(Path.Combine(root, "src", "RTUB.Web", "RTUB.csproj")).Should().NotContain("RTUB.Shared");
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).Should().NotContain("RTUB.Shared");
    }

    [Fact]
    public void Startup_DoesNotHostBlazor()
    {
        var src = Path.Combine(RepoPaths.Root(), "src", "RTUB.Web");
        var startup = File.ReadAllText(Path.Combine(src, "Program.cs")) + string.Concat(Directory.GetFiles(Path.Combine(src, "Extensions"), "*.cs").OrderBy(f => f).Select(File.ReadAllText));

        startup.Should().NotContainAny("MapRazorComponents", "AddRazorComponents", "AddInteractiveServerComponents",
            "AddInteractiveServerRenderMode", "AddCascadingAuthenticationState", "ServerAuthenticationStateProvider",
            "CircuitHost", "_blazor", "HubOptions");
    }

    [Fact]
    public void ServiceWorker_PrecachesNoBlazorAsset()
    {
        var sw = File.ReadAllText(Path.Combine(RepoPaths.Root(), "src", "RTUB.Web", "wwwroot", "service-worker.js"));

        sw.Should().NotContainAny("blazor.web.js", "_framework", "_blazor");
        sw.Should().Contain("'/offline.html'", "offline support stays");
        sw.Should().Contain("addEventListener('push'", "push support stays");
    }

    [Fact]
    public void LabelAndSlideshowCode_AndModel_AreGone()
    {
        var application = typeof(RTUB.Application.Data.ApplicationDbContext).Assembly;
        foreach (var name in new[] { "ILabelService", "LabelService", "ILabelRepository", "LabelRepository",
                     "ISlideshowService", "SlideshowService", "ISlideshowRepository", "SlideshowRepository" })
        {
            application.GetTypes().Should().NotContain(t => t.Name == name, "{0} has no caller", name);
        }

        foreach (var name in new[] { "Label", "Slideshow" })
        {
            typeof(RTUB.Core.Entities.ApplicationUser).Assembly.GetTypes().Should().NotContain(t => t.Name == name, "{0} is not an entity", name);
        }

        // The tables were dropped with their entities and DbSets.
        typeof(RTUB.Application.Data.ApplicationDbContext).GetProperty("Labels").Should().BeNull();
        typeof(RTUB.Application.Data.ApplicationDbContext).GetProperty("Slideshows").Should().BeNull();
    }

    [Fact]
    public void BlazorMeetingsPage_AndWhatOnlyItUsed_AreGone()
    {
        var src = Path.Combine(RepoPaths.Root(), "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Activities/Meetings.razor", "RTUB.Shared/Components/Cards/MeetingCard.razor",
                     "RTUB.Shared/Components/Cards/MeetingRequestCard.razor", "RTUB.Shared/Components/Modals/MeetingParticipationModal.razor",
                     "RTUB.Web/wwwroot/css/3-components/meeting-card.css",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} must not come back", gone);
        }

        typeof(RTUB.Program).Assembly.GetType("RTUB.Pages.Activities.Meetings").Should().BeNull();
    }

    [Fact]
    public void BlazorNaipesPages_AndWhatOnlyTheyUsed_AreGone()
    {
        var src = Path.Combine(RepoPaths.Root(), "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Activities/Naipes.razor", "RTUB.Web/Pages/Activities/NaipesConfig.razor",
                     "RTUB.Shared/Components/Cards/NaipeCard.razor", "RTUB.Shared/Components/Naipes/NaipeCommentItem.razor",
                     "RTUB.Shared/Components/Modals/DetailsModal.razor", "RTUB.Shared/Components/Modals/InfoSection.razor",
                     "RTUB.Shared/Components/Profile/ProfileField.razor", "RTUB.Web/wwwroot/css/3-components/naipe-card.css",
                     "RTUB.Web/wwwroot/css/3-components/details-modal.css", "RTUB.Web/wwwroot/css/4-pages/naipes-config.css",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} must not come back", gone);
        }

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Activities.Naipes").Should().BeNull();
        web.GetType("RTUB.Pages.Activities.NaipesConfig").Should().BeNull();
    }

    [Fact]
    public void BlazorProfileMapAndHallOfFamePages_AndWhatOnlyTheyUsed_AreGone()
    {
        var src = Path.Combine(RepoPaths.Root(), "src");
        foreach (var gone in new[]
                 {
                     "RTUB.Web/Pages/Members/Profile.razor", "RTUB.Web/Pages/Members/MemberMap.razor", "RTUB.Web/Pages/Activities/HallOfFame.razor",
                     "RTUB.Shared/Components/Profile/ProfileHeader.razor", "RTUB.Shared/Components/Profile/ProfileSection.razor",
                     "RTUB.Shared/Components/Ranking/RankCard.razor", "RTUB.Shared/Components/Profile/UnifiedTimeline.razor",
                     "RTUB.Shared/Components/Forms/MonthYearPicker.razor", "RTUB.Shared/Components/UI/PushNotificationToggle.razor",
                     "RTUB.Shared/Components/Uploads/ImageCropper.razor", "RTUB.Web/wwwroot/js/memberMap.js", "RTUB.Web/wwwroot/js/imageCropper.js",
                 })
        {
            File.Exists(Path.Combine(src, gone)).Should().BeFalse("{0} must not come back", gone);
        }

        var web = typeof(RTUB.Program).Assembly;
        web.GetType("RTUB.Pages.Members.Profile").Should().BeNull();
        web.GetType("RTUB.Pages.Members.MemberMap").Should().BeNull();
        web.GetType("RTUB.Pages.Activities.HallOfFame").Should().BeNull();
    }
}
