using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;

namespace SmoothOperator.Application.Features.Users.Queries
{
    public sealed record GetRolesQuery(ClaimsPrincipal User) : IRequest<IEnumerable<RoleCatalogItemDto>>;

    public sealed class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, IEnumerable<RoleCatalogItemDto>>
    {
        private static readonly IReadOnlyList<RoleCatalogItemDto> Catalog = new[]
        {
            new RoleCatalogItemDto { Name = "Owner",     Description = "Root access and bootstrap account owner." },
            new RoleCatalogItemDto { Name = "Admin",     Description = "Can create groups, invite users, vaults and credentials." },
            new RoleCatalogItemDto { Name = "TeamAdmin", Description = "Can create/manage connections in assigned vaults." },
            new RoleCatalogItemDto { Name = "User",      Description = "Can only use connections in assigned vaults." }
        };

        public Task<IEnumerable<RoleCatalogItemDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<RoleCatalogItemDto>>(Catalog);
    }
}
