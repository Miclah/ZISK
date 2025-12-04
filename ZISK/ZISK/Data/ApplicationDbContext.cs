using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ZISK.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
    {
        public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();
        public DbSet<ParentChild> ParentChildren => Set<ParentChild>();
    }
}
