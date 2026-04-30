using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ZISK.Data.Entities;

// Pomoc s AI
namespace ZISK.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Team> Teams => Set<Team>();
        public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
        public DbSet<TrainingEvent> TrainingEvents => Set<TrainingEvent>();
        public DbSet<TrainingSeries> TrainingSeries => Set<TrainingSeries>();
        public DbSet<Season> Seasons => Set<Season>();
        public DbSet<ParentChild> ParentChildren => Set<ParentChild>();
        public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
        public DbSet<AbsenceRequest> AbsenceRequests => Set<AbsenceRequest>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<AnnouncementAttachment> AnnouncementAttachments => Set<AnnouncementAttachment>();
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<CoachTeam> CoachTeams => Set<CoachTeam>();
        public DbSet<ParentInvitation> ParentInvitations => Set<ParentInvitation>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.PhoneNumber)
                .IsUnique()
                .HasFilter("[PhoneNumber] IS NOT NULL");

            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.RodneCislo)
                .IsUnique()
                .HasFilter("[RodneCislo] IS NOT NULL");

            builder.Entity<ApplicationUser>()
                .Property(u => u.PhoneNumber)
                .HasMaxLength(20);

            builder.Entity<Team>()
                .HasIndex(t => t.Name)
                .IsUnique();

            // TeamMember
            builder.Entity<TeamMember>()
                .HasKey(tm => new { tm.TeamId, tm.UserId });

            builder.Entity<TeamMember>()
                .HasOne(tm => tm.Team)
                .WithMany(t => t.Memberships)
                .HasForeignKey(tm => tm.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TeamMember>()
                .HasOne(tm => tm.User)
                .WithMany(u => u.TeamMemberships)
                .HasForeignKey(tm => tm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Season
            builder.Entity<Season>()
                .HasIndex(s => s.IsActive)
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            // TrainingSeries
            builder.Entity<TrainingSeries>()
                .HasOne(ts => ts.Team)
                .WithMany()
                .HasForeignKey(ts => ts.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TrainingSeries>()
                .HasOne(ts => ts.Coach)
                .WithMany()
                .HasForeignKey(ts => ts.CoachId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TrainingSeries>()
                .HasOne(ts => ts.Season)
                .WithMany(s => s.Series)
                .HasForeignKey(ts => ts.SeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            // TrainingEvent
            builder.Entity<TrainingEvent>()
                .HasOne(te => te.Team)
                .WithMany(t => t.TrainingEvents)
                .HasForeignKey(te => te.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TrainingEvent>()
                .HasOne(te => te.Series)
                .WithMany(ts => ts.Instances)
                .HasForeignKey(te => te.SeriesId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<TrainingEvent>()
                .HasOne(te => te.Season)
                .WithMany(s => s.Trainings)
                .HasForeignKey(te => te.SeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TrainingEvent>()
                .HasIndex(te => new { te.SeasonId, te.StartTime });

            builder.Entity<TrainingEvent>()
                .HasIndex(te => new { te.TeamId, te.StartTime });

            builder.Entity<ParentChild>()
                .HasKey(pc => new { pc.ParentId, pc.ChildId });

            builder.Entity<ParentChild>()
                .HasOne(pc => pc.Parent)
                .WithMany(u => u.Children)
                .HasForeignKey(pc => pc.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ParentChild>()
                .HasOne(pc => pc.Child)
                .WithMany(u => u.Parents)
                .HasForeignKey(pc => pc.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            // AttendanceRecord
            builder.Entity<AttendanceRecord>()
                .HasOne(ar => ar.TrainingEvent)
                .WithMany(te => te.AttendanceRecords)
                .HasForeignKey(ar => ar.TrainingEventId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AttendanceRecord>()
                .HasOne(ar => ar.Child)
                .WithMany()
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

            // AbsenceRequest
            builder.Entity<AbsenceRequest>()
                .HasIndex(ar => ar.Status);

            builder.Entity<AbsenceRequest>()
                .HasOne(ar => ar.TrainingEvent)
                .WithMany(te => te.AbsenceRequests)
                .HasForeignKey(ar => ar.TrainingEventId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AbsenceRequest>()
                .HasOne(ar => ar.Child)
                .WithMany()
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

            builder.Entity<Document>()
                .HasOne(d => d.UploadedByUser)
                .WithMany()
                .HasForeignKey(d => d.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<CoachTeam>()
                .HasIndex(ct => new { ct.CoachId, ct.TeamId })
                .IsUnique();

            builder.Entity<CoachTeam>()
                .HasOne(ct => ct.Coach)
                .WithMany(u => u.CoachTeams)
                .HasForeignKey(ct => ct.CoachId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<CoachTeam>()
                .HasOne(ct => ct.Team)
                .WithMany(t => t.Coaches)
                .HasForeignKey(ct => ct.TeamId)
                .OnDelete(DeleteBehavior.Cascade);

            // ParentInvitation
            builder.Entity<ParentInvitation>()
                .HasOne(pi => pi.Child)
                .WithMany()
                .HasForeignKey(pi => pi.ChildUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ParentInvitation>()
                .HasOne(pi => pi.Initiator)
                .WithMany()
                .HasForeignKey(pi => pi.InitiatorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ParentInvitation>()
                .HasOne(pi => pi.UsedBy)
                .WithMany()
                .HasForeignKey(pi => pi.UsedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ParentInvitation>()
                .HasIndex(pi => new { pi.ChildUserId, pi.UsedAt });

            builder.Entity<ParentInvitation>()
                .HasIndex(pi => pi.CodeHash);
        }
    }
}
