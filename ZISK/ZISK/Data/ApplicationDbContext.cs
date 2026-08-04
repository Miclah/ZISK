using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ZISK.Data.Entities;
using ZISK.Services.Demo;

// Pomoc s AI
namespace ZISK.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IDemoSessionContext? _demoSessionContext;

        // Bound to by every IDemoScoped HasQueryFilter below. Reading it off the DbContext
        // instance (rather than closing over a local variable) is EF Core's standard
        // multi-tenant filter pattern - it makes the predicate a per-instance runtime
        // parameter instead of a value baked into the first compiled query plan. Outside
        // ZISK_SEED_MODE=demo (or in tests that construct the context directly, bypassing DI)
        // _demoSessionContext is null and this is always null, which makes every filter
        // below "DemoSessionId == null" - i.e. a no-op against unscoped data.
        public Guid? DemoSessionId => _demoSessionContext?.Current;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IDemoSessionContext? demoSessionContext = null)
            : base(options)
        {
            _demoSessionContext = demoSessionContext;
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
        public DbSet<DemoSession> DemoSessions => Set<DemoSession>();
        public DbSet<DemoTemplateMeta> DemoTemplateMetas => Set<DemoTemplateMeta>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Uniqueness is scoped per demo session so that two visitors' clones (or the
            // shared template, DemoSessionId == null) never collide on phone/RC/team name -
            // each is its own independent copy of the same starting data. Outside demo mode
            // DemoSessionId is always null for every row, so this behaves exactly like a plain
            // unique index over PhoneNumber/RodneCislo/Name, same as before.
            builder.Entity<ApplicationUser>()
                .HasIndex(u => new { u.DemoSessionId, u.PhoneNumber })
                .IsUnique()
                .HasFilter("[PhoneNumber] IS NOT NULL");

            builder.Entity<ApplicationUser>()
                .HasIndex(u => new { u.DemoSessionId, u.RodneCislo })
                .IsUnique()
                .HasFilter("[RodneCislo] IS NOT NULL");

            builder.Entity<ApplicationUser>()
                .Property(u => u.PhoneNumber)
                .HasMaxLength(20);

            builder.Entity<Team>()
                .HasIndex(t => new { t.DemoSessionId, t.Name })
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
            // Filtered unique index: only rows where IsActive=1 are included, so there can be at most one active season at a time.
            // Inactive seasons (IsActive=0/null) are not covered by the index and can coexist freely.
            // Scoped by DemoSessionId so every demo clone (and the template) can each have their own active season.
            builder.Entity<Season>()
                .HasIndex(s => new { s.DemoSessionId, s.IsActive })
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

            // DemoTemplateMeta is a single-row table (Id is always 1, set explicitly) -
            // without this, EF's SqlServer provider defaults an int PK to IDENTITY, which
            // then rejects the explicit Id = 1 insert.
            builder.Entity<DemoTemplateMeta>()
                .Property(m => m.Id)
                .ValueGeneratedNever();

            // Demo-session isolation: every IDemoScoped entity is filtered to rows whose
            // DemoSessionId matches the current request's demo session (see DemoSessionId
            // property above). Applied uniformly and declaratively here so no service or
            // controller had to be touched to get per-visitor isolation - the same reasoning
            // behind CLAUDE.md's warning that team-scoping was applied ad hoc per-service and
            // some endpoints were missed. This can't repeat that mistake because it isn't
            // opt-in per query.
            builder.Entity<Team>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<TeamMember>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<CoachTeam>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<Season>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<TrainingSeries>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<TrainingEvent>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<AttendanceRecord>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<AbsenceRequest>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<Announcement>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<AnnouncementAttachment>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<Document>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<ParentInvitation>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<ParentChild>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
            builder.Entity<ApplicationUser>().HasQueryFilter(e => e.DemoSessionId == DemoSessionId);
        }
    }
}
