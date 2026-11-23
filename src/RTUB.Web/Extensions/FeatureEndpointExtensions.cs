using RTUB.Web.Endpoints;

namespace RTUB.Web.Extensions;

// The minimal-API endpoints of every React area, in registration order.
public static class FeatureEndpointExtensions
{
    public static WebApplication MapFeatureEndpoints(this WebApplication app)
    {
        // --------- Public request API for the React portal ---------
        app.MapPublicRequestEndpoints();

        // --------- Music API for the React Music area ---------
        app.MapMusicEndpoints();

        // --------- Public Órgãos Sociais for the React /roles ---------
        app.MapGovernanceEndpoints();

        // --------- Gallery timeline for the React /gallery ---------
        app.MapGalleryEndpoints();

        // --------- Events API for the React /events and the home preview ---------
        app.MapEventEndpoints();

        // --------- Rehearsals API for the React /rehearsals ---------
        app.MapRehearsalEndpoints();

        // --------- Members area for the React /members ---------
        app.MapMemberEndpoints();
        app.MapLeaderboardEndpoints();
        app.MapTrintunaEndpoints();
        app.MapInventoryEndpoints();
        app.MapShopEndpoints();
        app.MapDocumentationEndpoints();
        app.MapLogisticsEndpoints();
        app.MapPendingActionsEndpoints();
        app.MapTreasuryEndpoints();

        // --------- Public "Novidades" feed for the React /news ---------
        app.MapNewsEndpoints();

        // --------- Requests admin and Perguntas for the React /requests and /questions ---------
        app.MapRequestAdminEndpoints();
        app.MapQuestionEndpoints();

        // --------- Messaging v2 for the React /messages over the preserved v1 tables ---------
        app.MapMessagingEndpoints();

        // --------- Profile, members map and Hall of Fame for the React /profile, /members/map and /hall-of-fame ---------
        app.MapMemberAreaEndpoints();

        // --------- Naipes for the React /naipes and /naipes/config ---------
        app.MapNaipeEndpoints();

        // --------- Meetings for the React /meetings ---------
        app.MapMeetingEndpoints();

        // --------- Owner tools for the React /emails, /notifications, /users, /owner/tracing and /owner/db ---------
        app.MapOwnerEndpoints();

        // --------- Password recovery and email confirmation for the React identity pages ---------
        app.MapAccountRecoveryEndpoints();

        // --------- The member's own push subscriptions for the React /profile ---------
        app.MapPushEndpoints();

        // --------- Release notes for signed-in members ---------
        app.MapReleaseEndpoints();

        return app;
    }
}
