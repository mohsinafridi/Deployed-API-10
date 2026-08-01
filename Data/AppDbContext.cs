using API.Models;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Name).HasMaxLength(100);
            entity.Property(u => u.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(o => o.Product)
                .WithMany(p => p.Orders)
                .HasForeignKey(o => o.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Seed data so the deployed database starts with real rows
        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, Name = "Mohsin Azam", Email = "mohsin.azam@gmail.ae", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new User { Id = 2, Name = "Sara Khan", Email = "sara.khan@example.com", CreatedAt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new User { Id = 3, Name = "Ali Ahmed", Email = "ali.ahmed@example.com", CreatedAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        modelBuilder.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Laptop", Price = 3499.00m, Stock = 25 },
            new Product { Id = 2, Name = "Wireless Mouse", Price = 89.50m, Stock = 150 },
            new Product { Id = 3, Name = "USB-C Dock", Price = 425.00m, Stock = 40 }
        );

        modelBuilder.Entity<Order>().HasData(
            new Order { Id = 1, UserId = 1, ProductId = 1, Quantity = 1, OrderDate = new DateTime(2026, 3, 1, 10, 30, 0, DateTimeKind.Utc) },
            new Order { Id = 2, UserId = 2, ProductId = 2, Quantity = 2, OrderDate = new DateTime(2026, 3, 5, 14, 0, 0, DateTimeKind.Utc) },
            new Order { Id = 3, UserId = 1, ProductId = 3, Quantity = 1, OrderDate = new DateTime(2026, 3, 10, 9, 15, 0, DateTimeKind.Utc) }
        );
    }
}
