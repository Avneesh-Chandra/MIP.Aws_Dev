using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

public class Tenant : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
