using DataVault.Core;
using DataVault.Core.Algorithm;
using DataVault.Core.Node;
using DataVault.Core.Queues;
using DataVault.Core.Syntax;
using DataVault.Ef;
using DataVault.Ef.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Reflection;

namespace DataVault;

internal static class Program
{
    public const string SETTINGS_FILE = "appsettings.json";
    public const string DEFAULT_DATA_DIR = "./data/";

    static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length > 1)
            {
                var results = await Task.WhenAll(args.Select((it) => Main([it])));
                return results.Any((it) => it != 0) ? 1 : 0;
            }

            var dataFolder = args.FirstOrDefault(DEFAULT_DATA_DIR);
            Directory.CreateDirectory(dataFolder);

            var settingsPath = Path.Combine(dataFolder, SETTINGS_FILE);
            var settingsInfo = new FileInfo(settingsPath);
            if (!settingsInfo.Exists || settingsInfo.Length == 0)
            {
                var assembly = Assembly.GetExecutingAssembly();

                using var template = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{SETTINGS_FILE}")!;
                using var settingsOut = File.OpenWrite(settingsPath);

                await template.CopyToAsync(settingsOut);
            }

            var builder = Host.CreateApplicationBuilder(args);

            builder.Configuration.AddJsonFile(settingsPath, false, true);

            builder.Services.AddLogging((config) =>
            {
#if DEBUG
                config.SetMinimumLevel(LogLevel.Debug);
#else
                config.SetMinimumLevel(LogLevel.Information);
#endif
            });

            builder.Services.Configure<AppSettings>(builder.Configuration);

            builder.Services.AddSingleton((sp) =>
            {
                var settings = sp.GetRequiredService<IOptions<AppSettings>>();

                var log = sp.GetRequiredService<ILogger<ConnectionFactory>>();
                log.LogInformation("Connecting to broker at '{}:{}'",
                    settings.Value.RabbitMQ.HostName,
                    settings.Value.RabbitMQ.Port);

                var factory = new ConnectionFactory()
                {
                    HostName = settings.Value.RabbitMQ.HostName,
                    Port = settings.Value.RabbitMQ.Port,
                    UserName = settings.Value.RabbitMQ.UserName,
                    Password = settings.Value.RabbitMQ.Password
                };

                return Task.Run(async () => await factory.CreateConnectionAsync()).Result;
            });

            builder.Services.AddSingleton((sp) =>
            {
                var log = sp.GetRequiredService<ILogger<Fsa>>();
                log.LogInformation("Compiling control syntax tokens");

                var compileStart = DateTime.Now;
                var grammar = Grammar.CreateDfa();
                var compileTime = DateTime.Now - compileStart;

                log.LogDebug("Compiled in {:#,##0.00}ms", compileTime.TotalMilliseconds);

                return grammar;
            });

            builder.Services.AddKeyedSingleton("BasePath", dataFolder);

            builder.Services.AddDbContext<NodeContext>();

            builder.Services.AddSingleton<IdentityRepo>();
            builder.Services.AddSingleton<PeerRepo>();

            builder.Services.AddSingleton<Discovery>();
            builder.Services.AddSingleton<NodeIdentity>();
            builder.Services.AddSingleton<PoolTable>();
            builder.Services.AddSingleton<StripePlacement>();
            builder.Services.AddSingleton<Locking>();
            builder.Services.AddSingleton<ReadQueue>();
            builder.Services.AddSingleton<WriteQueue>();
            builder.Services.AddSingleton<Grammar>();

            using var app = builder.Build();
            await app.StartAsync();

            var nodeIdentity = app.Services.GetRequiredService<NodeIdentity>();
            await nodeIdentity.Initialize();

            using var cancel = new CancellationTokenSource();
            var longRunning = new List<Task>();

            var discovery = app.Services.GetRequiredService<Discovery>();
            longRunning.Add(discovery.BeginListen(cancel.Token));
            longRunning.Add(discovery.BeginAnnounce(cancel.Token));

            await app.WaitForShutdownAsync();

            cancel.Cancel();
            await Task.WhenAll(longRunning);

            return 0;
        } catch (Exception ex)
        {
            Console.Error.WriteLine("Encountered fatal unhandled exception");
            Console.Error.WriteLine(ex.ToString());

            return 1;
        }
    }
}
