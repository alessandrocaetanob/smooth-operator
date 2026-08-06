using System.Net;
using System.Text.Json;
using FluentValidation;
using SmoothOperator.Application.Exceptions;

namespace SmoothOperator.Api.Middleware
{
    public sealed class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, message) = exception switch
            {
                NotFoundException e => (HttpStatusCode.NotFound, e.Message),
                BadRequestException e => (HttpStatusCode.BadRequest, e.Message),
                ConflictException e => (HttpStatusCode.Conflict, e.Message),
                ForbiddenException e => (HttpStatusCode.Forbidden, e.Message),
                UnauthorizedException e => (HttpStatusCode.Unauthorized, e.Message),
                ValidationException e => (HttpStatusCode.BadRequest,
                    string.Join("; ", e.Errors.Select(err => err.ErrorMessage))),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
            };

            if (statusCode == HttpStatusCode.InternalServerError)
                _logger.LogError(exception, "Unhandled exception");

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var body = JsonSerializer.Serialize(new { message });
            await context.Response.WriteAsync(body, context.RequestAborted);
        }
    }
}
