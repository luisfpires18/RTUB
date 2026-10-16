using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 041 (docs/react-messages.md): Messaging v2 is the React /messages over /api/conversations and /api/messages, on
/// the preserved v1 tables. Text only - nothing in the page, the endpoints or the contracts takes a file. The 027-retired
/// inbox (Blazor page, SignalR hub, its scripts) does not come back, and message text never goes to a log. Source
/// contracts: there is no JS test runner; the browser flows were checked by hand.
/// </summary>
public class MessagingV2SourceTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Web = Path.Combine(Root, "src", "RTUB.Web");
    private static readonly string Portal = Path.Combine(Web, "portal", "src");
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts));

    [Fact]
    public void TheRoute_IsTheReactPage_LinkedFromTheMemberMenu()
    {
        Read(Portal, "main.tsx").Should().Contain("'/messages': lazy(() => import('./Messages')),");
        Read(Portal, "content.ts").Should().Contain("messages: '/messages',");
        // Task 044: the header inbox button links it (not the drawer menu).
        Read(Portal, "MemberShell.tsx").Should().Contain("href={portal.messages}").And.NotContain("label: 'Mensagens'");
        Read(Web, "Program.cs").Should().Contain("\"/meetings\", \"/messages\" })").And.Contain("app.MapMessagingEndpoints();");
    }

    [Fact]
    public void NothingOfTheRetiredInboxComesBack()
    {
        foreach (var gone in new[]
                 {
                     Path.Combine(Web, "Pages", "Messages", "Inbox.razor"), Path.Combine(Web, "Hubs", "MessagesHub.cs"),
                     Path.Combine(Web, "Services", "MessagesHubService.cs"), Path.Combine(Web, "wwwroot", "js", "messageComposer.js"),
                     Path.Combine(Web, "wwwroot", "js", "messageScroller.js"), Path.Combine(Web, "wwwroot", "js", "unreadMessages.js"),
                     Path.Combine(Web, "Components", "UnreadMessagesBadge.razor"),
                 })
        {
            File.Exists(gone).Should().BeFalse(gone);
        }

        Directory.GetFiles(Web, "*.razor", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
            && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).Should().BeEmpty("no Blazor messaging UI");
        Read(Web, "Program.cs").Should().NotContain("MapHub").And.NotContain("/hubs/messages\"");
        Read(Web, "RTUB.csproj").Should().NotContain("SignalR");
    }

    [Fact]
    public void MessagingIsTextOnly_NoAttachmentOrMediaPath()
    {
        var page = Read(Portal, "Messages.tsx");
        page.Should().NotContain("type=\"file\"").And.NotContain("FormData").And.NotContain("<video").And.NotContain("<audio")
            .And.NotContain("dangerouslySetInnerHTML").And.NotContain("innerHTML");

        var endpoints = Read(Web, "Endpoints", "MessagingEndpoints.cs");
        endpoints.Should().NotContain("IFormFile").And.NotContain("multipart").And.NotContain("ReadFormAsync").And.NotContain("Upload");

        var contracts = Read(Root, "src", "RTUB.Application", "DTOs", "MessagingDtos.cs");
        Regex.IsMatch(contracts, @"\b(Attachment|Media|File|Image|Video|Upload)\w*\s*[,)]").Should().BeFalse("v2 messages carry text only");

        var service = Read(Root, "src", "RTUB.Application", "Services", "MessagingService.cs");
        service.Should().NotContain("StorageService").And.NotContain("IFormFile").And.NotContain("Stream ");
    }

    [Fact]
    public void TheHeaderBadge_IsOneServerNumber_PolledByThePortal_NoHubNeeded()
    {
        var unread = Read(Portal, "unread.ts");
        unread.Should().Contain("'/api/conversations/unread'").And.Contain("document.visibilityState === 'visible'")
            .And.Contain("'visibilitychange'", "coming back to the tab refreshes it").And.Contain("useSyncExternalStore")
            .And.Contain("publishUnread", "/messages feeds the badge from the list it already loaded");
        unread.Should().NotContain("SignalR").And.NotContain("WebSocket").And.NotContain("EventSource");

        Read(Portal, "Messages.tsx").Should().Contain("publishUnread(o.data)");

        // #226 F12: on /messages the list's own poll feeds the badge, so the shell's 10 s badge poll stands down while the
        // page is mounted (coming back to the tab or online still asks), and resumes when the page goes.
        unread.Should().Contain("setInterval(() => providers === 0 && tick(), POLL_MS)")
            .And.Contain("export function useUnreadProvider()").And.Contain("providers -= 1");
        Read(Portal, "Messages.tsx").Should().Contain("useUnreadProvider();");
        Read(Web, "Endpoints", "MessagingEndpoints.cs").Should().Contain("MapGet(\"/unread\"");

        // Semantics shared with the list: not mine and my id not in ReadBy, summed.
        Read(Root, "src", "RTUB.Application", "Services", "MessagingService.cs").Should()
            .Contain("CountAsync(m => ids.Contains(m.ConversationId) && m.SenderId != me && !m.ReadBy.Contains(me))");
    }

    [Fact]
    public void MessageText_NeverReachesALog_OrAPush()
    {
        var service = Read(Root, "src", "RTUB.Application", "Services", "MessagingService.cs");
        foreach (Match log in Regex.Matches(service, @"_logger\.Log\w+\([^;]*;"))
        {
            log.Value.Should().NotContainAny(new[] { "body", "Body", "Title", "Preview", "Emoji" }, "a log line names ids only: {0}", log.Value);
        }

        var factory = Read(Root, "src", "RTUB.Application", "Factories", "PushNotificationFactory.cs");
        var method = factory[factory.IndexOf("CreateConversationMessageNotification(", StringComparison.Ordinal)..];
        method = method[..method.IndexOf("    /// <summary>", StringComparison.Ordinal)];
        method.Should().NotContain("body").And.Contain("Title = \"Nova mensagem\"").And.Contain("Url = $\"/messages?c={conversationId}\"");
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RTUB.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
