using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudentPortal.Api.Data;

// Used only by `dotnet ef migrations add`, which does not open a connection.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseNpgsql("Host=localhost;Database=student_portal;Username=portal_app")
            .Options;
        return new PortalDbContext(options);
    }
}
