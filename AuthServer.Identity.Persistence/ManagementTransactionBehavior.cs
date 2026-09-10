using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AuthServer.Identity.Persistence;

// Identity APIs save independently; one transaction keeps a management command atomic.
public sealed class ManagementTransactionBehavior<TRequest, TResponse>(AppDbContext db,
    AuthServer.Identity.Application.Interfaces.IAuditService audit,
    AuthServer.Identity.Application.Interfaces.ICurrentUserService actor)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var ns = typeof(TRequest).Namespace ?? "";
        if (!ns.Contains(".Management.") || !ns.Contains(".Commands.")) return await next();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var response = await next();
        if (response is IServiceResponse { Succeeded: true })
        {
            // Do not serialize command bodies: password/token commands contain secrets.
            await audit.LogAsync(actor.UserId ?? "System", typeof(TRequest).Name, "ManagementCommand", "",
                new { Command = typeof(TRequest).Name }, actor.IpAddress ?? "Unknown");
            await transaction.CommitAsync(cancellationToken);
        }
        else await transaction.RollbackAsync(cancellationToken);
        return response;
    }
}
