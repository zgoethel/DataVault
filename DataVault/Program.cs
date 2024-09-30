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
using RabbitMQ.Client;
using System.Reflection;

namespace DataVault;

internal static class Program
{
    public const string SETTINGS_FILE = "appsettings.json";
    public const string DEFAULT_DATA_DIR = "./data/";

    static async Task<int> Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: ./DataVault.exe [data_folder]");
            return 1;
        }
        var dataFolder = args.FirstOrDefault(DEFAULT_DATA_DIR);

        try
        {
            Directory.CreateDirectory(dataFolder);
            Directory.SetCurrentDirectory(dataFolder);

            var settingsInfo = new FileInfo(SETTINGS_FILE);
            if (!settingsInfo.Exists || settingsInfo.Length == 0)
            {
                var assembly = Assembly.GetExecutingAssembly();

                using var template = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{SETTINGS_FILE}")!;
                using var settingsOut = File.OpenWrite(SETTINGS_FILE);

                await template.CopyToAsync(settingsOut);
            }

            var builder = Host.CreateApplicationBuilder(args);

            builder.Configuration.AddJsonFile(SETTINGS_FILE, false, false);

            builder.Services.AddLogging((config) =>
            {
#if DEBUG
                config.SetMinimumLevel(LogLevel.Debug);
#else
                config.SetMinimumLevel(LogLevel.Information);
#endif
            });

            builder.Services.AddSingleton((sp) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var settings = new AppSettings();

                config.GetSection("RabbitMQ").Bind(settings.RabbitMQ);

                return settings;
            });

            builder.Services.AddSingleton((sp) =>
            {
                var settings = sp.GetRequiredService<AppSettings>();

                var log = sp.GetRequiredService<ILogger<ConnectionFactory>>();
                log.LogInformation("Connecting to broker at '{}:{}'",
                    settings.RabbitMQ.HostName,
                    settings.RabbitMQ.Port);

                var factory = new ConnectionFactory()
                {
                    HostName = settings.RabbitMQ.HostName,
                    Port = settings.RabbitMQ.Port,
                    UserName = settings.RabbitMQ.UserName,
                    Password = settings.RabbitMQ.Password
                };

                return factory.CreateConnection();
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

            builder.Services.AddDbContext<NodeContext>();

            builder.Services.AddSingleton<IdentityRepo>();

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
            nodeIdentity.Initialize();

            var grammar = app.Services.GetRequiredService<Grammar>();

            using var cancel = new CancellationTokenSource();
            var longRunning = new List<Task>();

            var discovery = app.Services.GetRequiredService<Discovery>();
            longRunning.Add(discovery.BeginListen(cancel.Token));
            longRunning.Add(discovery.BeginAnnounce(cancel.Token));

            await app.WaitForShutdownAsync();

            cancel.Cancel();
            await Task.WhenAll(longRunning);
        } catch (Exception ex)
        {
            Console.Error.WriteLine("Encountered fatal unhandled exception");
            Console.Error.WriteLine(ex.ToString());

            return 1;
        }

        return 0;
    }
}
