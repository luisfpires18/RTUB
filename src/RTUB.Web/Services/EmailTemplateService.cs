using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace RTUB.Web.Services;

/// <summary>
/// Implementation of email template service using Razor view engine
/// Fixed to use IServiceScopeFactory to avoid ObjectDisposedException when rendering templates in batches
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly IRazorViewEngine _razorViewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EmailTemplateService(
        IRazorViewEngine razorViewEngine,
        ITempDataProvider tempDataProvider,
        IServiceScopeFactory serviceScopeFactory)
    {
        _razorViewEngine = razorViewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<string> RenderTemplateAsync<TModel>(string viewName, TModel model)
    {
        // Create a new scope for this rendering operation to avoid ObjectDisposedException
        using var scope = _serviceScopeFactory.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var actionContext = GetActionContext(serviceProvider);
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

    private ActionContext GetActionContext(IServiceProvider serviceProvider)
    {
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        return new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new ActionDescriptor());
    }
}
