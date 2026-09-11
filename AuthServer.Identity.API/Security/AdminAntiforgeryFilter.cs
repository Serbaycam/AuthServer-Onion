using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthServer.Identity.API.Security;

// JWT clients do not use ambient browser credentials. Cookie mutations always need CSRF validation.
public sealed class AdminAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)) return;
        var browserEndpoint = context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller) && controller == "AdminSession";
        if (!browserEndpoint && context.HttpContext.User.Identity?.AuthenticationType != AdminSession.Scheme) return;
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(new
            { succeeded = false, code = "csrf_invalid", message = "Güvenlik doğrulaması yenilenmeli. Tekrar deneyin." });
        }
    }
}
