using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Login
{
    // Başarılı olursa TokenDto dönecek
    public class LoginCommand : IRequest<ServiceResponse<TokenDto>>
    {
        public string Email { get; set; }
        public string Password { get; set; }
        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}