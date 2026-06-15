using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MIP.Aws.Persistence;

/// <summary>Design-time factory for EF Core CLI (migrations add/update).</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MediaIntelligenceDbContext>
{
    public MediaIntelligenceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MediaIntelligenceDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=MIPAwsDesign;Trusted_Connection=True;TrustServerCertificate=True");
        return new MediaIntelligenceDbContext(optionsBuilder.Options);
    }
}
