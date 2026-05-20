using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SmoothOperator.Api.Authentication;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Features.ApiTokens.Commands;
using SmoothOperator.Application.Features.ApiTokens.Queries;

namespace SmoothOperator.Api.Controllers;

[ApiController]
[Route("api/api-tokens")]
[Authorize]
public class ApiTokensController : ControllerBase
{
    private const string CacheTag = "api-tokens";

    private readonly IMediator _mediator;
    private readonly IOutputCacheStore _cacheStore;

    public ApiTokensController(IMediator mediator, IOutputCacheStore cacheStore)
    {
        _mediator = mediator;
        _cacheStore = cacheStore;
    }

    [HttpGet]
    [OutputCache(PolicyName = "ShortCache", Tags = [CacheTag])]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var tokens = await _mediator.Send(new ListApiTokensQuery(userId), cancellationToken);
        return Ok(tokens);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateApiTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (CallerIsPat()) return Forbid();
        try
        {
            var result = await _mediator.Send(
                new CreateApiTokenCommand(userId, request.Name, request.ExpiresAt),
                cancellationToken);
            await _cacheStore.EvictByTagAsync(CacheTag, cancellationToken);
            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (CallerIsPat()) return Forbid();
        try
        {
            await _mediator.Send(new RevokeApiTokenCommand(userId, id), cancellationToken);
            await _cacheStore.EvictByTagAsync(CacheTag, cancellationToken);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }

    /// <summary>
    /// True when the current request authenticated via a PAT (rather than JWT/cookie).
    /// PATs are not allowed to manage other PATs.
    /// </summary>
    private bool CallerIsPat()
    {
        return User.HasClaim("amr", ApiTokenAuthHandler.SchemeName);
    }
}
