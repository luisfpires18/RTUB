namespace RTUB.Web.Extensions;

public static class EndpointsBuiltOnceExtensions
{
    /// <summary>
    /// Makes the app build its endpoints once. ASP.NET reads the endpoint sources twice when an app starts (first for
    /// the authorization policy cache, then for the router on the first request), and a minimal-API source compiles
    /// every handler again on each read. With about 400 endpoints that was 1 to 2 seconds of every start, and of every
    /// test host. One caching source in front of the others answers both reads with the same endpoints.
    /// Call it after the last <c>Map...</c> call.
    /// </summary>
    public static void BuildEndpointsOnce(this WebApplication app)
    {
        var sources = ((IEndpointRouteBuilder)app).DataSources;
        var builtOnce = new CompositeEndpointDataSource(sources.ToList());
        sources.Clear();
        sources.Add(builtOnce);
    }
}
