using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AuthServer.Identity.Persistence;

// Identity APIs save independently; one transaction keeps a management command atomic.
public sealed class ManagementTransactionBehavior<TRequest, TResponse>(AppDbContext db)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IManagementCommand) return await next();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var response = await next();
        if (response is IServiceResponse { Succeeded: true })
        {
            // The handler writes one target-aware audit event inside this transaction.
            await transaction.CommitAsync(cancellationToken);
        }
        else await transaction.RollbackAsync(cancellationToken);
        return response;
    }
}
