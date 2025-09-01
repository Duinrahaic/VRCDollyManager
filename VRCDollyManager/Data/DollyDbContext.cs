using Microsoft.EntityFrameworkCore;
using VRCDollyManager.Models;
using System.IO;
using VRCDollyManager.Extensions;

namespace VRCDollyManager.Data;

public class DollyDbContext : DbContext
{
    public DbSet<Dolly> Dollies { get; set; }

    private readonly string _dbPath = DollyManagerFilePaths.GetDatabaseFilePath();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Dolly>()
            .HasIndex(d => d.Name)
            .IsUnique(); // Define a unique index on Name

        modelBuilder.Entity<Dolly>()
            .Property(d => d.TagsString)
            .HasColumnName("Tags"); // Store Tags as a single string
    }
}