using Microsoft.EntityFrameworkCore;

namespace HomeRecall;

public class BackupContext : DbContext
{
    public BackupContext(DbContextOptions<BackupContext> options) : base(options) { }

    public DbSet<Device> Devices { get; set; }
    public DbSet<Backup> Backups { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>()
            .HasMany(d => d.Backups)
            .WithOne(b => b.Device)
            .HasForeignKey(b => b.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
