using AuthServer.Identity.Application.Wrappers;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.API.Middlewares;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Request failed. TraceId: {TraceId}", context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = exception is DbUpdateConcurrencyException ||
                exception is Npgsql.PostgresException { SqlState: "40001" or "40P01" } ||
                exception.InnerException is Npgsql.PostgresException { SqlState: "40001" or "40P01" } ? 409 : 500;
            await context.Response.WriteAsJsonAsync(new ServiceResponse<string>(
                context.Response.StatusCode == 409
                    ? "Kayıt başka bir işlem tarafından değiştirildi. Tekrar deneyin."
                    : $"İşlem tamamlanamadı. Referans: {context.TraceIdentifier}"));
        }
    }
}
