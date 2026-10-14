using Backend_Quques_Kashnikov.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend_Quques_Kashnikov.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<StudyQueue> Queues => Set<StudyQueue>();
    public DbSet<QueueEntry> Entries => Set<QueueEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AppUser>().HasIndex(u => u.PassNumber).IsUnique();
        model.Entity<QueueEntry>().HasIndex(e => new { e.StudyQueueId, e.AppUserId }).IsUnique();
        model.Entity<QueueEntry>().HasIndex(e => new { e.StudyQueueId, e.Sequence }).IsUnique();
        model.Entity<QueueEntry>().HasOne<StudyQueue>().WithMany().HasForeignKey(e => e.StudyQueueId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<QueueEntry>().HasOne<AppUser>().WithMany().HasForeignKey(e => e.AppUserId).OnDelete(DeleteBehavior.Restrict);
        // SQLite не сохраняет DateTime.Kind. Восстанавливаем UTC, чтобы JSON содержал суффикс Z.
        model.Entity<StudyQueue>().Property(q => q.OpensAtUtc).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        model.Entity<StudyQueue>().Property(q => q.StartsAtUtc).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        model.Entity<StudyQueue>().Property(q => q.ClosesAtUtc).HasConversion(
            v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null);
        model.Entity<QueueEntry>().Property(e => e.JoinedAtUtc).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}
