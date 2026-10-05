using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        EntityBaseConfiguration.ConfigureBaseProperties(builder);

        builder.ToTable("Orders");

        builder.Property(x => x.OrderNumber).HasMaxLength(32).IsRequired();
        builder.Property(x => x.OrderDate).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.PaymentStatus).HasConversion<int>();
        builder.Property(x => x.ShippingMethod).HasConversion<int>();

        // The address is a snapshot, owned by the order row so it is always non-null.
        builder.OwnsOne(x => x.ShippingAddress, address =>
        {
            address.Property(x => x.RecipientName).HasColumnName("ShipToName").HasMaxLength(120).IsRequired();
            address.Property(x => x.PhoneNumber).HasColumnName("ShipToPhone").HasMaxLength(20).IsRequired();
            address.Property(x => x.Line1).HasColumnName("ShipToLine1").HasMaxLength(200).IsRequired();
            address.Property(x => x.Line2).HasColumnName("ShipToLine2").HasMaxLength(200);
            address.Property(x => x.City).HasColumnName("ShipToCity").HasMaxLength(100).IsRequired();
            address.Property(x => x.State).HasColumnName("ShipToState").HasMaxLength(100);
            address.Property(x => x.PostalCode).HasColumnName("ShipToPostalCode").HasMaxLength(20).IsRequired();
            address.Property(x => x.Country).HasColumnName("ShipToCountry").HasMaxLength(100).IsRequired();
        });

        builder.OwnsOne(x => x.SubTotal, money => ConfigureMoney(money, "SubTotal"));
        builder.OwnsOne(x => x.DiscountAmount, money => ConfigureMoney(money, "DiscountAmount"));
        builder.OwnsOne(x => x.ShippingCost, money => ConfigureMoney(money, "ShippingCost"));
        builder.OwnsOne(x => x.TotalAmount, money => ConfigureMoney(money, "TotalAmount"));

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Order!)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.CustomerId, x.OrderDate });

        static void ConfigureMoney(OwnedNavigationBuilder<Order, Domain.ValueObjects.Money> money, string prefix)
        {
            money.Property(x => x.Amount).HasColumnName(prefix).HasPrecision(18, 2).IsRequired();
            money.Property(x => x.Currency).HasColumnName(prefix + "Currency").HasMaxLength(3).IsRequired();
        }
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        EntityBaseConfiguration.ConfigureBaseProperties(builder);

        builder.ToTable("OrderItems");

        builder.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Sku).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();

        builder.OwnsOne(x => x.UnitPrice, money =>
        {
            money.Property(x => x.Amount).HasColumnName("UnitPrice").HasPrecision(18, 2).IsRequired();
            money.Property(x => x.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3).IsRequired();
        });

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => x.ProductId);
    }
}

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        EntityBaseConfiguration.ConfigureBaseProperties(builder);

        builder.ToTable("Reviews");

        builder.Property(x => x.Rating).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.Comment).HasMaxLength(2000);
        builder.Property(x => x.IsApproved).HasDefaultValue(true);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // A shopper may review a product only once.
        builder.HasIndex(x => new { x.ProductId, x.CustomerId }).IsUnique();
        builder.HasIndex(x => new { x.ProductId, x.IsApproved });
    }
}