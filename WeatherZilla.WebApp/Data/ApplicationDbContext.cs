using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WeatherZilla.WebApp.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public DbSet<FavoritePlace> Favorites => Set<FavoritePlace>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.HasSequence<int>("FavoriteIds");
            builder.Entity<FavoritePlace>(entity =>
            {
                entity.ToTable("AspNetFavorites");
                entity.Property(f => f.Id).HasDefaultValueSql("NEXT VALUE FOR [FavoriteIds]");
                entity.Property(f => f.UserId).HasMaxLength(450).IsRequired();
                entity.Property(f => f.Place).HasMaxLength(128).IsRequired();
                entity.HasIndex(f => new { f.UserId, f.Place }).IsUnique();
                entity.HasOne<Microsoft.AspNetCore.Identity.IdentityUser>().WithMany()
                    .HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
            });
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
    }
}