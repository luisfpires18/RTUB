namespace RTUB.Web.Extensions;

/// <summary>
/// Old page addresses that members may still have bookmarked or in old emails. Each one sends the browser (302) to
/// the page that does the same job today. Nothing in the app links to them any more. GET/HEAD only, so nothing is ever
/// submitted to them.
/// </summary>
public static class LegacyRedirectExtensions
{
    public static WebApplication MapLegacyRedirects(this WebApplication app)
    {
        foreach (var route in new[] { "/", "/privacy", "/profile", "/request" })
        {
            app.MapMethods("/portal" + route.TrimEnd('/'), ["GET", "HEAD"], (HttpContext context) =>
                Results.Redirect(route + context.Request.QueryString));
        }

        app.MapMethods("/music/songs/{id:int}", ["GET", "HEAD"], (int id, HttpContext context) =>
            Results.Redirect($"/music/albums/{id}" + context.Request.QueryString));

        // Answering an event happens in a modal on the event page; the answer list is its "Quem vai / Quem foi".
        app.MapMethods("/events/{id:int}/enrollment", ["GET", "HEAD"], (int id) =>
            Results.Redirect($"/events/{id}?respond=1"));
        app.MapMethods("/events/{id:int}/enrollments", ["GET", "HEAD"], (int id) =>
            Results.Redirect($"/events/{id}#who-title"));

        // The query is kept: ?openModal=true&name=... opens the create form prefilled.
        app.MapMethods("/member/events", ["GET", "HEAD"], (HttpContext context) =>
            Results.Redirect("/events" + context.Request.QueryString));
        app.MapMethods("/member/gallery", ["GET", "HEAD"], () => Results.Redirect("/gallery"));
        app.MapMethods("/member/roles", ["GET", "HEAD"], (HttpContext context) =>
            Results.Redirect("/roles" + context.Request.QueryString));
        app.MapMethods("/hierarchy", ["GET", "HEAD"], () => Results.Redirect("/members/hierarchy"));
        app.MapMethods("/member/profile", ["GET", "HEAD"], () => Results.Redirect("/profile"));
        app.MapMethods("/member/map", ["GET", "HEAD"], () => Results.Redirect("/members/map"));
        app.MapMethods("/members/manage", ["GET", "HEAD"], () => Results.Redirect("/members"));

        // Tesouraria. /calotes keeps its ?fy=; the old MBWay and Nerba ids were only for a back button.
        app.MapMethods("/finance", ["GET", "HEAD"], () => Results.Redirect("/treasury"));
        app.MapMethods("/finance/report/{id:int}", ["GET", "HEAD"], (int id) => Results.Redirect($"/treasury/reports/{id}"));
        app.MapMethods("/calotes", ["GET", "HEAD"], (HttpContext context) =>
            Results.Redirect("/treasury/calotes" + (context.Request.Query["fy"].ToString() is { Length: > 0 } fy ? "?fy=" + Uri.EscapeDataString(fy) : "")));
        foreach (var route in new[] { "/mbway", "/mbway/{id:int}" })
        {
            app.MapMethods(route, ["GET", "HEAD"], () => Results.Redirect("/treasury/mbway"));
        }

        foreach (var route in new[] { "/nerba", "/nerba/{id:int}" })
        {
            app.MapMethods(route, ["GET", "HEAD"], () => Results.Redirect("/treasury/nerba"));
        }

        app.MapMethods("/nerba/event/{id:int}", ["GET", "HEAD"], (int id) => Results.Redirect($"/treasury/nerba/{id}"));

        // The homepage-slideshow admin has no replacement: a plain 404. /images/<file> stays a static file.
        app.MapMethods("/images", ["GET", "HEAD"], () => Results.NotFound());

        return app;
    }
}
