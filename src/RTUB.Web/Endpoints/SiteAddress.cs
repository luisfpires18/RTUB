namespace RTUB.Web.Endpoints;

/// <summary>
/// The site address that links in emails are built from (the password-reset link, the event cancellation and notice
/// links): the configured <c>App:BaseUrl</c>. A request's Host header is the caller's word, so it is used only where
/// nothing is configured (a local run).
/// </summary>
public static class SiteAddress
{
    public static string Of(HttpContext http) =>
        http.RequestServices.GetRequiredService<IConfiguration>()["App:BaseUrl"] is { Length: > 0 } configured
            ? configured.TrimEnd('/')
            : $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}";
}
