using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        EntityBaseConfiguration.ConfigureBaseProperties(builder);

        builder.ToTable("Customers");

        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        // One profile per identity account.
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.Email);

        builder.HasOne(x => x.Cart)
            .WithOne(x => x.Customer)
            .HasForeignKey<Cart>(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Addresses)
            .WithOne(x => x.Customer!)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Orders)
            .WithOne(x => x.Customer!)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        EntityBaseConfiguration.ConfigureBaseProperties(builder);

        builder.ToTable("Addresses");

        builder.Property(x => x.RecipientName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20);
        builder.Property(x => x.Line1).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Line2).HasMaxLength(200);
        builder.Property(x => x.City).HasMaxLength(100).IsRequired();
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.PostalCode).HasMaxLength(20);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.IsDefault).HasDefaultValue(false);

        builder.HasIndex(x => x.CustomerId);
    }
}