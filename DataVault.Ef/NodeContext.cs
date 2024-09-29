using DataVault.Ef.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataVault.Ef;

public class NodeContext(ILogger<NodeContext> log) : DbContext
{
    public DbSet<Identity> Identities { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source=node.db");

    public void Initialize()
    {
        log.LogInformation("Applying local database migrations");

        Database.Migrate();

        if (!Identities.Any())
        {
            log.LogInformation("Seeding node identity");
            
            Identities.Add(Identity.FromHost());
            SaveChanges();
        }
        var identity = Identities.Single();

        log.LogDebug("Hello, world! from '{}'", identity.Id);
    }
}
