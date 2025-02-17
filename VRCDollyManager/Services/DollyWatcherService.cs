using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VRCDollyManager.Data;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

public class DollyFileWatcherService : BackgroundService, IDollyFileWatcherService, IDisposable
{
    private readonly string _watchPath;
    private readonly FileSystemWatcher _fileWatcher;
    private readonly IDbContextFactory<DollyDbContext> _dbContextFactory;
    private bool _disposed = false;

    public event EventHandler<DollyChangedEventArgs>? DollyChanged;

    public DollyFileWatcherService(IDbContextFactory<DollyDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        _watchPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VRChat",
            "CameraPaths");

        if (!Directory.Exists(_watchPath))
            Directory.CreateDirectory(_watchPath);

        _fileWatcher = new FileSystemWatcher(_watchPath, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Deleted += OnFileDeleted;

        LoadExistingFiles();
    }

    private void LoadExistingFiles()
    {
        foreach (var file in Directory.GetFiles(_watchPath, "*.json")) IndexFile(file);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
            IndexFile(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        RemoveDollyAsync(Path.GetFileName(e.FullPath)).Wait();
    }

    private async Task IndexFile(string filePath)
    {
        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var fileName = Path.GetFileName(filePath);
            var exists = await dbContext.Dollies.AnyAsync(d => d.Name == fileName);

            if (await TryAddDollyAsync(new Dolly { Name = fileName }))
                OnDollyChanged(new DollyChangedEventArgs(fileName,
                    exists ? DollyChangeType.Updated : DollyChangeType.Added));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process {filePath}: {ex.Message}");
        }
    }

    public async Task<bool> TryAddDollyAsync(Dolly dolly)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        try
        {
            if (!await dbContext.Dollies.AnyAsync(d => d.Name == dolly.Name)) // Check inside the same context
            {
                dbContext.Dollies.Add(dolly);
                await dbContext.SaveChangesAsync();
                OnDollyChanged(new DollyChangedEventArgs(dolly.Name, DollyChangeType.Added));
                return true;
            }
        }
        catch (DbUpdateException ex) when
            ((ex.InnerException as SqliteException)?.SqliteErrorCode == 19) // Handle UNIQUE constraint
        {
            Console.WriteLine($"Skipping duplicate entry: {dolly.Name}");
        }

        return false;
    }

    public async Task<List<Dolly>> GetAllDolliesAsync()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        return await dbContext.Dollies.ToListAsync();
    }

    public async Task<Dolly?> GetDollyByNameAsync(string name)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        return await dbContext.Dollies.FirstOrDefaultAsync(d => d.Name == name);
    }

    public async Task AddDollyAsync(Dolly dolly)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        if (!await dbContext.Dollies.AnyAsync(d => d.Name == dolly.Name))
        {
            dbContext.Dollies.Add(dolly);
            await dbContext.SaveChangesAsync();
            OnDollyChanged(new DollyChangedEventArgs(dolly.Name, DollyChangeType.Added));
        }
    }

    public async Task UpdateDollyAsync(Dolly dolly)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var existingDolly = await dbContext.Dollies.FirstOrDefaultAsync(d => d.Name == dolly.Name);
        if (existingDolly != null)
        {
            existingDolly.Alias = dolly.Alias;
            existingDolly.Tags = dolly.Tags;
            await dbContext.SaveChangesAsync();
            OnDollyChanged(new DollyChangedEventArgs(dolly.Name, DollyChangeType.Updated));
        }
    }

    public async Task RemoveDollyAsync(string fileName)
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var dolly = await dbContext.Dollies.FirstOrDefaultAsync(d => d.Name == fileName);
        if (dolly != null)
        {
            dbContext.Dollies.Remove(dolly);
            await dbContext.SaveChangesAsync();

            RemoveDollyFile(fileName);

            OnDollyChanged(new DollyChangedEventArgs(fileName, DollyChangeType.Removed));
        }
    }

    public void RemoveDollyFile(string fileName)
    {
        var filePath = Path.Combine(_watchPath, fileName);
        if (File.Exists(filePath))
            try
            {
                File.Delete(filePath);
                Console.WriteLine($"Deleted file: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete file {filePath}: {ex.Message}");
            }
    }

    public async Task SyncFileSystemWithDatabaseAsync()
    {
        using var dbContext = _dbContextFactory.CreateDbContext();
        var existingFiles = Directory.GetFiles(_watchPath, "*.json").Select(Path.GetFileName).ToHashSet();
        var existingDollies = await dbContext.Dollies.ToDictionaryAsync(d => d.Name);

        foreach (var file in existingFiles)
            if (!existingDollies.ContainsKey(file))
            {
                Console.WriteLine($"Adding missing file to database: {file}");
                await AddDollyAsync(new Dolly { Name = file });
            }

        foreach (var dolly in existingDollies.Values)
            if (!existingFiles.Contains(dolly.Name))
            {
                Console.WriteLine($"Removing orphaned database entry: {dolly.Name}");
                await RemoveDollyAsync(dolly.Name);
            }
    }

    protected virtual void OnDollyChanged(DollyChangedEventArgs e)
    {
        DollyChanged?.Invoke(this, e);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("DollyFileWatcherService started...");
        await SyncFileSystemWithDatabaseAsync();
        await base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("DollyFileWatcherService stopping...");
        _fileWatcher.EnableRaisingEvents = false;
        return base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _fileWatcher?.Dispose();
            _disposed = true;
        }
    }
}