using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuthServer.Identity.Application.Features.Management.Sessions.Queries
{
    public class GetActiveSessionsQuery : IRequest<ServiceResponse<List<ActiveSessionDto>>>
    {
        public string? CurrentToken { get; set; }
    }
}
