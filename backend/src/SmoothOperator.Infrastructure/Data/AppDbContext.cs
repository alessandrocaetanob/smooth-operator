using SmoothOperator.Domain.Models;
using SmoothOperator.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace SmoothOperator.Infrastructure.Data
{
    public class AppDbContext : DbContext, IAppDbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<SmoothOperator.Domain.Models.Host> Hosts { get; set; } = null!;
        public DbSet<ConnectionGroup> ConnectionGroups { get; set; } = null!;
        public DbSet<Credential> Credentials { get; set; } = null!;
        public DbSet<Connection> Connections { get; set; } = null!;
        public DbSet<ConnectionTag> ConnectionTags { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<Invitation> Invitations { get; set; } = null!;
        public DbSet<SmtpSettings> SmtpSettings { get; set; } = null!;
        public DbSet<SystemSettings> SystemSettings { get; set; } = null!;
        public DbSet<UserGroup> UserGroups { get; set; } = null!;
        public DbSet<SsoProvider> SsoProviders { get; set; } = null!;
        public DbSet<SsoAuthState> SsoAuthStates { get; set; } = null!;
        public DbSet<SecretProvider> SecretProviders { get; set; } = null!;
        public DbSet<MfaCredential> MfaCredentials { get; set; } = null!;
        public DbSet<MfaRecoveryCode> MfaRecoveryCodes { get; set; } = null!;
        public DbSet<ApiToken> ApiTokens { get; set; } = null!;
        public DbSet<WebhookEndpoint> WebhookEndpoints { get; set; } = null!;
        public DbSet<WebhookDelivery> WebhookDeliveries { get; set; } = null!;

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

            // UserGroup -> Owner (User). SetNull on user delete so groups outlive their owner.
            modelBuilder.Entity<UserGroup>()
                .HasOne(g => g.Owner)
                .WithMany()
                .HasForeignKey(g => g.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<UserGroup>()
                .Property(g => g.Description)
                .HasMaxLength(500);

            // Self-referencing ConnectionGroup
            modelBuilder.Entity<ConnectionGroup>()
                .HasOne(cg => cg.ParentGroup)
                .WithMany(cg => cg.SubGroups)
                .HasForeignKey(cg => cg.ParentGroupId)
                .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<User>()
                .HasIndex(u => u.ExternalId)
                .IsUnique()
                .HasFilter("\"ExternalId\" IS NOT NULL");

            modelBuilder.Entity<SsoAuthState>()
                .HasIndex(s => s.State)
                .IsUnique();

            modelBuilder.Entity<SsoAuthState>()
                .HasIndex(s => s.ExpiresAt);

            // Credential -> SecretProvider (optional FK; delete restrict to avoid orphaned credentials)
            modelBuilder.Entity<Credential>()
                .HasOne(c => c.SecretProvider)
                .WithMany()
                .HasForeignKey(c => c.SecretProviderId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // ⚡ Bolt: Composite index to avoid full table scans on ConnectionsController.GetMyLastConnected
            // Query filter: a.UserId == profile.UserId && a.Action == "connection.started"
            // Group by: a.ResourceId, Select: max(a.Timestamp)
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.UserId, a.Action, a.ResourceId, a.Timestamp });

            // MfaCredential: one-to-one with User (cascade on user delete)
            modelBuilder.Entity<MfaCredential>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MfaCredential>()
                .HasIndex(m => m.UserId)
                .IsUnique();

            // MfaRecoveryCode: cascade on credential delete
            modelBuilder.Entity<MfaRecoveryCode>()
                .HasOne(r => r.MfaCredential)
                .WithMany(m => m.RecoveryCodes)
                .HasForeignKey(r => r.MfaCredentialId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApiToken: cascade on user delete
            modelBuilder.Entity<ApiToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Globally unique lookup (used for O(1) auth scheme dispatch)
            modelBuilder.Entity<ApiToken>()
                .HasIndex(t => t.TokenLookup)
                .IsUnique();

            // One token name per user (so list UI can't show ambiguous duplicates)
            modelBuilder.Entity<ApiToken>()
                .HasIndex(t => new { t.UserId, t.Name })
                .IsUnique();

            modelBuilder.Entity<ApiToken>()
                .Property(t => t.Name)
                .HasMaxLength(100);

            // WebhookEndpoint column lengths
            modelBuilder.Entity<WebhookEndpoint>()
                .Property(w => w.Name)
                .HasMaxLength(100);

            modelBuilder.Entity<WebhookEndpoint>()
                .Property(w => w.Url)
                .HasMaxLength(2048);

            modelBuilder.Entity<WebhookEndpoint>()
                .Property(w => w.EventTypes)
                .HasMaxLength(1024);

            modelBuilder.Entity<WebhookEndpoint>()
                .Property(w => w.LastDeliveryStatus)
                .HasMaxLength(16);

            // WebhookDelivery -> WebhookEndpoint: cascade so queued deliveries die with their endpoint
            modelBuilder.Entity<WebhookDelivery>()
                .HasOne(d => d.Endpoint)
                .WithMany(w => w.Deliveries)
                .HasForeignKey(d => d.WebhookEndpointId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WebhookDelivery>()
                .Property(d => d.EventType)
                .HasMaxLength(128);

            // Drives the background worker's "due deliveries" query
            modelBuilder.Entity<WebhookDelivery>()
                .HasIndex(d => new { d.Status, d.NextAttemptAt });
        }
    }
}
