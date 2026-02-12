using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuthServer.Identity.Application.Features.Management.Sessions.Commands.KillSession
{
    public class KillSessionHandler : IRequestHandler<KillSessionCommand, ServiceResponse<bool>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public KillSessionHandler(IApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<ServiceResponse<bool>> Handle(KillSessionCommand request, CancellationToken cancellationToken)
        {
            var token = await _context.RefreshTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == request.TokenId, cancellationToken);

            if (token == null) return new ServiceResponse<bool>("Oturum bulunamadı.");
            if (token.RevokedDate != null) return new ServiceResponse<bool>("Bu oturum zaten sonlandırılmış.");

            // Token'ı iptal et
            token.RevokedDate = DateTime.UtcNow;
            token.RevokedByIp = request.IpAddress;
            token.ReasonRevoked = "Terminated by Administrator (Kill Session)";

            await _context.SaveChangesAsync(cancellationToken);

            // Audit Log
            await _auditService.LogAsync(
                request.AdminId ?? "System",
                "KillSession",
                "RefreshToken",
                token.Id.ToString(),
                new { TargetUser = token.User.Email, TokenId = token.Id },
                request.IpAddress ?? "Unknown"
            );

            return new ServiceResponse<bool>(true, "Oturum başarıyla sonlandırıldı.");
        }
    }
}
