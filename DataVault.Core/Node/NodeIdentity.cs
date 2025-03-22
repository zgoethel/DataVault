using DataVault.Ef;
using DataVault.Ef.Models;
using DataVault.Ef.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataVault.Core.Node;

public class NodeIdentity(
    ILogger<NodeIdentity> log,
    NodeContext db,
    IdentityRepo repo)
{
    public Identity Identity { get; private set; } = new();

    public NodeStatus Status { get; private set; } = NodeStatus.Offline;

    public async Task Initialize()
    {
        log.LogInformation("Applying local database migrations");
        db.Database.Migrate();

        Identity = await repo.GetOrCreateIdentity();
        log.LogDebug("Hello, world! from '{}'", Identity.Id);
    }
}
