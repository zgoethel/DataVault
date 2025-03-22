using DataVault.Ef.Models;
using Microsoft.Extensions.Logging;

namespace DataVault.Ef.Repositories;

public class IdentityRepo(
    ILogger<IdentityRepo> log,
    NodeContext db)
{
    public async Task<Identity> GetOrCreateIdentity()
    {
        return await db.Run(async () =>
        {
            if (!db.Identities.Any())
            {
                log.LogInformation("Seeding node identity");

                db.Identities.Add(Identity.FromHost());

                await db.SaveChangesAsync();
            }

            return db.Identities.Single();
        });
    }
}
