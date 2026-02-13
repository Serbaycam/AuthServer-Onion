using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Management.Users.Queries
{
    public class GetUsersWithRolesQuery : IRequest<ServiceResponse<List<UserWithRolesDto>>> { }

    public class GetUsersWithRolesHandler : IRequestHandler<GetUsersWithRolesQuery, ServiceResponse<List<UserWithRolesDto>>>
    {
        private readonly UserManager<AppUser> _userManager;

        public GetUsersWithRolesHandler(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<ServiceResponse<List<UserWithRolesDto>>> Handle(GetUsersWithRolesQuery request, CancellationToken cancellationToken)
        {
            var users = await _userManager.Users.ToListAsync(cancellationToken);
            var userListWithRoles = new List<UserWithRolesDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userListWithRoles.Add(new UserWithRolesDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsActive = user.IsActive,
                    Roles = roles.ToList()
                });
            }

            return new ServiceResponse<List<UserWithRolesDto>>(userListWithRoles, "Kullanıcı listesi başarıyla getirildi.");
        }
    }
}