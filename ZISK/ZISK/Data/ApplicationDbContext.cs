using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ZISK.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<ClubRole> ClubRoles => Set<ClubRole>();
        public DbSet<Excuse> Excuses => Set<Excuse>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // UserProfile -> ApplicationUser (1:1 voliteľný)
            builder.Entity<UserProfile>()
                .HasOne(up => up.IdentityUser)
                .WithOne(au => au.UserProfile)
                .HasForeignKey<UserProfile>(up => up.IdentityUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // UserProfile self-referencia (Parent -> Children)
            builder.Entity<UserProfile>()
                .HasOne(up => up.Parent)
                .WithMany(up => up.Children)
                .HasForeignKey(up => up.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Excuse - viacero FK na UserProfile
            builder.Entity<Excuse>()
                .HasOne(e => e.SubjectUserProfile)
                .WithMany()
                .HasForeignKey(e => e.SubjectUserProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Excuse>()
                .HasOne(e => e.ReporterUserProfile)
                .WithMany()
                .HasForeignKey(e => e.ReporterUserProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Excuse>()
                .HasOne(e => e.ReviewedByUserProfile)
                .WithMany()
                .HasForeignKey(e => e.ReviewedByUserProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexy
            builder.Entity<UserProfile>()
                .HasIndex(up => up.IdentityUserId)
                .IsUnique()
                .HasFilter("[IdentityUserId] IS NOT NULL");

            builder.Entity<ClubRole>()
                .HasIndex(cr => new { cr.UserProfileId, cr.RoleType, cr.ContextId })
                .IsUnique();
        }
    }
}
