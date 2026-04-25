using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Services;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GuacamoleController : ControllerBase
    {
        private readonly GuacamoleProxyService _proxyService;

        public GuacamoleController(GuacamoleProxyService proxyService)
        {
            _proxyService = proxyService;
        }

        [HttpGet("connect/{connectionId}")]
        public async Task Get(Guid connectionId)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync("guacamole");
                await _proxyService.HandleWebSocketAsync(webSocket, connectionId, HttpContext.User);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }
    }
}
