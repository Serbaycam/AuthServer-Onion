using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.CreateRole
{
    public class CreateRoleCommand : IRequest<ServiceResponse<string>>
    {
        public string RoleName { get; set; }
    }
}
