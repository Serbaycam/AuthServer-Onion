using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Revoke
{
    public class RevokeTokenCommand : IRequest<ServiceResponse<bool>>
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(1024)]
        public string Token { get; set; }
    }
}