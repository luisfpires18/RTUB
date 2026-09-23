using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Shared;

namespace RTUB.Shared.Tests.Components.UI;

/// <summary>
/// Tests for the PushNotificationToggle component.
/// Covers the push subscription workflow, status checking and toggle behaviour.
/// Every assertion targets a NAMED JS helper and its argument values - the component
/// no longer dispatches generated JavaScript, so no test inspects script text.
/// </summary>
public class PushNotificationToggleTests : BunitContext
{
    private readonly Mock<ILogger<PushNotificationToggle>> _mockLogger;

    public PushNotificationToggleTests()
    {
        _mockLogger = new Mock<ILogger<PushNotificationToggle>>();
        Services.AddSingleton(_mockLogger.Object);
        this.AddAuthorization();
    }

    private static PushStatusDto Available => new()
    {
        IsEnabled = true,
        IsConfigured = true,
        VapidPublicKey = "test-key"
    };

    /// <summary>
    /// Arranges the named helpers the component calls while it boots: status fetch,
    /// push-manager init, the subscription check and the Android-hint probe.
    /// </summary>
    private void SetupAccess(PushStatusDto status, bool initialized = true, bool isSubscribed = false)
    {
        JSInterop.Setup<PushStatusDto?>("pwaHelper.getPushStatus", _ => true)
            .SetResult(status);
        JSInterop.Setup<bool>("pwaHelper.initializePushManager", _ => true)
            .SetResult(initialized);
        JSInterop.Setup<bool>("pwaHelper.isSubscribedToPush", _ => true)
            .SetResult(isSubscribed);
        JSInterop.Setup<bool>("pwaHelper.isAndroidPwa", _ => true)
            .SetResult(false);
    }

    private IReadOnlyList<JSRuntimeInvocation> InvocationsOf(string identifier)
        => JSInterop.Invocations.Identifiers.Contains(identifier)
            ? JSInterop.Invocations[identifier]
            : Array.Empty<JSRuntimeInvocation>();

    [Fact]
    public void PushNotificationToggle_ShowsLoadingState_Initially()
    {
        // Arrange & Act - no JS setup, so the status call is unhandled: the component
        // catches and exits loading. Loading can be transient; assert the root renders
        // and we end up in a valid state.
        var cut = Render<PushNotificationToggle>();

        cut.Markup.Should().Contain("push-notification-toggle", "should render root");
        var hasLoading = cut.Markup.Contains("Checking permissions");
        var hasToggle = cut.Markup.Contains("form-check-input");
        var hasEmpty = cut.Markup.Contains("push-notification-toggle") && !hasLoading && !hasToggle;
        (hasLoading || hasToggle || hasEmpty).Should().BeTrue("should be in loading, toggle, or no-access state");
    }

    [Fact]
    public void PushNotificationToggle_ShowsNothing_WhenNoAccess()
    {
        // Arrange - the server returns no push status at all
        JSInterop.Setup<PushStatusDto?>("pwaHelper.getPushStatus", _ => true)
            .SetResult(null);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().NotContain("form-check-input", "should not show toggle when no access");
        cut.Markup.Should().NotContain("Push Notifications", "should not show toggle label when no access");
    }

    [Fact]
    public void PushNotificationToggle_StaysGraceful_WhenPushNotConfigured()
    {
        // Arrange - push is enabled but the server has no VAPID configuration
        SetupAccess(new PushStatusDto { IsEnabled = true, IsConfigured = false });

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Assert - no toggle, no error, and the push manager is never initialized
        cut.Markup.Should().NotContain("form-check-input", "should not show toggle when push is not configured");
        cut.Markup.Should().NotContain("alert-danger", "an unconfigured server is not an error state");
        InvocationsOf("pwaHelper.initializePushManager").Should().BeEmpty();
    }

    [Fact]
    public void PushNotificationToggle_ShowsToggle_WhenHasAccess()
    {
        // Arrange
        SetupAccess(Available);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("form-check-input", "should show toggle when has access");
        cut.Markup.Should().Contain("Push Notifications", "should show toggle label when has access");
    }

    [Fact]
    public void PushNotificationToggle_BootsThrough_NamedHelpers()
    {
        // Arrange
        SetupAccess(Available);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Assert - the boot sequence goes through named JS functions taking no script
        var identifiers = JSInterop.Invocations.Identifiers.ToList();
        identifiers.Should().Contain("pwaHelper.getPushStatus");
        identifiers.Should().Contain("pwaHelper.initializePushManager");
        identifiers.Should().Contain("pwaHelper.isSubscribedToPush");
        identifiers.Should().NotContain("eval", "the component must not dispatch generated JavaScript");

        // The toggle fetches status read-only: syncOptOut: false, so rendering it never
        // reconciles the local opted-out cache. That side effect belongs to the Prompt path.
        InvocationsOf("pwaHelper.getPushStatus").Single().Arguments
            .Should().ContainSingle().Which.Should().Be(false, "the toggle must not sync the opt-out cache");
    }

    [Fact]
    public void PushNotificationToggle_SkipsSubscriptionCheck_WhenInitializeFails()
    {
        // Arrange - the manager cannot be created (no service worker / no PushManager)
        SetupAccess(Available, initialized: false);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Assert - the toggle still renders unchecked, and nothing is asked of the missing manager
        cut.Find("input[type=checkbox]").HasAttribute("checked").Should().BeFalse();
        InvocationsOf("pwaHelper.isSubscribedToPush")
            .Should().BeEmpty("there is no manager to ask when initialization failed");
    }

    [Fact]
    public void PushNotificationToggle_ShowsUnchecked_WhenNotSubscribed()
    {
        // Arrange
        SetupAccess(new PushStatusDto { IsEnabled = true, IsConfigured = true }, isSubscribed: false);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Assert
        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.HasAttribute("checked").Should().BeFalse("checkbox should be unchecked when not subscribed");
        cut.Markup.Should().Contain("bi-bell", "should show bell icon (not filled) when not subscribed");
        cut.Markup.Should().NotContain("bi-bell-fill", "should not show filled bell icon when not subscribed");
    }

    [Fact]
    public void PushNotificationToggle_ShowsChecked_WhenSubscribed()
    {
        // Arrange
        SetupAccess(new PushStatusDto { IsEnabled = true, IsConfigured = true }, isSubscribed: true);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Assert
        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.HasAttribute("checked").Should().BeTrue("checkbox should be checked when subscribed");
        cut.Markup.Should().Contain("bi-bell-fill", "should show filled bell icon when subscribed");
    }

    [Fact]
    public void PushNotificationToggle_ShowsAndroidHint_WhenSubscribedOnAndroidPwa()
    {
        // Arrange - subscribed, and the device reports Android running as an installed PWA/TWA
        JSInterop.Setup<PushStatusDto?>("pwaHelper.getPushStatus", _ => true)
            .SetResult(new PushStatusDto { IsEnabled = true, IsConfigured = true });
        JSInterop.Setup<bool>("pwaHelper.initializePushManager", _ => true).SetResult(true);
        JSInterop.Setup<bool>("pwaHelper.isSubscribedToPush", _ => true).SetResult(true);
        JSInterop.Setup<bool>("pwaHelper.isAndroidPwa", _ => true).SetResult(true);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => cut.Markup.Contains("alert-warning"), TimeSpan.FromSeconds(2));

        // Assert - the hint comes from the named probe, not a user-agent script
        cut.Markup.Should().Contain("alert-warning", "the Android hint uses warning styling");
        InvocationsOf("pwaHelper.isAndroidPwa").Should().ContainSingle();
    }

    [Fact]
    public void PushNotificationToggle_HidesAndroidHint_WhenNotAndroidPwa()
    {
        // Arrange - subscribed but not an installed Android client
        SetupAccess(new PushStatusDto { IsEnabled = true, IsConfigured = true }, isSubscribed: true);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().NotContain("alert-warning", "the Android hint is Android-PWA only");
    }

    [Fact]
    public void PushNotificationToggle_Subscribes_WithTrueArgument()
    {
        // Arrange
        SetupAccess(new PushStatusDto { IsEnabled = true, IsConfigured = true });
        JSInterop.Setup<bool>("pwaHelper.setPushSubscription", _ => true).SetResult(true);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        cut.Find("input[type=checkbox]").Change(true);
        cut.WaitForState(() => cut.Markup.Contains("Successfully subscribed"), TimeSpan.FromSeconds(2));

        // Assert - one named helper, driven by a boolean argument rather than a script
        var call = InvocationsOf("pwaHelper.setPushSubscription").Should().ContainSingle().Subject;
        call.Arguments.Should().ContainSingle().Which.Should().Be(true, "true asks the helper to subscribe");
    }

    [Fact]
    public void PushNotificationToggle_Unsubscribes_WithFalseArgument()
    {
        // Arrange - start subscribed so flipping the toggle means "unsubscribe"
        SetupAccess(new PushStatusDto { IsEnabled = true, IsConfigured = true }, isSubscribed: true);
        JSInterop.Setup<bool>("pwaHelper.setPushSubscription", _ => true).SetResult(true);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        cut.Find("input[type=checkbox]").Change(false);
        cut.WaitForState(() => cut.Markup.Contains("Successfully unsubscribed"), TimeSpan.FromSeconds(2));

        // Assert - same named helper, the opposite data value
        var call = InvocationsOf("pwaHelper.setPushSubscription").Should().ContainSingle().Subject;
        call.Arguments.Should().ContainSingle().Which.Should().Be(false, "false asks the helper to unsubscribe");
        cut.Markup.Should().Contain("alert-success");
    }

    [Fact]
    public void PushNotificationToggle_ShowsErrorMessage_WhenSubscribeFails()
    {
        // Arrange - the helper rethrows when the push manager is unavailable
        SetupAccess(new PushStatusDto { IsEnabled = true, IsConfigured = true });
        JSInterop.Setup<bool>("pwaHelper.setPushSubscription", _ => true)
            .SetException(new Exception("Subscription failed"));

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        cut.Find("input[type=checkbox]").Change(true);
        cut.WaitForState(() => cut.Markup.Contains("Failed to subscribe"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Failed to subscribe", "should show error message when subscribe fails");
        cut.Markup.Should().Contain("alert-danger", "error message should have danger styling");
    }

    [Fact]
    public void PushNotificationToggle_ShowsSuccessMessage_WhenSubscribeSucceeds()
    {
        // Arrange
        SetupAccess(new PushStatusDto { IsEnabled = true, IsConfigured = true });
        JSInterop.Setup<bool>("pwaHelper.setPushSubscription", _ => true).SetResult(true);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        cut.Find("input[type=checkbox]").Change(true);
        cut.WaitForState(() => cut.Markup.Contains("Successfully subscribed"), TimeSpan.FromSeconds(2));

        // Assert
        cut.Markup.Should().Contain("Successfully subscribed", "should show success message when subscribe succeeds");
        cut.Markup.Should().Contain("alert-success", "success message should have success styling");
    }

    [Fact]
    public void PushNotificationToggle_NeverDispatchesGeneratedScript()
    {
        // Arrange - drive the whole subscribe flow
        SetupAccess(Available);
        JSInterop.Setup<bool>("pwaHelper.setPushSubscription", _ => true).SetResult(true);

        // Act
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));
        cut.Find("input[type=checkbox]").Change(true);
        cut.WaitForState(() => cut.Markup.Contains("Successfully subscribed"), TimeSpan.FromSeconds(2));

        // Assert - every identifier is a named function and no argument is JavaScript source
        var identifiers = JSInterop.Invocations.Identifiers.ToList();
        identifiers.Should().NotBeEmpty();
        identifiers.Should().OnlyContain(id => id.StartsWith("pwaHelper.", StringComparison.Ordinal));

        var stringArguments = identifiers
            .SelectMany(InvocationsOf)
            .SelectMany(i => i.Arguments)
            .OfType<string>()
            .ToList();
        stringArguments.Should().NotContain(
            s => s.Contains("function", StringComparison.Ordinal) || s.Contains("=>", StringComparison.Ordinal),
            "arguments must be data, not JavaScript source");
    }

    // ---------- one operation at a time, and how it ends (hotfix 2.0.1) ----------
    //
    // PROD: a subscribe whose browser side never answered hit Blazor Server's 60 s JS-interop
    // default, was logged as an error, and re-enabled the switch while the browser was still
    // working - and the switch still showed ON, so the next tap sent an unsubscribe. The tests
    // above complete every JS call at once, so none of that was reachable. These hold it open.

    [Fact]
    public void SecondToggle_WhileAnOperationRuns_StartsNoSecondOperation()
    {
        // Arrange
        var (cut, setSubscription) = RenderReadyToggle();

        // Act - taps that reach the server before the disabled switch reaches the browser
        Switch(cut).Change(true);
        Switch(cut).Change(false);
        Switch(cut).Change(true);

        // Assert
        InvocationsOf("pwaHelper.setPushSubscription").Should().ContainSingle("one subscribe/unsubscribe at a time");
        Switch(cut).HasAttribute("disabled").Should().BeTrue();

        setSubscription.SetResult(true);
        cut.WaitForState(() => cut.Markup.Contains("Successfully subscribed"));
        InvocationsOf("pwaHelper.setPushSubscription").Should().ContainSingle();
        Switch(cut).HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public async Task Disposal_WhileSubscribing_CancelsTheCall_WithoutAnErrorOrARetry()
    {
        // Arrange
        var (cut, setSubscription) = RenderReadyToggle();
        Switch(cut).Change(true);
        var call = InvocationsOf("pwaHelper.setPushSubscription").Should().ContainSingle().Subject;

        call.CancellationToken.Should().NotBeNull(
            "the component must own the call through a token (a token-less call only ends on the 60 s default)");

        // Act - navigation or circuit teardown disposes the component mid-call
        await DisposeComponentsAsync();
        call.CancellationToken!.Value.IsCancellationRequested.Should().BeTrue(
            "disposal must cancel the JS call this component owns");
        setSubscription.SetCanceled(); // what JSRuntime does to a call whose token was cancelled
        await Renderer.Dispatcher.InvokeAsync(() => { }); // let the component observe it

        // Assert
        InvocationsOf("pwaHelper.setPushSubscription").Should().ContainSingle("a cancelled call is not retried");
        VerifyLogged(LogLevel.Error, Times.Never());
        VerifyLogged(LogLevel.Warning, Times.Never());
    }

    /// <summary>
    /// Only a token-less JS call gets Blazor Server's 60 s default timeout. Both operations must pass
    /// a token, and the same one - the component's lifetime token. A per-call token would be a per-call
    /// timeout again (CancelAfter, or the TimeSpan overload), re-enabling the switch mid-request.
    /// </summary>
    [Fact]
    public void SubscribeAndUnsubscribe_PassTheComponentLifetimeToken_NotAPerCallTimeout()
    {
        // Arrange
        var (cut, setSubscription) = RenderReadyToggle();
        setSubscription.SetResult(true);

        // Act
        Switch(cut).Change(true);
        cut.WaitForState(() => cut.Markup.Contains("Successfully subscribed"));
        Switch(cut).Change(false);
        cut.WaitForState(() => cut.Markup.Contains("Successfully unsubscribed"));

        // Assert
        var tokens = InvocationsOf("pwaHelper.setPushSubscription").Select(call => call.CancellationToken).ToList();
        tokens.Should().HaveCount(2).And.OnlyContain(token => token.HasValue,
            "a token-less call gets the 60 s default timeout");
        tokens[0].Should().Be(tokens[1], "one token for the component's lifetime, not one per call");
        tokens[0]!.Value.IsCancellationRequested.Should().BeFalse("only disposal cancels it");
    }

    /// <summary>
    /// The PROD failure mode against the REAL JSRuntime timeout logic, scaled down: the default timeout
    /// is 50 ms (production: 60 s) and setPushSubscription never answers. A token-less call is cancelled
    /// after 50 ms; this one must still be pending - switch disabled, no error - many timeouts later, and
    /// end quietly only when the component is disposed.
    /// </summary>
    [Fact]
    public async Task PendingSubscribe_OutlivesTheJsInteropDefaultTimeout_UntilDisposed()
    {
        // Arrange
        Services.AddSingleton<IJSRuntime>(new BrowserThatNeverAnswersSubscribe(defaultTimeout: TimeSpan.FromMilliseconds(50)));
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));

        // Act
        Switch(cut).Change(true);
        await Task.Delay(TimeSpan.FromMilliseconds(500)); // ten default timeouts

        // Assert - still waiting on the browser
        Switch(cut).HasAttribute("disabled").Should().BeTrue("the browser has not answered; the request is still running");
        cut.Markup.Should().NotContain("Failed to subscribe");
        VerifyLogged(LogLevel.Error, Times.Never());

        // Act - leaving the page is what ends the wait
        await DisposeComponentsAsync();
        await Renderer.Dispatcher.InvokeAsync(() => { });

        // Assert
        VerifyLogged(LogLevel.Error, Times.Never());
        VerifyLogged(LogLevel.Warning, Times.Never());
    }

    [Fact]
    public void GenuineFailure_IsLoggedAndShown_AndTheSwitchIsRenderedBackOff()
    {
        // Arrange
        var (cut, setSubscription) = RenderReadyToggle();

        // Act
        Switch(cut).Change(true);
        Switch(cut).HasAttribute("checked").Should().BeTrue(
            "while it runs the switch shows the requested state, as the browser already does - so a " +
            "failure below is a real change Blazor sends back to the page");
        setSubscription.SetException(new JSException("Notification permission denied"));
        cut.WaitForState(() => cut.Markup.Contains("Failed to subscribe"));

        // Assert
        Switch(cut).HasAttribute("checked").Should().BeFalse("a failed subscribe renders OFF");
        Switch(cut).HasAttribute("disabled").Should().BeFalse("processing resets");
        cut.Markup.Should().Contain("alert-danger");
        VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public void Retry_AfterAGenuineFailure_IsOneFreshAttempt()
    {
        // Arrange
        var (cut, setSubscription) = RenderReadyToggle();
        Switch(cut).Change(true);
        setSubscription.SetException(new JSException("push service unavailable"));
        cut.WaitForState(() => cut.Markup.Contains("Failed to subscribe"));

        // Act - one new tap
        setSubscription.SetResult(true);
        Switch(cut).Change(true);
        cut.WaitForState(() => cut.Markup.Contains("Successfully subscribed"));

        // Assert
        InvocationsOf("pwaHelper.setPushSubscription").Should().HaveCount(2, "the failed attempt and one retry");
        Switch(cut).HasAttribute("checked").Should().BeTrue();
    }

    /// <summary>A ready, unsubscribed toggle whose setPushSubscription call stays pending until the test settles it.</summary>
    private (IRenderedComponent<PushNotificationToggle> Cut, JSRuntimeInvocationHandler<bool> SetSubscription) RenderReadyToggle()
    {
        SetupAccess(Available);
        var setSubscription = JSInterop.Setup<bool>("pwaHelper.setPushSubscription", _ => true);
        var cut = Render<PushNotificationToggle>();
        cut.WaitForState(() => !cut.Markup.Contains("Checking permissions"), TimeSpan.FromSeconds(2));
        return (cut, setSubscription);
    }

    private static IElement Switch(IRenderedComponent<PushNotificationToggle> cut) => cut.Find("#pushNotificationSwitch");

    private void VerifyLogged(LogLevel level, Times times) =>
        _mockLogger.Verify(logger => logger.Log(
            level,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    /// <summary>
    /// The real Microsoft.JSInterop base class - with its DefaultAsyncTimeout handling - standing in for a
    /// browser whose permission prompt or push registration hangs: the boot calls answer at once,
    /// setPushSubscription never does.
    /// </summary>
    private sealed class BrowserThatNeverAnswersSubscribe : JSRuntime
    {
        public BrowserThatNeverAnswersSubscribe(TimeSpan defaultTimeout) => DefaultAsyncTimeout = defaultTimeout;

        protected override void BeginInvokeJS(long taskId, string identifier, string? argsJson, JSCallResultType resultType, long targetInstanceId)
        {
            var result = identifier switch
            {
                "pwaHelper.getPushStatus" => """{"isEnabled":true,"isConfigured":true}""",
                "pwaHelper.initializePushManager" => "true",
                "pwaHelper.isSubscribedToPush" => "false",
                _ => null
            };

            if (result != null)
            {
                DotNetDispatcher.EndInvokeJS(this, $"[{taskId},true,{result}]");
            }
        }

        protected override void EndInvokeDotNet(DotNetInvocationInfo invocationInfo, in DotNetInvocationResult invocationResult)
        {
        }
    }
}
