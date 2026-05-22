using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Interfaces;

namespace SmoothOperator.Application.Features.ApiTokens.Queries
{
    public sealed record ListApiTokensQuery(Guid UserId) : IRequest<IReadOnlyList<ApiTokenSummaryDto>>;

    public sealed class ListApiTokensQueryHandler : IRequestHandler<ListApiTokensQuery, IReadOnlyList<ApiTokenSummaryDto>>
    {
        private readonly IAppDbContext _context;

        public ListApiTokensQueryHandler(IAppDbContext context) => _context = context;

        public async Task<IReadOnlyList<ApiTokenSummaryDto>> Handle(ListApiTokensQuery request, CancellationToken cancellationToken)
        {
            return await _context.ApiTokens
                .AsNoTracking()
                .Where(t => t.UserId == request.UserId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new ApiTokenSummaryDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    CreatedAt = t.CreatedAt,
                    ExpiresAt = t.ExpiresAt,
                    LastUsedAt = t.LastUsedAt,
                    RevokedAt = t.RevokedAt,
                })
                .ToListAsync(cancellationToken);
        }
    }
}
