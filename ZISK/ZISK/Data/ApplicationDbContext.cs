using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ZISK.Data.Entities;

namespace ZISK.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();
        public DbSet<ParentChild> ParentChildren => Set<ParentChild>();
        public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
        public DbSet<AbsenceRequest> AbsenceRequests => Set<AbsenceRequest>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<Document> Documents => Set<Document>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ParentChild>()
                .HasKey(pc => new { pc.ParentId, pc.ChildId });

            builder.Entity<ParentChild>()
                .HasOne(pc => pc.Parent)
                .WithMany(u => u.Children)
                .HasForeignKey(pc => pc.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ParentChild>()
                .HasOne(pc => pc.Child)
                .WithMany(c => c.Parents)
                .HasForeignKey(pc => pc.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AttendanceRecord>()
                .HasOne(ar => ar.Child)
                .WithMany(c => c.AttendanceRecords)
                .HasForeignKey(ar => ar.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AttendanceRecord>()
                .HasOne(ar => ar.MarkedByUser)
                .WithMany()
                .HasForeignKey(ar => ar.MarkedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AbsenceRequest>()
                .HasOne(ar => ar.Child)
                .WithMany(c => c.AbsenceRequests)
                .HasForeignKey(ar => ar.ChildId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AbsenceRequest>()
                .HasOne(ar => ar.Parent)
                .WithMany()
                .HasForeignKey(ar => ar.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Announcement>()
                .HasOne(a => a.AuthorUser)
                .WithMany()
                .HasForeignKey(a => a.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChildProfile>()
                .HasIndex(c => c.TeamId);

            builder.Entity<AttendanceRecord>()
                .HasIndex(ar => new { ar.TrainingEventId, ar.ChildId })
                .IsUnique();
        }
    }
}
