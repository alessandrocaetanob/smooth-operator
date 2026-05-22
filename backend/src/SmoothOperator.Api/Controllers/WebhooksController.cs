using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;
using SmoothOperator.Application.Features.Webhooks.Commands;
using SmoothOperator.Application.Features.Webhooks.Queries;
using SmoothOperator.Infrastructure.Services;

namespace SmoothOperator.Api.Controllers;

/// <summary>
/// Admin management of outbound webhook endpoints. Not output-cached: the list
/// carries live delivery-status fields that the background worker updates.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[Authorize(Roles = AppRoles.OwnerOrAdmin)]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;

    public WebhooksController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListWebhooksQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var result = await _mediator.Send(
                new CreateWebhookCommand(request.Name, request.Url, request.EventTypes),
                cancellationToken);
            return Ok(result);
        }
        catch (BadRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _mediator.Send(
                new UpdateWebhookCommand(id, request.Name, request.Url, request.EventTypes, request.Enabled),
                cancellationToken);
            return NoContent();
        }
        catch (BadRequestException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new DeleteWebhookCommand(id), cancellationToken);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/rotate-secret")]
    public async Task<IActionResult> RotateSecret(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mediator.Send(new RotateWebhookSecretCommand(id), cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> SendTest(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new SendTestWebhookCommand(id), cancellationToken);
            return Accepted();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
