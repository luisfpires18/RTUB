namespace RTUB.Web.Services;

/// <summary>
/// Service for rendering Razor email templates
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Renders a Razor view to string
    /// </summary>
    /// <typeparam name="TModel">The model type</typeparam>
    /// <param name="viewName">Name of the view (without .cshtml extension)</param>
    /// <param name="model">The model instance</param>
    /// <returns>Rendered HTML string</returns>
    Task<string> RenderTemplateAsync<TModel>(string viewName, TModel model);
}
