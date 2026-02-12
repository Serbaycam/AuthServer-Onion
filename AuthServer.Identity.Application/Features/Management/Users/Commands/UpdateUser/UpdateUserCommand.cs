using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.UpdateUser
{
    public class UpdateUserCommand : IRequest<ServiceResponse<bool>>
    {
        public Guid UserId { get; set; } // Hangi kullanıcı?
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }

        // Audit Log İçin
        [JsonIgnore]
        public string? AdminId { get; set; }
        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}