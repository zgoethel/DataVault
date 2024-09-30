using DataVault.Ef.Models;
using Microsoft.Extensions.Logging;

namespace DataVault.Ef.Repositories;

public class IdentityRepo(
    ILogger<IdentityRepo> log,
    NodeContext db)
{
    public Identity GetOrCreateIdentity()
    {
        if (!db.Identities.Any())
        {
            log.LogInformation("Seeding node identity");

            db.Identities.Add(Identity.FromHost());
            db.SaveChanges();
        }
        var identity = db.Identities.Single();

        return identity;
    }
}
