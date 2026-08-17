using MIP.Aws.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Infrastructure.Portal;

internal sealed class EfCommandTimeoutScope : IDisposable
{
    private readonly DbContext _db;
    private readonly int? _previous;

    private EfCommandTimeoutScope(DbContext db, int? previous)
    {
        _db = db;
        _previous = previous;
    }

    public static IDisposable? TryBegin(IApplicationDbContext db, int seconds)
    {
        if (db is not DbContext ef)
        {
            return null;
        }

        var previous = ef.Database.GetCommandTimeout();
        ef.Database.SetCommandTimeout(seconds);
        return new EfCommandTimeoutScope(ef, previous);
    }

    public void Dispose() => _db.Database.SetCommandTimeout(_previous);
}
