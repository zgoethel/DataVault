using DataVault.Core;
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
    const string SETTINGS_FILE = "appsettings.json";
    const string DEFAULT_DATA_DIR = "./data/";

    static int Main(string[] args)
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

                template.CopyTo(settingsOut);
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
                var factory = new ConnectionFactory()
                {
                    HostName = settings.RabbitMQ.HostName,
                    Port = settings.RabbitMQ.Port,
                    UserName = settings.RabbitMQ.UserName,
                    Password = settings.RabbitMQ.Password
                };

                return factory.CreateConnection();
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

            app.Start();

            var identityRepo = app.Services.GetRequiredService<IdentityRepo>();
            identityRepo.Initialize();

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
