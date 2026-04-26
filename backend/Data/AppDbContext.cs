using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<Backend.Models.Host> Hosts { get; set; } = null!;
        public DbSet<ConnectionGroup> ConnectionGroups { get; set; } = null!;
        public DbSet<Credential> Credentials { get; set; } = null!;
        public DbSet<Connection> Connections { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<Invitation> Invitations { get; set; } = null!;
        public DbSet<SmtpSettings> SmtpSettings { get; set; } = null!;
        public DbSet<UserGroup> UserGroups { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // M2M User <-> Role
            modelBuilder.Entity<User>()
                .HasMany(u => u.Roles)
                .WithMany(r => r.Users);

            // M2M Role <-> Permission
            modelBuilder.Entity<Role>()
                .HasMany(r => r.Permissions)
                .WithMany(p => p.Roles);

            // M2M User <-> Connection (direct assignment)
            modelBuilder.Entity<User>()
                .HasMany(u => u.Connections)
                .WithMany(c => c.Users);

            // M2M User <-> ConnectionGroup (vault assignment)
            modelBuilder.Entity<User>()
                .HasMany(u => u.ConnectionGroups)
                .WithMany(cg => cg.Users);

            // M2M User <-> UserGroup
            modelBuilder.Entity<User>()
                .HasMany(u => u.Groups)
                .WithMany(g => g.Members);

            // M2M ConnectionGroup <-> UserGroup (vault group assignments)
            modelBuilder.Entity<ConnectionGroup>()
                .HasMany(cg => cg.Groups)
                .WithMany(g => g.Vaults);

            // Self-referencing ConnectionGroup
            modelBuilder.Entity<ConnectionGroup>()
                .HasOne(cg => cg.ParentGroup)
                .WithMany(cg => cg.SubGroups)
                .HasForeignKey(cg => cg.ParentGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // Enforce unique Entra ID
            modelBuilder.Entity<User>()
                .HasIndex(u => u.EntraObjectId)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Invitation -> User (cascade so invites die with the user).
            modelBuilder.Entity<Invitation>()
                .HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Invitation>()
                .HasIndex(i => i.TokenHash)
                .IsUnique();
        }
    }
}
