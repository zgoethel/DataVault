using DataVault.Ef.Models;
using Microsoft.EntityFrameworkCore;

namespace DataVault.Ef;

public class NodeContext : DbContext
{
    public DbSet<Identity> Identity { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source=node.db");
}
