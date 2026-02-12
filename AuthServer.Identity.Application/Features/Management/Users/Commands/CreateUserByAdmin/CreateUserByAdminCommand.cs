using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.CreateUserByAdmin
{
    public class CreateUserByAdminCommand : IRequest<ServiceResponse<Guid>>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public List<string> Roles { get; set; } // Örn: ["LabManager"]

        // Audit Log için
        [JsonIgnore]
        public string? AdminId { get; set; }
        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}