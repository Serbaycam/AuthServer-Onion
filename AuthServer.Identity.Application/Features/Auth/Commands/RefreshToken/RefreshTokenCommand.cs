using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Auth.Commands.RefreshToken
{
    public class RefreshTokenCommand : IRequest<ServiceResponse<TokenDto>>
    {
        public string? AccessToken { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(1024)]
        public string RefreshToken { get; set; }
    }
}