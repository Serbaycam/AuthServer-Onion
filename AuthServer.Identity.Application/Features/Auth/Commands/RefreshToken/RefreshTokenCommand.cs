using MediatR;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Auth.Commands.RefreshToken
{
  public class RefreshTokenCommand : IRequest<ServiceResponse<TokenDto>>
  {
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }

    [JsonIgnore]
    public string? IpAddress { get; set; }
  }
}