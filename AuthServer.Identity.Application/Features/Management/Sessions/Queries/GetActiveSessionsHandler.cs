using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuthServer.Identity.Application.Features.Management.Sessions.Queries
{
    public class GetActiveSessionsHandler : IRequestHandler<GetActiveSessionsQuery, ServiceResponse<List<ActiveSessionDto>>>
    {
        private readonly IApplicationDbContext _context;

        public GetActiveSessionsHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResponse<List<ActiveSessionDto>>> Handle(GetActiveSessionsQuery request, CancellationToken cancellationToken)
        {
            var activeSessions = await _context.RefreshTokens
                .Include(x => x.User)
                .Where(x => x.RevokedDate == null && x.Expires > DateTime.UtcNow)
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new ActiveSessionDto
                {
                    TokenId = x.Id,
                    UserEmail = x.User.Email,
                    FullName = x.User.FullName,
                    IpAddress = x.CreatedByIp,
                    CreatedDate = x.CreatedDate,
                    ExpirationDate = x.Expires,
                    IsCurrentSession = x.Token == request.CurrentToken
                })
                .ToListAsync(cancellationToken);

            return new ServiceResponse<List<ActiveSessionDto>>(activeSessions, "Aktif oturumlar başarıyla listelendi.");
        }
    }
}
