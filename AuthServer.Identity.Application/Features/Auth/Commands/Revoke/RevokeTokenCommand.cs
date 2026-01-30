using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Revoke
{
    public class RevokeTokenCommand : IRequest<ServiceResponse<bool>>
    {
        public string Token { get; set; }

        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}