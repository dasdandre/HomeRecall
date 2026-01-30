using Microsoft.EntityFrameworkCore;

namespace HomeRecall;

public class BackupContext : DbContext
{
    public BackupContext(DbContextOptions<BackupContext> options) : base(options) { }

    public DbSet<Device> Devices { get; set; }
    public DbSet<Backup> Backups { get; set; }
    public DbSet<AppSettings> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>()
            .HasMany(d => d.Backups)
            .WithOne(b => b.Device)
            .HasForeignKey(b => b.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Initial seeding for AppSettings
        modelBuilder.Entity<AppSettings>().HasData(
            new AppSettings { Id = 1, AutoBackupEnabled = false, RetentionMode = RetentionMode.Smart }
        );
    }
}
