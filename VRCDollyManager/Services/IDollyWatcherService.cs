using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

public interface IDollyFileWatcherService
{
    event EventHandler<DollyChangedEventArgs>? DollyChanged;

    Task<List<Dolly>> GetAllDolliesAsync();
    Task<Dolly?> GetDollyByNameAsync(string name);
    Task AddDollyAsync(Dolly dolly);
    Task UpdateDollyAsync(Dolly dolly);
    Task RemoveDollyAsync(string fileName);
    Task SyncFileSystemWithDatabaseAsync();
}