using FabriTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FabriTrack.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Fabric> Fabrics { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Fabric>()
                .HasOne(f => f.Category)
                .WithMany(c => c.Fabrics)
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Fabric>()
                .HasOne(f => f.Supplier)
                .WithMany(s => s.Fabrics)
                .HasForeignKey(f => f.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Sale>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<SaleItem>()
                .HasOne(si => si.Sale)
                .WithMany(s => s.SaleItems)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SaleItem>()
                .HasOne(si => si.Fabric)
                .WithMany(f => f.SaleItems)
                .HasForeignKey(si => si.FabricId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed a couple of categories so the app isn't empty on first run
            builder.Entity<Category>().HasData(
                new Category { CategoryId = 1, Name = "Cotton", Description = "Cotton-based fabrics" },
                new Category { CategoryId = 2, Name = "Silk", Description = "Silk-based fabrics" },
                new Category { CategoryId = 3, Name = "Linen", Description = "Linen-based fabrics" }
            );
        }
    }
}
