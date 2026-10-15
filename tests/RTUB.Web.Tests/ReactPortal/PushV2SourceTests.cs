using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace RTUB.Web.Tests.ReactPortal;

/// <summary>
/// Task 038 (docs/react-push-v2.md): the member's push settings are a section of the React /profile, over /api/push,
/// with capability detection only; the old Blazor-era client (push-notifications.js, pwa-helper.js) is gone; the
/// service worker shows a notification, opens a same-origin page and keeps a rotated subscription alive without
/// opting the member out. Source contracts: there is no JS test runner, the browser flows were checked by hand.
/// </summary>
public class PushV2SourceTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Portal = Path.Combine(Root, "src", "RTUB.Web", "portal", "src");
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts));
    private static string Push => Read(Portal, "push.ts");
    private static string Profile => Read(Portal, "Profile.tsx");
    private static string Worker => Read(Root, "src", "RTUB.Web", "wwwroot", "service-worker.js");

    [Fact]
    public void Profile_HasThePushSection_SeparateFromEmail()
    {
        Profile.Should().Contain("<PushSection />").And.Contain("title=\"Notificações push\"")
            .And.Contain("title=\"Notificações por email\"");
        Profile.IndexOf("<SubscriptionSection", StringComparison.Ordinal).Should()
            .BeLessThan(Profile.IndexOf("<PushSection />", StringComparison.Ordinal));
    }

    [Fact]
    public void Profile_OffersEveryRepairState_AndNoAutoPrompt()
    {
        foreach (var kind in new[] { "unsupported", "disabled", "not-configured", "no-worker", "denied", "off", "on", "unregistered", "stale", "error" })
        {
            Push.Should().Contain($"'{kind}'", kind);
        }

        Profile.Should().Contain("Corrigir subscrição").And.Contain("Ativar novamente").And.Contain("Ativar neste dispositivo")
            .And.Contain("Desativar neste dispositivo").And.Contain("Desativar em todos os dispositivos")
            .And.Contain("adiciona a RTUB ao ecrã principal");

        // Permission is asked only inside turnOn (called from a button), never while reading the state.
        Regex.Matches(Push, @"requestPermission\(").Count.Should().Be(1);
        var read = Push[Push.IndexOf("export async function readPush", StringComparison.Ordinal)..Push.IndexOf("export type PushResult", StringComparison.Ordinal)];
        read.Should().NotContain("requestPermission").And.NotContain("pushManager.subscribe");
        Profile.Should().NotContain("requestPermission");
        Profile.Should().Contain("useEffect(load, [])").And.Contain("readPush().then(setState)");
    }

    [Fact]
    public void Client_UsesCapabilityDetection_StoresNothing_AndSendsTheBrowsersJson()
    {
        Push.Should().Contain("'serviceWorker' in navigator").And.Contain("'PushManager' in window").And.Contain("'Notification' in window")
            .And.Contain("Notification.permission").And.Contain("pushManager.getSubscription()")
            .And.Contain("userVisibleOnly: true").And.Contain("subscription.toJSON()");

        foreach (var source in new[] { Push, Profile })
        {
            source.Should().NotContain("userAgent").And.NotContain("navigator.platform").And.NotContain("iPhone|")
                .And.NotContain("android-app://");
        }

        Push.Should().NotContain("localStorage").And.NotContain("sessionStorage").And.NotContain("indexedDB");
        Push.Should().Contain("'/api/push/status'").And.Contain("'/api/push/check'").And.Contain("'/api/push/subscribe'")
            .And.Contain("'/api/push/unsubscribe'");
    }

    [Fact]
    public void NothingIsLeftOfTheOldClient_OrOfRemovedModules()
    {
        var web = Path.Combine(Root, "src", "RTUB.Web");
        File.Exists(Path.Combine(web, "Controllers", "PushController.cs")).Should().BeFalse();
        File.Exists(Path.Combine(web, "wwwroot", "js", "push-notifications.js")).Should().BeFalse();
        File.Exists(Path.Combine(web, "wwwroot", "js", "pwa-helper.js")).Should().BeFalse();

        var sources = new[] { Push, Profile, Worker, Read(web, "wwwroot", "js", "sw-register.js") };
        foreach (var source in sources)
        {
            source.Should().NotContainAny("pwaHelper", "PushNotificationsManager", "rtub-push-", "subscription-lost", "/messages",
                "/my-tuno", "/games", "/bets", "_blazor", "blazor.web.js", "RTUB.Shared");
        }

        Push.Should().NotContainAny("migra", "Migra");
        var section = Profile[Profile.IndexOf("function PushSection", StringComparison.Ordinal)..Profile.IndexOf("// ---------- Segurança", StringComparison.Ordinal)];
        section.Should().NotContainAny("migra", "Migra");
    }

    [Fact]
    public void Worker_ShowsTheNotificationInsideWaitUntil_WithoutUserAgentChecks()
    {
        var push = Section(Worker, "addEventListener('push'", "function notificationTarget");

        push.Should().Contain("event.waitUntil(").And.Contain("self.registration.showNotification(");
        push.Should().Contain("baseTag + '-' + Date.now()", "a unique tag keeps browsers from replacing a shown notification");
        push.Should().NotContain("userAgent").And.NotContain("vibrate:").And.NotContain("isIOS");
        Worker.Should().NotContain("AppBadge").And.NotContain("unreadCount");
    }

    [Fact]
    public void Worker_OpensOnlySameOriginPages()
    {
        var target = Section(Worker, "function notificationTarget", "addEventListener('notificationclick'");

        target.Should().Contain("new URL(url, self.location.origin)")
            .And.Contain("target.origin === self.location.origin ? target.href : self.location.origin + '/'");
        Section(Worker, "addEventListener('notificationclick'", "async function antiforgeryToken")
            .Should().Contain("notificationTarget(event.notification.data)").And.Contain("clients.openWindow");

        var register = Read(Root, "src", "RTUB.Web", "wwwroot", "js", "sw-register.js");
        register.Should().Contain("target.origin === window.location.origin");
    }

    [Fact]
    public void Worker_KeepsARotatedSubscription_WithTheToken_AndNeverOptsOut()
    {
        var change = Section(Worker, "addEventListener('pushsubscriptionchange'", "function urlBase64ToUint8Array");

        change.Should().Contain("sendSubscription(subscription, oldEndpoint)").And.Contain("status.optedOut");
        change.Should().NotContain("/api/push/unsubscribe", "a rotation must not opt the member out (the old worker did)");
        Section(Worker, "async function sendSubscription", "addEventListener('pushsubscriptionchange'")
            .Should().Contain("'X-CSRF-TOKEN': await antiforgeryToken()").And.Contain("replaces: replacedEndpoint");
    }

    [Fact]
    public void On_NeedsThisBrowsersSubscription_GrantedPermission_AndTheServersRecord()
    {
        // #230: "Ligadas neste dispositivo" only when the browser holds a subscription, may show notifications, and the
        // server stores that endpoint for the member. Permission alone, or a server row alone, is never "on".
        var read = Section(Push, "export async function readPush", "export type PushResult");

        read.Should().Contain("await reg.pushManager.getSubscription()").And.Contain("pushApi.check(subscription.endpoint)");
        read.Should().Contain("if (!subscription || Notification.permission !== 'granted') return { kind: 'off'");
        read.Should().Contain("return { kind: registered ? 'on' : 'unregistered'");
        Section(Profile, "state.kind === 'on'", "} else {").Should().Contain("Ligadas neste dispositivo");
    }

    [Fact]
    public void TestButton_SendsToThisBrowsersEndpoint_AndClaimsOnlyWhatTheServerKnows()
    {
        Section(Profile, "state.kind === 'on'", "} else {").Should().Contain("'Enviar notificação de teste'").And.Contain("run(sendTest)");

        var test = Section(Push, "export async function sendTest", "export async function turnOff");
        test.Should().Contain("pushApi.test(subscription.endpoint)");
        Push.Should().Contain("'/api/push/test'").And.Contain("Notificação enviada ao serviço push.");
        Push.Should().NotContainAny("Notificação recebida", "foi recebida", "chegou ao dispositivo");
    }

    [Fact]
    public void Worker_ReadsEveryFieldTheServerSends_WithFallbacks()
    {
        var service = Read(Root, "src", "RTUB.Application", "Services", "PushNotificationService.cs");
        var push = Section(Worker, "addEventListener('push'", "function notificationTarget");

        foreach (var field in new[] { "title", "body", "icon", "url", "tag" })
        {
            service.Should().Contain($"{field} = notification.", field);
            push.Should().Contain($"data.{field}", field);
        }

        // Missing fields fall back to defaults; an unparseable payload still shows a notification.
        push.Should().Contain("title: data.title || notificationData.title").And.Contain("event.data.text()");
        push.Should().Contain("return self.registration.showNotification('RTUB'", "a failed icon or option still shows a minimal notification");
    }

    private static string Section(string content, string start, string end)
    {
        var a = content.IndexOf(start, StringComparison.Ordinal);
        a.Should().BeGreaterThan(-1, start);
        var b = content.IndexOf(end, a, StringComparison.Ordinal);
        return b < 0 ? content[a..] : content[a..b];
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RTUB.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
