using Microsoft.EntityFrameworkCore;
using VRCDollyManager.Models;
using System.IO;

namespace VRCDollyManager.Data;

public class DollyDbContext : DbContext
{
    public DbSet<Dolly> Dollies { get; set; }

    private readonly string _dbPath;

    public DollyDbContext()
    {
        _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "VRChat", "CameraPaths", "vdm_database.sqlite");
    }

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