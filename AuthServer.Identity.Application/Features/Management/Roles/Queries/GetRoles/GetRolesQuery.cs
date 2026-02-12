using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuthServer.Identity.Application.Features.Management.Roles.Queries.GetRoles
{
    public class GetRolesQuery : IRequest<ServiceResponse<List<RoleDto>>> { }
}
