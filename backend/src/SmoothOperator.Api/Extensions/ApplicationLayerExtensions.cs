using FluentValidation;
using MapsterMapper;
using SmoothOperator.Application.Behaviors;
using SmoothOperator.Application.Features.Auth.Commands;
using SmoothOperator.Application.Interfaces;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Api.Extensions;

public static class ApplicationLayerExtensions
{
    // MediatR, FluentValidation, Mapster, and the IAppDbContext alias.
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(LoginCommandHandler).Assembly);
            cfg.AddBehavior(typeof(MediatR.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(MediatR.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(MediatR.IPipelineBehavior<,>), typeof(MetricsBehavior<,>));
        });
        services.AddValidatorsFromAssembly(typeof(LoginCommandHandler).Assembly);
        services.AddScoped<IMapper, Mapper>();
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }
}
