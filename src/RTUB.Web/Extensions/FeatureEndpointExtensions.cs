using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Geocoding;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using RTUB.Security;
using RTUB.Web.Endpoints;
using ApplicationUser = RTUB.Core.Entities.ApplicationUser;

namespace RTUB.Web.Extensions;

// The minimal-API endpoints of every React area, in registration order.
public static class FeatureEndpointExtensions
{
    public static WebApplication MapFeatureEndpoints(this WebApplication app)
    {
        // --------- Public request API for the React portal (React track 003) ---------
        app.MapPublicRequestEndpoints();

        // --------- Music API for the React Music area (React track 006) ---------
        app.MapMusicEndpoints();

        // --------- Public Órgãos Sociais for the React /roles (React track 008) ---------
        app.MapGovernanceEndpoints();

        // --------- Gallery timeline for the React /gallery (React track 009) ---------
        app.MapGalleryEndpoints();

        // --------- Events API for the React /events and the home preview (React tracks 010, 011) ---------
        app.MapEventEndpoints();

        // --------- Rehearsals API for the React /rehearsals (React track 014) ---------
        app.MapRehearsalEndpoints();

        // --------- Members area for the React /members (React track 017) ---------
        app.MapMemberEndpoints();
        app.MapLeaderboardEndpoints();
        app.MapInventoryEndpoints();
        app.MapShopEndpoints();
        app.MapDocumentationEndpoints();
        app.MapLogisticsEndpoints();
        app.MapPendingActionsEndpoints();
        app.MapTreasuryEndpoints();

        // --------- Public "Novidades" feed for the React /news (React track 025) ---------
        app.MapNewsEndpoints();

        // --------- Requests admin and Perguntas for the React /requests and /questions (task 031) ---------
        app.MapRequestAdminEndpoints();
        app.MapQuestionEndpoints();

        // --------- Messaging v2 for the React /messages over the preserved v1 tables (task 041) ---------
        app.MapMessagingEndpoints();

        // --------- Profile, members map and Hall of Fame for the React /profile, /members/map and /hall-of-fame (task 032) ---------
        app.MapMemberAreaEndpoints();

        // --------- Naipes for the React /naipes and /naipes/config (task 033) ---------
        app.MapNaipeEndpoints();

        // --------- Meetings for the React /meetings (task 034) ---------
        app.MapMeetingEndpoints();

        // --------- Owner tools for the React /emails, /notifications, /users, /owner/tracing and /owner/db (task 035) ---------
        app.MapOwnerEndpoints();

        // --------- Password recovery and email confirmation for the React identity pages (task 036) ---------
        app.MapAccountRecoveryEndpoints();

        // --------- The member's own push subscriptions for the React /profile (task 038) ---------
        app.MapPushEndpoints();

        // --------- Release notes for signed-in members (task 083, #264) ---------
        app.MapReleaseEndpoints();

        return app;
    }
}
