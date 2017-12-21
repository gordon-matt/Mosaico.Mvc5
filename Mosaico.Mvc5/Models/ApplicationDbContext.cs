using System.Data.Entity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace Mosaico.Mvc5.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<MosaicoEmail> MosaicoEmails { get; set; }

        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<ApplicationDbContext>());
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var builder = modelBuilder.Entity<MosaicoEmail>();
            builder.ToTable("MosaicoEmails");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Name).IsRequired().HasMaxLength(128).IsUnicode(true);
            builder.Property(m => m.Template).IsRequired();
            builder.Property(m => m.Metadata).IsRequired().IsUnicode(true);
            builder.Property(m => m.Content).IsRequired().IsUnicode(true);
            builder.Property(m => m.Html).IsRequired().IsUnicode(true);
        }
    }
}