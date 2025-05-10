using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VRCDollyManager.Data;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

public sealed class DollyFileWatcherService : BackgroundService, IDollyFileWatcherService, IDisposable
{
    private readonly string _watchPath;
    private readonly FileSystemWatcher _fileWatcher;
    private readonly IDbContextFactory<DollyDbContext> _dbContextFactory;
    private bool _disposed = false;

    public event EventHandler<DollyChangedEventArgs>? DollyChanged;
    public DollyFileWatcherService(IDbContextFactory<DollyDbContext> dbContextFactory)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VRChat");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Console.WriteLine($"Directory created at: {path}");
        }
        else
        {
            Console.WriteLine($"Directory already exists at: {path}");
        }
        if (!Directory.Exists(Path.Combine(path, "CameraPaths")))
        {
            Directory.CreateDirectory(Path.Combine(path, "CameraPaths"));
            Console.WriteLine($"Directory created at: {Path.Combine(path, "CameraPaths")}");
        }
        else
        {
            Console.WriteLine($"Directory already exists at: {Path.Combine(path, "CameraPaths")}");
        }
        
        _dbContextFactory = dbContextFactory;
        using (var context = _dbContextFactory.CreateDbContext())
        {
            context.Database.EnsureCreated();
        }

       
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
    private string _ignore = string.Empty;
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.Name == _ignore)
        {
            _ignore = string.Empty;
            return;
        }

        if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Changed)
            _ = IndexFile(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            RemoveDollyAsync(Path.GetFileName(e.FullPath)).Wait();
        }
        catch (Exception ex)
        {
            // _ignore
        }
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
    
 

    
    public async Task ImportDollyFile(Dolly dolly)
    {
        
        if (dolly == null || string.IsNullOrWhiteSpace(dolly.Name))
        {
            throw new ArgumentException("Dolly data is invalid.");
        }

        string filePath = Path.Combine(_watchPath, $"{dolly.Name}");
 
        // If the file exists, create a unique name for the new file
        
        var exits = await _dbContextFactory.CreateDbContext().Dollies.AnyAsync(d => d.Name == dolly.Name);
        
        
        if (File.Exists(filePath) || exits)
        {
            string newFileName = GenerateUniqueFileName(dolly.Name);
            filePath = Path.Combine(_watchPath, newFileName);
            dolly.Name = Path.GetFileNameWithoutExtension(newFileName).Replace(".json",string.Empty) + ".json";  
        }

        _ignore = Path.Combine(_watchPath, $"{dolly.Name}");    
        await Task.Delay(1);
        try
        {
            var data = JsonSerializer.Serialize(dolly.KeyFrames, new JsonSerializerOptions()
            {
                WriteIndented = true,
       
            });


            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            if (!await dbContext.Dollies.AnyAsync(d => d.Name == dolly.Name))
            {
                dbContext.Dollies.Add(dolly);
                await dbContext.SaveChangesAsync();
            } 
            await File.WriteAllTextAsync(_ignore, data);

            OnDollyChanged(new DollyChangedEventArgs(dolly.Name, DollyChangeType.Added));
            _ignore = string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write file {filePath}: {ex.Message}");
        }
    }

    private string GenerateUniqueFileName(string baseName)
    {
        
        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"VRM_Import_{timeStamp}";
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
        try
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
        }catch (Exception ex)
        {
            Console.WriteLine($"Failed to sync file system with database: {ex.Message}");
            return;
        }

    }

    private void OnDollyChanged(DollyChangedEventArgs e)
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

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _fileWatcher?.Dispose();
            _disposed = true;
        }
    }
}