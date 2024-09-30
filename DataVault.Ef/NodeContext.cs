using DataVault.Ef.Models;
using Microsoft.EntityFrameworkCore;

namespace DataVault.Ef;

public class NodeContext : DbContext
{
    const string NODE_DB_FILE = "node.db";

    public DbSet<Identity> Identities { get; set; }
    public DbSet<Peer> Peers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={NODE_DB_FILE}");
}
