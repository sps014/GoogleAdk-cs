// Copyright 2025 Google LLC
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;

namespace GoogleAdk.Sessions.EfCore;

/// <summary>
/// EF Core entity for storing sessions.
/// </summary>
public class StorageSession
{
    /// <summary>Session ID.</summary>
    public string Id { get; set; } = null!;

    /// <summary>Application name.</summary>
    public string AppName { get; set; } = null!;

    /// <summary>User ID.</summary>
    public string UserId { get; set; } = null!;

    /// <summary>Session-scoped state as JSON.</summary>
    public string StateJson { get; set; } = "{}";

    /// <summary>Created UTC.</summary>
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    /// <summary>Last updated UTC.</summary>
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;

    public ICollection<StorageEvent> Events { get; set; } = new List<StorageEvent>();
}

/// <summary>
/// EF Core entity for storing events.
/// </summary>
public class StorageEvent
{
    /// <summary>Auto-generated primary key.</summary>
    public long RowId { get; set; }

    /// <summary>Event ID.</summary>
    public string Id { get; set; } = null!;

    /// <summary>Application name (FK part).</summary>
    public string AppName { get; set; } = null!;

    /// <summary>User ID (FK part).</summary>
    public string UserId { get; set; } = null!;

    /// <summary>Session ID (FK part).</summary>
    public string SessionId { get; set; } = null!;

    /// <summary>Invocation ID for deduplication.</summary>
    public string? InvocationId { get; set; }

    /// <summary>Event timestamp.</summary>
    public long Timestamp { get; set; }

    /// <summary>Full event data as JSON.</summary>
    public string EventDataJson { get; set; } = "{}";

    public StorageSession? Session { get; set; }
}

/// <summary>
/// EF Core entity for app-scoped state.
/// </summary>
public class StorageAppState
{
    public string AppName { get; set; } = null!;
    public string StateJson { get; set; } = "{}";
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// EF Core entity for user-scoped state.
/// </summary>
public class StorageUserState
{
    public string AppName { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string StateJson { get; set; } = "{}";
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// The DbContext for session persistence.
/// Supports any EF Core provider (SQLite, SQL Server, PostgreSQL, MySQL).
/// </summary>
public class AdkSessionDbContext : DbContext
{
    public DbSet<StorageSession> Sessions => Set<StorageSession>();
    public DbSet<StorageEvent> Events => Set<StorageEvent>();
    public DbSet<StorageAppState> AppStates => Set<StorageAppState>();
    public DbSet<StorageUserState> UserStates => Set<StorageUserState>();

    public AdkSessionDbContext(DbContextOptions<AdkSessionDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StorageSession>(entity =>
        {
            entity.HasKey(e => new { e.AppName, e.UserId, e.Id });
            entity.Property(e => e.StateJson).HasColumnType("text");
        });

        modelBuilder.Entity<StorageEvent>(entity =>
        {
            entity.HasKey(e => e.RowId);
            entity.Property(e => e.RowId).ValueGeneratedOnAdd();
            entity.HasIndex(e => new { e.AppName, e.UserId, e.SessionId });
            entity.HasOne(e => e.Session)
                .WithMany(s => s.Events)
                .HasForeignKey(e => new { e.AppName, e.UserId, e.SessionId })
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.EventDataJson).HasColumnType("text");
        });

        modelBuilder.Entity<StorageAppState>(entity =>
        {
            entity.HasKey(e => e.AppName);
            entity.Property(e => e.StateJson).HasColumnType("text");
        });

        modelBuilder.Entity<StorageUserState>(entity =>
        {
            entity.HasKey(e => new { e.AppName, e.UserId });
            entity.Property(e => e.StateJson).HasColumnType("text");
        });
    }
}
