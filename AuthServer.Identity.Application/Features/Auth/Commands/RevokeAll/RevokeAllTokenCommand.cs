using MediatR;
using AuthServer.Identity.Application.Wrappers;

namespace AuthServer.Identity.Application.Features.Auth.Commands.RevokeAll
{
    public class RevokeAllTokensCommand : IRequest<ServiceResponse<bool>>
    {
        public Guid UserId { get; set; } // Hangi kullanıcının fişini çekeceğiz?
    }
}