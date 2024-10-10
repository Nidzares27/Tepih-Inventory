using Inventar.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventar.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : base(options)
        {
        }

        public DbSet<Tepih> Tepisi { get; set; }
    }
}
