using DataVault.Ef.Models;
using Microsoft.EntityFrameworkCore;

namespace DataVault.Ef;

public class NodeContext : DbContext
{
    public const string NODE_DB_FILE = "node.db";

    private readonly SemaphoreSlim mutex = new(1, 1);

    public DbSet<Identity> Identities { get; set; }
    public DbSet<Peer> Peers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={NODE_DB_FILE}");

    public async Task Run(Func<Task> work)
    {
        await mutex.WaitAsync();
        try
        {
            await work();
        } finally
        {
            mutex.Release();
        }
    }

    public async Task<T> Run<T>(Func<Task<T>> work)
    {
        await mutex.WaitAsync();
        try
        {
            return await work();
        } finally
        {
            mutex.Release();
        }
    }
}
