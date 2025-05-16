using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

public interface IDollyFileWatcherService
{
    event EventHandler<DollyChangedEventArgs>? DollyChanged;
    string GetVersion();
    Task<List<Models.Dolly>> GetAllDolliesAsync();
    Task<Dolly?> GetDollyByNameAsync(string name);
    Task AddDollyAsync(Dolly dolly);
    Task ImportDollyFile(Dolly dolly);
    Task UpdateDollyAsync(Dolly dolly);
    Task RemoveDollyAsync(string fileName);
    Task SyncFileSystemWithDatabaseAsync();
}