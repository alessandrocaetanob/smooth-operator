using System;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Infrastructure.Data;
using SmoothOperator.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GuacamoleController : ControllerBase
    {
        private readonly GuacamoleProxyService _proxyService;
        private readonly AppDbContext _db;
        private readonly IAccessControlService _accessControl;

        public GuacamoleController(
            GuacamoleProxyService proxyService,
            AppDbContext db,
            IAccessControlService accessControl)
        {
            _proxyService = proxyService;
            _db = db;
            _accessControl = accessControl;
        }

        // Issues a short-lived single-use ticket bound to (userId, connectionId, IP).
        // The browser then opens the WebSocket with ?ticket=<id> — Authorization
        // headers can't be sent on a WS upgrade, so this is the secure handshake.
        [HttpPost("ticket/{connectionId}")]
        [Authorize]
        public async Task<IActionResult> IssueTicket(Guid connectionId)
        {
            var profile = await _accessControl.GetCurrentProfileAsync(HttpContext.User);
            if (profile == null)
            {
                return Unauthorized();
            }

            var connection = await _db.Connections
                .Include(c => c.Users)
                .FirstOrDefaultAsync(c => c.Id == connectionId);
            if (connection == null)
            {
                return NotFound();
            }

            if (!_accessControl.CanUseConnection(profile, connection))
            {
                return Forbid();
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var ticket = await _proxyService.IssueTicketAsync(profile.UserId, connectionId, ip);
            return Ok(new { ticket });
        }

        // WebSocket upgrade endpoint. AllowAnonymous because browsers can't
        // attach Authorization headers to WS upgrades; auth is provided by the
        // single-use ticket query parameter and validated against Redis.
        [HttpGet("connect/{connectionId}")]
        [AllowAnonymous]
        public async Task Get(Guid connectionId, [FromQuery] string? ticket)
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (string.IsNullOrWhiteSpace(ticket))
            {
                HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            var consumed = await _proxyService.ConsumeTicketAsync(ticket, connectionId, ip);
            if (consumed == null)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync("guacamole");
            await _proxyService.HandleWebSocketAsync(webSocket, connectionId, consumed.Value, ip);
        }
    }
}
