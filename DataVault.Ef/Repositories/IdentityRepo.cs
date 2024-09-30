using DataVault.Ef.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataVault.Ef.Repositories;

public class IdentityRepo(
    ILogger<IdentityRepo> log,
    NodeContext db)
{
    public void Initialize()
    {
        log.LogInformation("Applying local database migrations");

        db.Database.Migrate();

        if (!db.Identities.Any())
        {
            log.LogInformation("Seeding node identity");

            db.Identities.Add(Identity.FromHost());
            db.SaveChanges();
        }
        var identity = db.Identities.Single();

        log.LogDebug("Hello, world! from '{}'", identity.Id);
    }
}
