using DataVault.Ef.Models;
using Microsoft.Extensions.Logging;

namespace DataVault.Ef.Repositories;

public class PeerRepo(
    ILogger<PeerRepo> log,
    NodeContext db)
{
    public async Task UpdatePeerStatus(AnnounceIdentityDto message)
    {
        await db.Run(async () =>
        {
            var peer = db.Peers.FirstOrDefault((it) => it.Id == message.Identity.Id);
            if (peer is null)
            {
                log.LogInformation("Unrecognized peer '{}' has announced itself", message.Identity.HostName);

                peer = new()
                {
                    Id = message.Identity.Id,
                    Created = DateTime.UtcNow
                };
                db.Peers.Add(peer);
            } else
            {
                log.LogDebug("Existing peer '{}' has announced itself", message.Identity.HostName);
            }

            peer.HostName = message.Identity.HostName;
            peer.Address = message.Address;
            peer.Port = message.Port;
            peer.Status = message.Status;
            peer.StatusReported = DateTime.UtcNow;

            await db.SaveChangesAsync();
        });
    }
}
