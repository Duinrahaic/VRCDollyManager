using Microsoft.EntityFrameworkCore;
using VRCDollyManager.Data;
using VRCDollyManager.Services;

namespace VRCDollyManager.Extensions;

public static class ServiceExtension
{
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<OscService>();
        services.AddSingleton<IDollyFileWatcherService, DollyFileWatcherService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<ISteamVrService, SteamVrService>();
         services.AddDbContextFactory<DollyDbContext>(options =>
            options.UseSqlite(@$"Data Source={DollyExtensions.GetDollyDbPath()}"));
        return services;
    }
}