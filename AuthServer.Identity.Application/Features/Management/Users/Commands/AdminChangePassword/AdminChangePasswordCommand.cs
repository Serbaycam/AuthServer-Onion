using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.AdminChangePassword
{
    public class AdminChangePasswordCommand : IRequest<ServiceResponse<bool>>
    {
        public Guid UserId { get; set; }
        public string NewPassword { get; set; }

        [JsonIgnore]
        public string? AdminId { get; set; }
        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}