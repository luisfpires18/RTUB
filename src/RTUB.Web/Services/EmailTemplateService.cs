using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

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

/// <summary>
/// Implementation of email template service using Razor view engine
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly IRazorViewEngine _razorViewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;

    public EmailTemplateService(
        IRazorViewEngine razorViewEngine,
        ITempDataProvider tempDataProvider,
        IServiceProvider serviceProvider)
    {
        _razorViewEngine = razorViewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> RenderTemplateAsync<TModel>(string viewName, TModel model)
    {
        var actionContext = GetActionContext();
        var viewPath = $"~/EmailTemplates/{viewName}.cshtml";
        
        var viewEngineResult = _razorViewEngine.GetView(executingFilePath: null, viewPath: viewPath, isMainPage: false);
        
        if (!viewEngineResult.Success)
        {
            throw new InvalidOperationException($"Could not find view '{viewPath}'");
        }

        var view = viewEngineResult.View;
        
        using var sw = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            view,
            new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            },
            new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
            sw,
            new HtmlHelperOptions()
        );

        await view.RenderAsync(viewContext);
        return sw.ToString();
    }

    private ActionContext GetActionContext()
    {
        var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
        return new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new ActionDescriptor());
    }
}
