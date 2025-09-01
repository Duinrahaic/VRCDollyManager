using Microsoft.EntityFrameworkCore;
using VRCDollyManager.Data;
using VRCDollyManager.Services;
using VRCDollyManager.Services.OSC;
using VRCDollyManager.Services.SteamVR;

namespace VRCDollyManager.Extensions;

public static class ServiceExtension
{
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        
        services.AddLogging(builder =>
        {
            builder.AddConsole();
        });


        Console.WriteLine(DollyManagerFilePaths.TryMigrateOldDatabase()
            ? "Database migration checked (migrated if old DB was found)."
            : "No database migration was required.");

        services.AddDbContextFactory<DollyDbContext>(options =>
            options.UseSqlite(@$"Data Source={DollyExtensions.GetDollyDbPath()}"));

        
        services.AddSingleton<IOscService, OscService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<ISteamVrService, SteamVrService>();
        services.AddSingleton<DollyFileWatcherService>();
        services.AddSingleton<IKeypressWatcherService, KeypressWatcherService>();
        
        return services;
    }
    public static IHost BuildApp(this HostApplicationBuilder builder)
    {
        var host = builder.Build();

        _ = host.Services.GetRequiredService<DollyFileWatcherService>();
        
        return host;
    }
 
}