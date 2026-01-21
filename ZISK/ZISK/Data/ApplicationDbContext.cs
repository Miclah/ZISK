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

        public DbSet<Team> Teams => Set<Team>();
        public DbSet<TrainingEvent> TrainingEvents => Set<TrainingEvent>();
        public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();
        public DbSet<ParentChild> ParentChildren => Set<ParentChild>();
        public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
        public DbSet<AbsenceRequest> AbsenceRequests => Set<AbsenceRequest>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<AnnouncementAttachment> AnnouncementAttachments => Set<AnnouncementAttachment>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<CoachTeam> CoachTeams => Set<CoachTeam>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Team>()
                .HasIndex(t => t.Name)
                .IsUnique();

            builder.Entity<ChildProfile>()
                .HasOne(c => c.Team)
                .WithMany(t => t.Members)
                .HasForeignKey(c => c.TeamId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ChildProfile>()
                .HasIndex(c => c.TeamId);

            builder.Entity<TrainingEvent>()
                .HasOne(te => te.Team)
                .WithMany(t => t.TrainingEvents)
                .HasForeignKey(te => te.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TrainingEvent>()
                .HasIndex(te => new { te.TeamId, te.StartTime });

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
                .HasOne(ar => ar.TrainingEvent)
                .WithMany(te => te.AttendanceRecords)
                .HasForeignKey(ar => ar.TrainingEventId)
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

            builder.Entity<AttendanceRecord>()
                .HasIndex(ar => new { ar.TrainingEventId, ar.ChildId })
                .IsUnique();

            builder.Entity<AbsenceRequest>()
                .HasOne(ar => ar.TrainingEvent)
                .WithMany(te => te.AbsenceRequests)
                .HasForeignKey(ar => ar.TrainingEventId)
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

            builder.Entity<AbsenceRequest>()
                .HasOne(ar => ar.ReviewedByUser)
                .WithMany()
                .HasForeignKey(ar => ar.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Announcement>()
                .HasOne(a => a.AuthorUser)
                .WithMany()
                .HasForeignKey(a => a.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Announcement>()
                .HasOne(a => a.TargetTeam)
                .WithMany()
                .HasForeignKey(a => a.TargetTeamId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Announcement>()
                .HasIndex(a => a.PublishDate);

            builder.Entity<AnnouncementAttachment>()
                .HasOne(aa => aa.Announcement)
                .WithMany(a => a.Attachments)
                .HasForeignKey(aa => aa.AnnouncementId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CoachTeam>()
                .HasIndex(ct => new { ct.CoachId, ct.TeamId })
                .IsUnique();

            builder.Entity<CoachTeam>()
                .HasOne(ct => ct.Coach)
                .WithMany()
                .HasForeignKey(ct => ct.CoachId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CoachTeam>()
                .HasOne(ct => ct.Team)
                .WithMany()
                .HasForeignKey(ct => ct.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
