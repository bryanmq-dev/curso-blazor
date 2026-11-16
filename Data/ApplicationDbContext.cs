using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using sharpness_sharp.Data.Entities;
using sharpness_sharp.Data.Models;

namespace sharpness_sharp.Data;

public class ApplicationAuthDbContext(DbContextOptions<ApplicationAuthDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Cafe> Cafes => Set<Cafe>();
    public DbSet<Ventas> Ventas => Set<Ventas>();
    public DbSet<Destination> Destinations => Set<Destination>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TravelPackage> TravelPackages => Set<TravelPackage>();
    public DbSet<TravelPackageCategory> TravelPackageCategories => Set<TravelPackageCategory>();
    public DbSet<TravelImage> TravelImages => Set<TravelImage>();
    public DbSet<ItineraryDay> ItineraryDays => Set<ItineraryDay>();
    public DbSet<TravelDeparture> TravelDepartures => Set<TravelDeparture>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationStatusHistory> ReservationStatusHistory => Set<ReservationStatusHistory>();


    protected override void OnModelCreating(ModelBuilder builder)
    {

        builder.Entity<Ventas>(venta =>
        {
            venta.HasOne(venta => venta.Cliente)
                .WithMany()
                .HasForeignKey(venta => venta.Id_Cliente);

            venta.HasOne(venta => venta.Cafe).WithMany().HasForeignKey(venta => venta.Id_Cafe);
        });

        builder.Entity<TravelPackageCategory>()
            .HasKey(item => new { item.TravelPackageId, item.CategoryId });

        builder.Entity<TravelPackageCategory>()
            .HasOne(item => item.TravelPackage)
            .WithMany(item => item.TravelPackageCategories)
            .HasForeignKey(item => item.TravelPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TravelPackageCategory>()
            .HasOne(item => item.Category)
            .WithMany(item => item.TravelPackageCategories)
            .HasForeignKey(item => item.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Reservation>(reservation =>
        {
            reservation.HasIndex(item => item.Code).IsUnique();
            reservation.HasIndex(item => new { item.UserId, item.Status });
            reservation.Property(item => item.UnitPrice).HasPrecision(18, 2);
            reservation.Property(item => item.TotalPrice).HasPrecision(18, 2);
            reservation.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            reservation.HasOne(item => item.TravelDeparture)
                .WithMany(item => item.Reservations)
                .HasForeignKey(item => item.TravelDepartureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReservationStatusHistory>(history =>
        {
            history.HasIndex(item => new { item.ReservationId, item.ChangedAt });
            history.Property(item => item.PreviousStatus).HasMaxLength(32);
            history.Property(item => item.NewStatus).HasMaxLength(32);
            history.HasOne(item => item.Reservation)
                .WithMany(item => item.StatusHistory)
                .HasForeignKey(item => item.ReservationId)
                .OnDelete(DeleteBehavior.Cascade);
            history.HasOne(item => item.ChangedByUser)
                .WithMany()
                .HasForeignKey(item => item.ChangedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TravelDeparture>().Property(item => item.Price).HasPrecision(18, 2);
        builder.Entity<TravelPackage>().Property(item => item.Price).HasPrecision(18, 2);
        base.OnModelCreating(builder);
    }
}

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
}
