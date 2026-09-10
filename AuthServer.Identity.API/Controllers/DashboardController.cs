using AuthServer.Identity.Application.Features.Management.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Identity.API.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly IMediator _mediator;

        public DashboardController(IMediator mediator) => _mediator = mediator;

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var response = await _mediator.Send(new GetDashboardStatsQuery(), HttpContext.RequestAborted);
            return Ok(response);
        }
    }
}