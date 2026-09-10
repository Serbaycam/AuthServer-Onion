using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Login
{
    // Başarılı olursa TokenDto dönecek
    public class LoginCommand : IRequest<ServiceResponse<TokenDto>>
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(256)]
        [System.ComponentModel.DataAnnotations.EmailAddress]
        public string Email { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(256)]
        public string Password { get; set; }
    }
}