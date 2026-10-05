using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECommerce.Infrastructure.Persistence.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        EntityBaseConfiguration.ConfigureBaseProperties(builder);

        builder.ToTable("Carts");

        builder.Property(x => x.CustomerId).IsRequired();

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Cart!)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CustomerId).IsUnique();
    }
}

public sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        EntityBaseConfiguration.ConfigureBaseProperties(builder);

        builder.ToTable("CartItems");

        builder.Property(x => x.Quantity).IsRequired();

        builder.OwnsOne(x => x.UnitPrice, money =>
        {
            money.Property(x => x.Amount).HasColumnName("UnitPrice").HasPrecision(18, 2).IsRequired();
            money.Property(x => x.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3).IsRequired();
        });

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CartId);
        builder.HasIndex(x => x.ProductId);
    }
}