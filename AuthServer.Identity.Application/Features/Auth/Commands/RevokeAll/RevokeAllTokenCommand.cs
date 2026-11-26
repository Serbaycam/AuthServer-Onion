using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using MediatR;

namespace AuthServer.Identity.Application.Features.Auth.Commands.RevokeAll
{
    public class RevokeAllTokensCommand : IRequest<ServiceResponse<bool>>, IManagementCommand
    {
        public Guid UserId { get; set; } // Hangi kullanıcının fişini çekeceğiz?
    }
}
