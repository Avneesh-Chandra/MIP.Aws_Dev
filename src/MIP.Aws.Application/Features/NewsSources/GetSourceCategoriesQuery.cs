using MIP.Aws.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

/// <summary>Lookup query for the source category combo on the admin UI.</summary>
public sealed record GetSourceCategoriesQuery() : IRequest<IReadOnlyList<SourceCategoryDto>>;

public sealed record SourceCategoryDto(Guid Id, string Name, string? Description);

public sealed class GetSourceCategoriesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSourceCategoriesQuery, IReadOnlyList<SourceCategoryDto>>
{
    public async Task<IReadOnlyList<SourceCategoryDto>> Handle(GetSourceCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await db.SourceCategories.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new SourceCategoryDto(c.Id, c.Name, c.Description))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
