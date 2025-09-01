using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VRCDollyManager.Data;
using VRCDollyManager.Extensions;
using VRCDollyManager.Models;

namespace VRCDollyManager.Services;

/// <summary>
/// Service that watches the VRChat DollyManager CameraPaths folder,
/// synchronizes dolly JSON files with the database, and raises events when
/// dollies are added, updated, or removed.
/// </summary>
public class DollyFileWatcherService : IDisposable
{
    private readonly IDbContextFactory<DollyDbContext> _dbContextFactory;
    private readonly ILogger<DollyFileWatcherService> _logger;
    private FileSystemWatcher? _fileWatcher;
    private bool _disposed = false;
    private string _ignore = string.Empty;

    public event EventHandler<DollyChangedEventArgs>? DollyChanged;

    public DollyFileWatcherService(
        ILogger<DollyFileWatcherService> logger,
        IDbContextFactory<DollyDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        
        Task.Run(async () =>
        {
            await Setup();
        });
    }

 
    
    private async Task Setup()
    {
        _logger.LogInformation("Starting DollyFileWatcherService...");

        // Ensure DollyManager folder exists
        if (DollyManagerFilePaths.TryCreateFolder(DollyManagerFilePaths.GetDollyManagerFolder(), out _))
            _logger.LogInformation("Ensured DollyManager folder exists at {Path}", DollyManagerFilePaths.GetDollyManagerFolder());
        else
            _logger.LogWarning("Failed to ensure DollyManager folder exists at {Path}", DollyManagerFilePaths.GetDollyManagerFolder());

        // Ensure CameraPaths folder exists
        if (DollyManagerFilePaths.TryCreateFolder(DollyManagerFilePaths.GetCameraPathsFolder(), out _))
            _logger.LogInformation("Ensured CameraPaths folder exists at {Path}", DollyManagerFilePaths.GetCameraPathsFolder());
        else
            _logger.LogWarning("Failed to ensure CameraPaths folder exists at {Path}", DollyManagerFilePaths.GetCameraPathsFolder());


        

        // Ensure database is created
        await using (var context = await _dbContextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }
        _logger.LogInformation("Database ensured/created at {Path}", DollyManagerFilePaths.GetDatabaseFilePath());

        
        
        
        // Setup file watcher
        _fileWatcher = new FileSystemWatcher(DollyManagerFilePaths.GetCameraPathsFolder(), "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _fileWatcher.Created += OnFileChanged;
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Deleted += OnFileDeleted;

        _logger.LogInformation("Started watching CameraPaths folder: {Path}", DollyManagerFilePaths.GetCameraPathsFolder());

        
        
        
        // Initial sync
        _ = SyncFileSystemWithDatabaseAsync();
        LoadExistingFiles();
    }

 
    private void LoadExistingFiles()
    {
        foreach (var file in Directory.GetFiles(DollyManagerFilePaths.GetCameraPathsFolder(), "*.json")) IndexFile(file).ConfigureAwait(true);
    }
    
    
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
        catch 
        {
            // _ignore
        }
    }

    private async Task IndexFile(string filePath)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var fileName = Path.GetFileName(filePath);
            var exists = await dbContext.Dollies.AnyAsync(d => d.Name == fileName);

            if (await TryAddDollyAsync(new Models.Dolly { Name = fileName }))
                OnDollyChanged(new DollyChangedEventArgs(fileName,
                    exists ? DollyChangeType.Updated : DollyChangeType.Added));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to process {filePath}: {ex.Message}");
        }
    }

    public async Task<bool> TryAddDollyAsync(Models.Dolly dolly)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
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
            _logger.LogInformation($"Skipping duplicate entry: {dolly.Name}");
        }

        return false;
    }

    public async Task<List<Dolly>> GetAllDolliesAsync()
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Dollies.ToListAsync();
    }

    public async Task<Dolly?> GetDollyByNameAsync(string name)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        return await dbContext.Dollies.FirstOrDefaultAsync(d => d.Name == name);
    }

    public async Task AddDollyAsync(Dolly dolly)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
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
            throw new ArgumentException("Dolly data is invalid.");

        string folderPath = DollyManagerFilePaths.GetCameraPathsFolder();
        string fileName = Path.GetFileNameWithoutExtension(dolly.Name) + ".json"; // ensure .json
        string filePath = Path.Combine(folderPath, fileName);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        if (File.Exists(filePath) || await dbContext.Dollies.AnyAsync(d => d.Name == fileName))
        {
            string newFileName = GenerateUniqueFileName(fileName);
            fileName = newFileName;
            filePath = Path.Combine(folderPath, fileName);
            dolly.Name = fileName;
        }
        else
        {
            dolly.Name = fileName; // normalize to "xxx.json"
        }

        _ignore = fileName;
        await Task.Delay(1); // tiny delay to ensure _ignore is set

        try
        {
            // Serialize keyframes
            var data = JsonSerializer.Serialize(dolly.KeyFrames, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            if (!await dbContext.Dollies.AnyAsync(d => d.Name == dolly.Name))
            {
                dbContext.Dollies.Add(dolly);
                await dbContext.SaveChangesAsync();
            }

            // Write JSON file
            await File.WriteAllTextAsync(filePath, data);

            OnDollyChanged(new DollyChangedEventArgs(dolly.Name, DollyChangeType.Added));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to write file {filePath}: {ex.Message}");
        }
        finally
        {
            _ignore = string.Empty; // always reset ignore flag
        }
    }

    private string GenerateUniqueFileName(string baseName)
    {
        
        var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"VRM_Import_{timeStamp}";
    }

    public async Task UpdateDollyAsync(Models.Dolly dolly)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
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
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
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
        var filePath = Path.Combine(DollyManagerFilePaths.GetCameraPathsFolder(), fileName);
        if (File.Exists(filePath))
            try
            {
                File.Delete(filePath);
                _logger.LogInformation($"Deleted file: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete file {filePath}: {ex.Message}");
            }
    }

    public async Task SyncFileSystemWithDatabaseAsync()
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            string watchPath = DollyManagerFilePaths.GetCameraPathsFolder();

            var existingFiles = Directory.GetFiles(watchPath,"*.json").Select(Path.GetFileName).ToHashSet();
            var existingDollies = await dbContext.Dollies.ToDictionaryAsync(d => d.Name);

            foreach (var file in existingFiles)
                if (!existingDollies.ContainsKey(file))
                {
                    _logger.LogInformation($"Adding missing file to database: {file}");
                    await AddDollyAsync(new Models.Dolly { Name = file });
                }

            foreach (var dolly in existingDollies.Values)
                if (!existingFiles.Contains(dolly.Name))
                {
                    _logger.LogInformation($"Removing orphaned database entry: {dolly.Name}");
                    await RemoveDollyAsync(dolly.Name);
                }
        }catch (Exception ex)
        {
            _logger.LogCritical($"Failed to sync file system with database: {ex.Message}");
        }

    }

  


    private void OnDollyChanged(DollyChangedEventArgs e)
    {
        DollyChanged?.Invoke(this, e);
    }

    public string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version 
                      ?? new Version(0, 0, 0, 0);
        return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    public void Dispose()
    {        
        _fileWatcher.EnableRaisingEvents = false;
        Dispose(true);
        // ReSharper disable once GCSuppressFinalizeForTypeWithoutDestructor
        GC.SuppressFinalize(this);
    }

 

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing) _fileWatcher?.Dispose();
        _disposed = true;
    }
}