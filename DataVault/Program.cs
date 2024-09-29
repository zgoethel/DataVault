using DataVault.Core.Node;
using DataVault.Core.Queues;
using DataVault.Core.Syntax;
using DataVault.Ef;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataVault;

internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: ./DataVault.exe [data_folder]");
            return 1;
        }
        var dataFolder = args.FirstOrDefault("./data/");

        try
        {
            Directory.CreateDirectory(dataFolder);
            Directory.SetCurrentDirectory(dataFolder);

            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddLogging((config) =>
            {
#if DEBUG
                config.SetMinimumLevel(LogLevel.Debug);
#else
                config.SetMinimumLevel(LogLevel.Information);
#endif
            });

            builder.Services.AddDbContext<NodeContext>();

            builder.Services.AddSingleton<Discovery>();
            builder.Services.AddSingleton<NodeIdentity>();
            builder.Services.AddSingleton<PoolTable>();
            builder.Services.AddSingleton<StripePlacement>();
            builder.Services.AddSingleton<Locking>();
            builder.Services.AddSingleton<ReadQueue>();
            builder.Services.AddSingleton<WriteQueue>();
            builder.Services.AddSingleton<Grammar>();

            using var app = builder.Build();

            app.Start();

            var db = app.Services.GetRequiredService<NodeContext>();
            db.Initialize();

            app.WaitForShutdown();
        } catch (Exception ex)
        {
            Console.Error.WriteLine("Encountered fatal unhandled exception");
            Console.Error.WriteLine(ex.ToString());

            return 1;
        }

        return 0;
    }
}
