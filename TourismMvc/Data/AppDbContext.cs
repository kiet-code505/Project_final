using Microsoft.EntityFrameworkCore;
using TourismMvc.Models.Entities;

namespace TourismMvc.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Tour> Tours => Set<Tour>();
    public DbSet<TourImage> TourImages => Set<TourImage>();
    public DbSet<ItineraryDay> ItineraryDays => Set<ItineraryDay>();
    public DbSet<TourDeparture> Departures => Set<TourDeparture>();
    public DbSet<DeparturePricing> Pricings => Set<DeparturePricing>();
    public DbSet<DepartureTransport> Transports => Set<DepartureTransport>();
    public DbSet<BranchContact> Branches => Set<BranchContact>();
    public DbSet<PolicyTemplate> Policies => Set<PolicyTemplate>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingPassenger> BookingPassengers => Set<BookingPassenger>();

    // Enum lưu dạng chuỗi giống @Enumerated(EnumType.STRING)
    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<Region>().HaveConversion<string>();
        b.Properties<Role>().HaveConversion<string>();
        b.Properties<PassengerType>().HaveConversion<string>();
        b.Properties<TransportType>().HaveConversion<string>();
        b.Properties<VehicleType>().HaveConversion<string>();
        b.Properties<BookingStatus>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Tour>().HasIndex(t => t.TourCode).IsUnique();
        m.Entity<Tour>().HasOne(t => t.StartLocation).WithMany().HasForeignKey(t => t.StartLocationId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Tour>().HasOne(t => t.EndLocation).WithMany().HasForeignKey(t => t.EndLocationId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Tour>().HasMany(t => t.Images).WithOne(i => i.Tour).HasForeignKey(i => i.TourId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<Tour>().HasMany(t => t.ItineraryDays).WithOne(i => i.Tour).HasForeignKey(i => i.TourId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<Tour>().HasMany(t => t.Departures).WithOne(d => d.Tour).HasForeignKey(d => d.TourId).OnDelete(DeleteBehavior.Cascade);

        m.Entity<TourDeparture>().HasMany(d => d.Pricings).WithOne(p => p.TourDeparture).HasForeignKey(p => p.DepartureId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<TourDeparture>().HasMany(d => d.Transports).WithOne(t => t.TourDeparture).HasForeignKey(t => t.DepartureId).OnDelete(DeleteBehavior.Cascade);
        m.Entity<TourDeparture>().HasMany(d => d.Bookings).WithOne(b => b.TourDeparture).HasForeignKey(b => b.DepartureId);
        m.Entity<TourDeparture>().HasOne(d => d.PolicyTemplate).WithMany(p => p.TourDepartures).HasForeignKey(d => d.PolicyTemplateId);

        m.Entity<PolicyTemplate>().HasIndex(p => p.TemplateName).IsUnique();
        m.Entity<PolicyTemplate>().HasOne(p => p.Contact).WithMany(c => c.Policies).HasForeignKey(p => p.ContactId);

        m.Entity<Location>().HasIndex(l => l.Slug).IsUnique();
        m.Entity<User>().HasIndex(u => u.Email).IsUnique();

        m.Entity<Booking>().HasIndex(b => b.BookingCode).IsUnique();
        m.Entity<Booking>().HasOne(b => b.User).WithMany(u => u.Bookings).HasForeignKey(b => b.UserId);
        m.Entity<Booking>().HasMany(b => b.Passengers).WithOne(p => p.Booking).HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
    }

    // Thay cho @CreatedDate / @LastModifiedDate của Spring Auditing
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        foreach (var e in ChangeTracker.Entries<BaseEntity>())
        {
            if (e.State == EntityState.Added) e.Entity.CreatedAt = now;
            if (e.State is EntityState.Added or EntityState.Modified) e.Entity.UpdatedAt = now;
        }
        return base.SaveChangesAsync(ct);
    }
}
